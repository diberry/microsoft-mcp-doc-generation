using System.Text.Json;

namespace DocGeneration.Tools.Fingerprint;

/// <summary>
/// CLI entry point for the fingerprint tool.
/// Usage:
///   dotnet run -- snapshot [--namespace ns] [--output path]
///   dotnet run -- diff --baseline path --candidate path [--output path]
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        return command switch
        {
            "snapshot" => RunSnapshot(args[1..]),
            "diff" => RunDiff(args[1..]),
            _ => Error($"Unknown command: {command}. Use 'snapshot' or 'diff'.")
        };
    }

    private static int RunSnapshot(string[] args)
    {
        string? ns = null;
        string? outputPath = null;
        string? repoRoot = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--namespace" or "-n" when i + 1 < args.Length:
                    ns = args[++i];
                    break;
                case "--output" or "-o" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--repo-root" or "-r" when i + 1 < args.Length:
                    repoRoot = args[++i];
                    break;
            }
        }

        repoRoot ??= FindRepoRoot();
        if (repoRoot is null)
            return Error("Could not find repo root. Use --repo-root to specify.");

        Console.WriteLine($"📸 Generating fingerprint snapshot...");
        Console.WriteLine($"   Repo root: {repoRoot}");
        if (ns is not null)
            Console.WriteLine($"   Namespace: {ns}");

        var generator = new SnapshotGenerator(repoRoot);
        var snapshot = generator.GenerateSnapshot(ns);

        if (snapshot.Namespaces.Count == 0)
        {
            Console.WriteLine("⚠️  No generated-* directories found.");
            return 1;
        }

        outputPath ??= Path.Combine(repoRoot, "fingerprint-baseline.json");
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"✅ Snapshot saved to: {outputPath}");
        Console.WriteLine($"   Namespaces: {snapshot.Namespaces.Count}");
        Console.WriteLine($"   Total files: {snapshot.Namespaces.Values.Sum(n => n.FileCount)}");

        // Print per-namespace summary
        foreach (var (name, fp) in snapshot.Namespaces.OrderBy(kv => kv.Key))
        {
            var tfInfo = fp.ToolFamilyArticle is not null
                ? $", tool-family: {fp.ToolFamilyArticle.H2Headings.Count} H2s"
                : "";
            Console.WriteLine($"   {name}: {fp.FileCount} files, {SnapshotDiffer.FormatSize(fp.TotalSizeBytes)}{tfInfo}");
        }

        return 0;
    }

    private static int RunDiff(string[] args)
    {
        string? baselinePath = null;
        string? candidatePath = null;
        string? outputPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--baseline" or "-b" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    break;
                case "--candidate" or "-c" when i + 1 < args.Length:
                    candidatePath = args[++i];
                    break;
                case "--output" or "-o" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
            }
        }

        if (baselinePath is null || candidatePath is null)
            return Error("Both --baseline and --candidate paths are required for diff.");

        if (!File.Exists(baselinePath))
            return Error($"Baseline file not found: {baselinePath}");
        if (!File.Exists(candidatePath))
            return Error($"Candidate file not found: {candidatePath}");

        Console.WriteLine($"📊 Computing diff...");
        Console.WriteLine($"   Baseline:  {baselinePath}");
        Console.WriteLine($"   Candidate: {candidatePath}");

        var baseline = JsonSerializer.Deserialize<FingerprintSnapshot>(
            File.ReadAllText(baselinePath), JsonOptions);
        var candidate = JsonSerializer.Deserialize<FingerprintSnapshot>(
            File.ReadAllText(candidatePath), JsonOptions);

        if (baseline is null)
            return Error($"Failed to parse baseline JSON: {baselinePath}");
        if (candidate is null)
            return Error($"Failed to parse candidate JSON: {candidatePath}");

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);
        var report = SnapshotDiffer.GenerateReport(diff);

        if (outputPath is not null)
        {
            File.WriteAllText(outputPath, report);
            Console.WriteLine($"✅ Diff report saved to: {outputPath}");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(report);
        }

        // Return non-zero if regressions detected
        var regressionCount = diff.NamespaceDiffs.Values.Count(d => d.QualityRegressions.Count > 0);
        if (regressionCount > 0)
        {
            Console.WriteLine($"⚠️  {regressionCount} namespace(s) with quality regressions.");
            return 1;
        }

        return 0;
    }

    private static string? FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "mcp-doc-generation.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static int Error(string message)
    {
        Console.Error.WriteLine($"❌ {message}");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Azure MCP Documentation Fingerprint Tool

            Usage:
              fingerprint snapshot [options]    Generate a fingerprint snapshot of generated output
              fingerprint diff [options]        Compare two snapshots and generate a diff report

            Snapshot options:
              --namespace, -n <name>    Fingerprint a single namespace (default: all)
              --output, -o <path>       Output file path (default: ./fingerprint-baseline.json)
              --repo-root, -r <path>    Repository root (default: auto-detect)

            Diff options:
              --baseline, -b <path>     Path to baseline snapshot JSON (required)
              --candidate, -c <path>    Path to candidate snapshot JSON (required)
              --output, -o <path>       Output file for diff report (default: stdout)

            Examples:
              dotnet run -- snapshot
              dotnet run -- snapshot --namespace advisor --output advisor-snapshot.json
              dotnet run -- diff --baseline before.json --candidate after.json
              dotnet run -- diff -b before.json -c after.json -o report.md
            """);
    }
}
