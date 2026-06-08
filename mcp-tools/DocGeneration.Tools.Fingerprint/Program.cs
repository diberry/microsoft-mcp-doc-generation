using System.Text.Json;
using System.Text;

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
    private static readonly UTF8Encoding Utf8NoBom = new(false);

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
            "golden" => RunGolden(args[1..]),
            _ => Error($"Unknown command: {command}. Use 'snapshot', 'diff', or 'golden'.")
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
        File.WriteAllText(outputPath, json, Utf8NoBom);

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
            File.WriteAllText(outputPath, report, Encoding.UTF8);
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

    private static int RunGolden(string[] args)
    {
        if (args.Length == 0)
        {
            return Error("Golden command requires a subcommand: capture or verify.");
        }

        var subcommand = args[0].ToLowerInvariant();
        return subcommand switch
        {
            "capture" => RunGoldenCapture(args[1..]),
            "verify" => RunGoldenVerify(args[1..]),
            _ => Error($"Unknown golden subcommand: {subcommand}. Use 'capture' or 'verify'.")
        };
    }

    private static int RunGoldenCapture(string[] args)
    {
        string? namespaceName = null;
        string? outputPath = null;
        string? repoRoot = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--namespace" or "-n" when i + 1 < args.Length:
                    namespaceName = args[++i];
                    break;
                case "--output" or "-o" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--repo-root" or "-r" when i + 1 < args.Length:
                    repoRoot = args[++i];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return Error("Golden capture requires --namespace.");
        }

        repoRoot ??= FindRepoRoot();
        if (repoRoot is null)
        {
            return Error("Could not find repo root. Use --repo-root to specify.");
        }

        var capture = new GoldenSnapshotCapture(repoRoot);
        var manifest = capture.Capture(namespaceName);
        var manifestPath = ResolveGoldenManifestPath(outputPath, repoRoot, namespaceName);

        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), Utf8NoBom);

        Console.WriteLine("✅ Golden manifest captured.");
        Console.WriteLine($"   Namespace: {namespaceName}");
        Console.WriteLine($"   Deterministic files: {manifest.DeterministicFiles.Count}");
        Console.WriteLine($"   AI files: {manifest.AiFiles.Count}");
        Console.WriteLine($"   Output: {manifestPath}");

        return 0;
    }

    private static int RunGoldenVerify(string[] args)
    {
        string? manifestPath = null;
        string? outputDirectory = null;
        string? repoRoot = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--manifest" or "-m" when i + 1 < args.Length:
                    manifestPath = args[++i];
                    break;
                case "--output-dir" or "-o" when i + 1 < args.Length:
                    outputDirectory = args[++i];
                    break;
                case "--repo-root" or "-r" when i + 1 < args.Length:
                    repoRoot = args[++i];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(manifestPath) || string.IsNullOrWhiteSpace(outputDirectory))
        {
            return Error("Golden verify requires both --manifest and --output-dir.");
        }

        if (!File.Exists(manifestPath))
        {
            return Error($"Manifest file not found: {manifestPath}");
        }

        if (!Directory.Exists(outputDirectory))
        {
            return Error($"Output directory not found: {outputDirectory}");
        }

        repoRoot ??= FindRepoRoot() ?? Directory.GetCurrentDirectory();

        GoldenManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<GoldenManifest>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (JsonException ex)
        {
            return Error($"Invalid JSON in golden manifest '{manifestPath}': {ex.Message}");
        }

        if (manifest is null)
        {
            return Error($"Failed to parse golden manifest JSON: {manifestPath}");
        }

        var verifier = new GoldenSnapshotVerifier(repoRoot);
        var result = verifier.Verify(manifest, outputDirectory);

        Console.WriteLine("🔍 Golden verification result");
        Console.WriteLine($"   Namespace: {manifest.Namespace}");
        Console.WriteLine($"   Deterministic files: {manifest.DeterministicFiles.Count}");
        Console.WriteLine($"   AI files: {manifest.AiFiles.Count}");

        foreach (var note in result.Notes)
        {
            Console.WriteLine($"ℹ️  {note}");
        }

        if (result.Succeeded)
        {
            Console.WriteLine("✅ Golden verification passed.");
            return 0;
        }

        Console.WriteLine("❌ Golden verification failed:");
        foreach (var violation in result.Violations)
        {
            Console.WriteLine($"   - {violation}");
        }

        return 1;
    }

    private static string ResolveGoldenManifestPath(string? outputPath, string repoRoot, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Path.Combine(
                repoRoot,
                "mcp-tools",
                "DocGeneration.PipelineRunner.Tests",
                "Fixtures",
                "GoldenSnapshot",
                namespaceName,
                "golden-manifest.json");
        }

        return Directory.Exists(outputPath) || !Path.HasExtension(outputPath)
            ? Path.Combine(outputPath, "golden-manifest.json")
            : outputPath;
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
              fingerprint golden [options]      Capture or verify golden behavioral baselines

            Snapshot options:
              --namespace, -n <name>    Fingerprint a single namespace (default: all)
              --output, -o <path>       Output file path (default: ./fingerprint-baseline.json)
              --repo-root, -r <path>    Repository root (default: auto-detect)

            Diff options:
              --baseline, -b <path>     Path to baseline snapshot JSON (required)
              --candidate, -c <path>    Path to candidate snapshot JSON (required)
              --output, -o <path>       Output file for diff report (default: stdout)

            Golden capture options:
              --namespace, -n <name>    Namespace to capture (required)
              --output, -o <path>       Output manifest path or directory
              --repo-root, -r <path>    Repository root (default: auto-detect)

            Golden verify options:
              --manifest, -m <path>     Path to golden manifest JSON (required)
              --output-dir, -o <path>   Generated namespace output directory (required)
              --repo-root, -r <path>    Repository root (default: auto-detect)

            Examples:
              dotnet run -- snapshot
              dotnet run -- snapshot --namespace advisor --output advisor-snapshot.json
              dotnet run -- diff --baseline before.json --candidate after.json
              dotnet run -- diff -b before.json -c after.json -o report.md
              dotnet run -- golden capture --namespace advisor --repo-root .
              dotnet run -- golden verify --manifest path\to\golden-manifest.json --output-dir generated-advisor
            """);
    }
}
