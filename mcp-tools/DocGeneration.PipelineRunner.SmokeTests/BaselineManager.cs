// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

namespace DocGeneration.PipelineRunner.SmokeTests;

/// <summary>
/// Manages baseline fixtures for smoke tests. Handles storage, comparison, and updating
/// of golden files that represent expected pipeline output.
/// </summary>
public static class BaselineManager
{
    private const string BaselinesDirectory = "Baselines";
    private const string ManifestFileName = "baseline-manifest.json";

    /// <summary>
    /// Captures current generated output as baseline fixtures for the given namespace.
    /// </summary>
    /// <param name="testProjectRoot">Root directory of the test project</param>
    /// <param name="repoRoot">Root directory of the repository</param>
    /// <param name="namespaceName">Azure namespace name (e.g., "quota", "redis")</param>
    public static void CaptureBaseline(string testProjectRoot, string repoRoot, string namespaceName)
    {
        var generatedDir = Path.Combine(repoRoot, $"generated-{namespaceName}");
        if (!Directory.Exists(generatedDir))
        {
            throw new InvalidOperationException(
                $"Generated output not found for namespace '{namespaceName}'. " +
                $"Run './start.sh {namespaceName}' first to generate output.");
        }

        var baselineDir = Path.Combine(testProjectRoot, BaselinesDirectory, namespaceName);
        
        // Clean existing baseline
        if (Directory.Exists(baselineDir))
        {
            Directory.Delete(baselineDir, recursive: true);
        }

        // Copy key output directories
        CopyDirectory(
            Path.Combine(generatedDir, "annotations"),
            Path.Combine(baselineDir, "annotations"));
        
        CopyDirectory(
            Path.Combine(generatedDir, "parameters"),
            Path.Combine(baselineDir, "parameters"));
        
        CopyDirectory(
            Path.Combine(generatedDir, "tools"),
            Path.Combine(baselineDir, "tools"));
        
        CopyDirectory(
            Path.Combine(generatedDir, "tool-family"),
            Path.Combine(baselineDir, "tool-family"));

        // Update manifest
        UpdateManifest(testProjectRoot, namespaceName, baselineDir);
        
        Console.WriteLine($"✓ Captured baseline for namespace '{namespaceName}' to {baselineDir}");
    }

    /// <summary>
    /// Compares generated output against baseline fixtures and returns validation results.
    /// </summary>
    /// <param name="testProjectRoot">Root directory of the test project</param>
    /// <param name="repoRoot">Root directory of the repository</param>
    /// <param name="namespaceName">Azure namespace name</param>
    /// <returns>Validation result with list of differences</returns>
    public static BaselineComparisonResult CompareWithBaseline(
        string testProjectRoot,
        string repoRoot,
        string namespaceName)
    {
        var result = new BaselineComparisonResult { NamespaceName = namespaceName };
        
        var baselineDir = Path.Combine(testProjectRoot, BaselinesDirectory, namespaceName);
        if (!Directory.Exists(baselineDir))
        {
            result.AddIssue("Baseline directory not found. Run with BASELINE_UPDATE=true to create baselines.");
            return result;
        }

        var generatedDir = Path.Combine(repoRoot, $"generated-{namespaceName}");
        if (!Directory.Exists(generatedDir))
        {
            result.AddIssue($"Generated output not found: {generatedDir}");
            return result;
        }

        // Compare each baseline directory
        CompareDirectory(baselineDir, "annotations", generatedDir, result);
        CompareDirectory(baselineDir, "parameters", generatedDir, result);
        CompareDirectory(baselineDir, "tools", generatedDir, result);
        CompareDirectory(baselineDir, "tool-family", generatedDir, result);

        return result;
    }

    /// <summary>
    /// Checks if baselines exist for the given namespace.
    /// </summary>
    public static bool BaselinesExist(string testProjectRoot, string namespaceName)
    {
        var baselineDir = Path.Combine(testProjectRoot, BaselinesDirectory, namespaceName);
        return Directory.Exists(baselineDir);
    }

    private static void CompareDirectory(
        string baselineRoot,
        string subdirName,
        string generatedRoot,
        BaselineComparisonResult result)
    {
        var baselineDir = Path.Combine(baselineRoot, subdirName);
        var generatedDir = Path.Combine(generatedRoot, subdirName);

        if (!Directory.Exists(baselineDir))
        {
            // Baseline subdir doesn't exist - skip (optional directory)
            return;
        }

        if (!Directory.Exists(generatedDir))
        {
            result.AddIssue($"Missing output directory: {subdirName}");
            return;
        }

        // Get all files in both directories
        var baselineFiles = Directory.GetFiles(baselineDir, "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(f => f)
            .ToHashSet();
        
        var generatedFiles = Directory.GetFiles(generatedDir, "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(f => f)
            .ToHashSet();

        // Check for missing files
        var missingFiles = baselineFiles.Except(generatedFiles).ToList();
        foreach (var file in missingFiles)
        {
            result.AddIssue($"Missing file in {subdirName}: {file}");
        }

        // Check for extra files
        var extraFiles = generatedFiles.Except(baselineFiles).ToList();
        foreach (var file in extraFiles)
        {
            result.AddIssue($"Extra file in {subdirName}: {file}");
        }

        // Compare content of common files
        var commonFiles = baselineFiles.Intersect(generatedFiles);
        foreach (var fileName in commonFiles)
        {
            var baselinePath = Path.Combine(baselineDir, fileName!);
            var generatedPath = Path.Combine(generatedDir, fileName!);

            var baselineContent = File.ReadAllText(baselinePath).Trim();
            var generatedContent = File.ReadAllText(generatedPath).Trim();

            if (baselineContent != generatedContent)
            {
                result.AddIssue($"Content mismatch in {subdirName}/{fileName}");
                result.AddDiff(subdirName, fileName!, baselineContent, generatedContent);
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            // Source doesn't exist - skip (optional directory)
            return;
        }

        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*.md", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(destDir, fileName), overwrite: true);
        }
    }

    private static void UpdateManifest(string testProjectRoot, string namespaceName, string baselineDir)
    {
        var manifestPath = Path.Combine(testProjectRoot, BaselinesDirectory, ManifestFileName);
        
        var manifest = new Dictionary<string, BaselineManifestEntry>();
        if (File.Exists(manifestPath))
        {
            var json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<Dictionary<string, BaselineManifestEntry>>(json)
                ?? new Dictionary<string, BaselineManifestEntry>();
        }

        // Count files in baseline
        var fileCount = Directory.GetFiles(baselineDir, "*.md", SearchOption.AllDirectories).Length;

        manifest[namespaceName] = new BaselineManifestEntry
        {
            CapturedAt = DateTime.UtcNow,
            FileCount = fileCount,
            Path = $"{BaselinesDirectory}/{namespaceName}"
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, options));
    }

    private record BaselineManifestEntry
    {
        public DateTime CapturedAt { get; init; }
        public int FileCount { get; init; }
        public string Path { get; init; } = string.Empty;
    }
}

/// <summary>
/// Results of comparing generated output against baseline fixtures.
/// </summary>
public class BaselineComparisonResult
{
    private readonly List<string> _issues = new();
    private readonly List<FileDiff> _diffs = new();

    public string NamespaceName { get; init; } = string.Empty;
    public IReadOnlyList<string> Issues => _issues;
    public IReadOnlyList<FileDiff> Diffs => _diffs;
    public bool Success => _issues.Count == 0;

    public void AddIssue(string issue) => _issues.Add(issue);

    public void AddDiff(string directory, string fileName, string baseline, string generated)
    {
        _diffs.Add(new FileDiff
        {
            Directory = directory,
            FileName = fileName,
            BaselineContent = baseline,
            GeneratedContent = generated
        });
    }

    public string GetSummary()
    {
        if (Success)
        {
            return $"✓ All files match baseline for namespace '{NamespaceName}'";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"✗ Baseline comparison failed for namespace '{NamespaceName}':");
        sb.AppendLine();
        
        foreach (var issue in Issues)
        {
            sb.AppendLine($"  - {issue}");
        }

        if (Diffs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Content differences:");
            foreach (var diff in Diffs)
            {
                sb.AppendLine($"  File: {diff.Directory}/{diff.FileName}");
                sb.AppendLine($"    Baseline: {diff.BaselineContent.Length} chars");
                sb.AppendLine($"    Generated: {diff.GeneratedContent.Length} chars");
                
                // Show first difference
                var baselineLines = diff.BaselineContent.Split('\n');
                var generatedLines = diff.GeneratedContent.Split('\n');
                for (int i = 0; i < Math.Min(baselineLines.Length, generatedLines.Length); i++)
                {
                    if (baselineLines[i] != generatedLines[i])
                    {
                        sb.AppendLine($"    First diff at line {i + 1}:");
                        sb.AppendLine($"      Baseline:  {TruncateString(baselineLines[i], 80)}");
                        sb.AppendLine($"      Generated: {TruncateString(generatedLines[i], 80)}");
                        break;
                    }
                }
            }
        }

        return sb.ToString();
    }

    private static string TruncateString(string str, int maxLength)
    {
        if (str.Length <= maxLength)
            return str;
        return str.Substring(0, maxLength - 3) + "...";
    }
}

public record FileDiff
{
    public string Directory { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string BaselineContent { get; init; } = string.Empty;
    public string GeneratedContent { get; init; } = string.Empty;
}
