// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;

namespace DocGeneration.Tools.PostProcessVerifier;

/// <summary>
/// Applies the deterministic post-processing chain to existing tool-family files
/// and reports what changed — without modifying originals.
/// Usage:
///   dotnet run                          # process all namespaces
///   dotnet run -- --namespace deploy    # process one namespace
/// </summary>
internal static class Program
{
    // Post-processor descriptors: name + delegate, in pipeline order (stages 4-13).
    private static readonly (string Name, Func<string, string> Apply)[] Processors =
    [
        ("AcronymExpander",        AcronymExpander.ExpandAll),
        ("FrontmatterEnricher",    FrontmatterEnricher.Enrich),
        ("DuplicateExampleStripper", DuplicateExampleStripper.Strip),
        ("AnnotationSpaceFixer",   AnnotationSpaceFixer.Fix),
        ("PresentTenseFixer",      PresentTenseFixer.Fix),
        ("ContractionFixer",       ContractionFixer.Fix),
        ("IntroductoryCommaFixer", IntroductoryCommaFixer.Fix),
        ("ExampleValueBackticker", ExampleValueBackticker.Fix),
        ("LearnUrlRelativizer",    LearnUrlRelativizer.Relativize),
        ("JsonSchemaCollapser",    JsonSchemaCollapser.Collapse),
    ];

    static int Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            Console.Error.WriteLine("ERROR: Could not find repository root (looked for generated-validated-* dirs).");
            return 1;
        }

        // Parse --namespace filter
        string? nsFilter = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--namespace" or "-n")
            {
                nsFilter = args[i + 1];
                break;
            }
        }

        var namespaceDirs = Directory.GetDirectories(repoRoot, "generated-validated-*")
            .OrderBy(d => d)
            .ToList();

        if (namespaceDirs.Count == 0)
        {
            Console.Error.WriteLine("ERROR: No generated-validated-* directories found.");
            return 1;
        }

        Console.WriteLine("=== Post-Processing Verification Report ===");
        Console.WriteLine();

        int totalFiles = 0;
        int changedFiles = 0;

        foreach (var nsDir in namespaceDirs)
        {
            var nsName = Path.GetFileName(nsDir).Replace("generated-validated-", "");

            if (nsFilter is not null && !nsName.Equals(nsFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var toolFamilyDir = Path.Combine(nsDir, "tool-family");
            if (!Directory.Exists(toolFamilyDir))
                continue;

            var mdFiles = Directory.GetFiles(toolFamilyDir, "*.md")
                .Where(f => !f.EndsWith(".after", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToList();

            if (mdFiles.Count == 0)
                continue;

            foreach (var mdFile in mdFiles)
            {
                totalFiles++;
                ProcessFile(mdFile, nsName, ref changedFiles);
            }
        }

        Console.WriteLine("─────────────────────────────────────────");
        Console.WriteLine($"Total files processed: {totalFiles}");
        Console.WriteLine($"Files with changes:    {changedFiles}");
        Console.WriteLine($"Files unchanged:       {totalFiles - changedFiles}");

        return 0;
    }

    private static void ProcessFile(string filePath, string nsName, ref int changedFiles)
    {
        var original = File.ReadAllText(filePath);
        var originalLines = CountLines(original);
        var originalChars = original.Length;

        Console.WriteLine($"Namespace: {nsName}");
        Console.WriteLine($"  File: {Path.GetFileName(filePath)}");
        Console.WriteLine($"  Original: {originalLines} lines, {originalChars:N0} chars");

        var current = original;
        var changes = new List<string>();
        bool anyChange = false;

        foreach (var (name, apply) in Processors)
        {
            var before = current;
            current = apply(current);

            if (!string.Equals(before, current, StringComparison.Ordinal))
            {
                anyChange = true;
                var detail = DescribeChange(name, before, current);
                changes.Add(detail);
            }
        }

        var afterLines = CountLines(current);
        var afterChars = current.Length;

        Console.WriteLine($"  After:    {afterLines} lines, {afterChars:N0} chars");

        if (anyChange)
        {
            changedFiles++;

            Console.WriteLine("  Changes:");
            foreach (var c in changes)
                Console.WriteLine($"    - {c}");

            // Write .after file (never overwrite original)
            var afterPath = filePath + ".after";
            File.WriteAllText(afterPath, current);
            Console.WriteLine($"  Written:  {Path.GetFileName(afterPath)}");
        }
        else
        {
            Console.WriteLine("  Changes:  (none — file already fully post-processed)");
        }

        Console.WriteLine($"  Status: ✅ All post-processors applied successfully");
        Console.WriteLine();
    }

    private static string DescribeChange(string processorName, string before, string after)
    {
        var linesBefore = CountLines(before);
        var linesAfter = CountLines(after);
        var lineDiff = linesAfter - linesBefore;
        var charDiff = after.Length - before.Length;

        // Build a human-readable summary
        var parts = new List<string>();

        if (lineDiff != 0)
            parts.Add($"{(lineDiff > 0 ? "+" : "")}{lineDiff} lines");

        if (charDiff != 0)
            parts.Add($"{(charDiff > 0 ? "+" : "")}{charDiff} chars");

        // Count concrete replacements by diffing lines
        var replacements = CountReplacements(before, after);
        if (replacements > 0 && lineDiff == 0)
            parts.Add($"{replacements} replacement{(replacements == 1 ? "" : "s")}");

        var summary = parts.Count > 0 ? string.Join(", ", parts) : "modified";

        // Add processor-specific context
        var context = processorName switch
        {
            "JsonSchemaCollapser" when lineDiff < 0 => " (JSON schema collapsed)",
            "AcronymExpander" => " (acronym expansions)",
            "FrontmatterEnricher" => " (frontmatter fields added)",
            "DuplicateExampleStripper" => " (duplicate examples removed)",
            "AnnotationSpaceFixer" => " (annotation spacing fixed)",
            "PresentTenseFixer" => " (future→present tense)",
            "ContractionFixer" => " (contractions applied)",
            "IntroductoryCommaFixer" => " (introductory commas added)",
            "ExampleValueBackticker" => " (values backticked)",
            "LearnUrlRelativizer" => " (URLs relativized)",
            "JsonSchemaCollapser" => " (JSON schema processing)",
            _ => ""
        };

        return $"{processorName}: {summary}{context}";
    }

    private static int CountReplacements(string before, string after)
    {
        var beforeLines = before.Split('\n');
        var afterLines = after.Split('\n');
        int count = 0;
        int limit = Math.Min(beforeLines.Length, afterLines.Length);
        for (int i = 0; i < limit; i++)
        {
            if (beforeLines[i] != afterLines[i])
                count++;
        }
        // Lines only in one version also count
        count += Math.Abs(beforeLines.Length - afterLines.Length);
        return count;
    }

    private static int CountLines(string text) =>
        text.Length == 0 ? 0 : text.Split('\n').Length;

    private static string? FindRepoRoot()
    {
        // Walk up from the executable looking for generated-validated-* dirs
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            if (Directory.GetDirectories(dir, "generated-validated-*").Length > 0)
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        // Fallback: try CWD
        var cwd = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10; i++)
        {
            if (Directory.GetDirectories(cwd, "generated-validated-*").Length > 0)
                return cwd;
            var parent = Directory.GetParent(cwd);
            if (parent is null) break;
            cwd = parent.FullName;
        }

        return null;
    }
}
