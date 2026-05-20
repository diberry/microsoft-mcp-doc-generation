// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Validation;

/// <summary>
/// Post-assembly gate that confirms output files in <c>tool-family/</c> match
/// the composition declarations in <c>brand-to-server-mapping.json</c>.
///
/// Composition rules:
///   standalone — exactly one tool-family file with the expected FileName.
///   split      — each namespace produces its own file (same per-file check as standalone).
///   merge      — all group members produce intermediate files; merged output keyed
///                by <c>mergeGroup</c> must exist.
///
/// Complementary to <see cref="ToolFamilyPostAssemblyValidator"/> (which validates
/// article content and tool counts). This validator checks file-level composition.
/// </summary>
public sealed class CompositionOutputValidator : IPostValidator
{
    /// <summary>Context key for injecting brand mapping entries in tests.</summary>
    internal const string BrandEntriesContextKey = "CompositionOutputValidator.BrandEntries";

    public string Name => "CompositionOutputValidator";

    /// <summary>
    /// Per-namespace pipeline validator. Validates that the current namespace produced
    /// the tool-family file declared in its brand mapping.
    /// </summary>
    public async ValueTask<ValidatorResult> ValidateAsync(
        PipelineContext context,
        IPipelineStep step,
        CancellationToken cancellationToken)
    {
        var familyName = ResolveFamilyName(context);
        var outputFileName = ResolveOutputFileName(context, familyName);

        if (string.IsNullOrWhiteSpace(outputFileName))
            return new ValidatorResult(Name, true, []);

        var toolFamilyDir = Path.Combine(context.OutputPath, "tool-family");
        var warnings = new List<string>();

        // Load brand mapping entries to determine expected composition behaviour.
        var entries = await LoadEntriesAsync(context, cancellationToken);
        var entry = FindEntry(entries, familyName);

        // Derive expected filename: prefer brand mapping, fall back to outputFileName from context.
        var expectedFileName = !string.IsNullOrEmpty(entry?.FileName) ? entry.FileName : outputFileName;
        var composition = entry?.Composition?.ToLowerInvariant() ?? "standalone";

        switch (composition)
        {
            case "standalone":
            case "split":
            case "merge":
                // All three types require the per-namespace output file to exist.
                if (!File.Exists(Path.Combine(toolFamilyDir, $"{expectedFileName}.md")))
                {
                    warnings.Add(
                        $"CompositionOutputValidator [{composition}]: expected file not found: " +
                        $"tool-family/{expectedFileName}.md (namespace: {familyName})");
                }
                break;
        }

        return new ValidatorResult(Name, warnings.Count == 0, warnings);
    }

    // ── Static validation ──────────────────────────────────────────────────────

    /// <summary>
    /// Validates all composition entries against the output directory.
    /// Use this for post-run checks, standalone CLI commands, and unit tests.
    /// </summary>
    /// <param name="outputPath">Root output path containing the <c>tool-family/</c> subdirectory.</param>
    /// <param name="entries">Brand mapping entries from <c>brand-to-server-mapping.json</c>.</param>
    /// <returns>A result object describing any missing or mismatched files.</returns>
    public static CompositionValidationResult Validate(
        string outputPath,
        IReadOnlyList<BrandMappingEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(entries);

        if (!Directory.Exists(outputPath))
        {
            return new CompositionValidationResult(
                false,
                [new CompositionIssue("directory", "N/A", $"Output directory does not exist: {outputPath}")]);
        }

        var toolFamilyDir = Path.Combine(outputPath, "tool-family");
        var issues = new List<CompositionIssue>();

        // ── Standalone ────────────────────────────────────────────────────────
        foreach (var entry in entries.Where(IsStandalone))
        {
            var file = Path.Combine(toolFamilyDir, $"{entry.FileName}.md");
            if (!File.Exists(file))
            {
                issues.Add(new CompositionIssue(
                    entry.McpServerName, "standalone",
                    $"Expected file not found: tool-family/{entry.FileName}.md"));
            }
        }

        // ── Split ─────────────────────────────────────────────────────────────
        foreach (var entry in entries.Where(IsSplit))
        {
            var file = Path.Combine(toolFamilyDir, $"{entry.FileName}.md");
            if (!File.Exists(file))
            {
                issues.Add(new CompositionIssue(
                    entry.McpServerName, "split",
                    $"Expected split file not found: tool-family/{entry.FileName}.md"));
            }
        }

        // ── Merge groups ──────────────────────────────────────────────────────
        var mergeGroups = entries
            .Where(e => IsMerge(e) && !string.IsNullOrEmpty(e.MergeGroup))
            .GroupBy(e => e.MergeGroup!, StringComparer.OrdinalIgnoreCase);

        foreach (var group in mergeGroups)
        {
            // 1. Each group member must have produced its intermediate file.
            foreach (var member in group)
            {
                var memberFile = Path.Combine(toolFamilyDir, $"{member.FileName}.md");
                if (!File.Exists(memberFile))
                {
                    issues.Add(new CompositionIssue(
                        member.McpServerName, "merge-intermediate",
                        $"Merge group '{group.Key}': intermediate file not found: " +
                        $"tool-family/{member.FileName}.md"));
                }
            }

            // 2. Merged output (keyed by mergeGroup name) must exist.
            var mergedFile = Path.Combine(toolFamilyDir, $"{group.Key}.md");
            if (!File.Exists(mergedFile))
            {
                issues.Add(new CompositionIssue(
                    group.Key, "merge-output",
                    $"Merge group '{group.Key}': merged output not found: " +
                    $"tool-family/{group.Key}.md"));
            }
        }

        return new CompositionValidationResult(issues.Count == 0, issues);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static bool IsStandalone(BrandMappingEntry e)
        => string.IsNullOrEmpty(e.Composition)
           || string.Equals(e.Composition, "standalone", StringComparison.OrdinalIgnoreCase);

    private static bool IsSplit(BrandMappingEntry e)
        => string.Equals(e.Composition, "split", StringComparison.OrdinalIgnoreCase);

    private static bool IsMerge(BrandMappingEntry e)
        => string.Equals(e.Composition, "merge", StringComparison.OrdinalIgnoreCase);

    private static string ResolveFamilyName(PipelineContext context)
    {
        if (context.Items.TryGetValue(ToolFamilyPostAssemblyValidator.FamilyNameContextKey, out var v)
            && v is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        if (context.Items.TryGetValue("Namespace", out v) && v is string ns && !string.IsNullOrWhiteSpace(ns))
        {
            return ns.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        }

        return string.Empty;
    }

    private static string ResolveOutputFileName(PipelineContext context, string familyName)
    {
        if (context.Items.TryGetValue(ToolFamilyPostAssemblyValidator.OutputFileNameContextKey, out var v)
            && v is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        return familyName;
    }

    private static BrandMappingEntry? FindEntry(IReadOnlyList<BrandMappingEntry> entries, string familyName)
        => entries.FirstOrDefault(e =>
            string.Equals(e.McpServerName, familyName, StringComparison.OrdinalIgnoreCase));

    private static async Task<IReadOnlyList<BrandMappingEntry>> LoadEntriesAsync(
        PipelineContext context, CancellationToken cancellationToken)
    {
        // Allow test injection via context items.
        if (context.Items.TryGetValue(BrandEntriesContextKey, out var v)
            && v is IReadOnlyList<BrandMappingEntry> injected)
        {
            return injected;
        }

        var loader = new BrandMappingLoader();
        return await loader.LoadAsync(context.McpToolsRoot, cancellationToken);
    }
}

// ── Result types ───────────────────────────────────────────────────────────────

/// <summary>
/// An issue found during composition output validation.
/// </summary>
/// <param name="Identifier">McpServerName or mergeGroup key that has the issue.</param>
/// <param name="IssueType">One of: standalone, split, merge-intermediate, merge-output.</param>
/// <param name="Message">Human-readable description of what is missing or wrong.</param>
public sealed record CompositionIssue(string Identifier, string IssueType, string Message);

/// <summary>
/// Result of a full-composition validation run via <see cref="CompositionOutputValidator.Validate"/>.
/// </summary>
/// <param name="Success"><c>true</c> if all expected files are present.</param>
/// <param name="Issues">One entry per missing or mismatched file.</param>
public sealed record CompositionValidationResult(bool Success, IReadOnlyList<CompositionIssue> Issues);
