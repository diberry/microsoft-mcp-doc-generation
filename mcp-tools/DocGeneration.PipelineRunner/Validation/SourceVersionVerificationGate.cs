using System.Text.Json;
using System.Text.RegularExpressions;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using Shared;
using ToolFamilyCleanup.Services;

namespace PipelineRunner.Validation;

public static partial class SourceVersionVerificationGate
{
    public const string Name = "SourceVersionVerificationGate";

    public static async ValueTask<ValidatorResult> ValidateAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        var configuredVersion = await ReadConfiguredVersionAsync(context.RepoRoot, cancellationToken);
        if (string.IsNullOrWhiteSpace(configuredVersion))
        {
            return new ValidatorResult(Name, true, issues);
        }

        var cliVersion = SemverVersionNormalizer.StripBuildMetadata(context.CliVersion);
        var sourceResolution = await ResolveSourceSnapshotAsync(context, configuredVersion, cancellationToken);
        CliMetadataSnapshot? sourceSnapshot = null;
        string? sourceFolderVersion = null;
        if (!sourceResolution.Success)
        {
            issues.Add($"Source version check failed: {sourceResolution.Error}");
        }
        else
        {
            sourceSnapshot = sourceResolution.Snapshot!;
            context.SourceCliOutput = sourceSnapshot;
            sourceFolderVersion = ExtractVersionFromSourcePath(sourceSnapshot.FilePath);
        }

        var cliOutputVersion = ExtractVersionFromCliOutput(context.CliOutput?.RawRoot);
        var sourceJsonVersion = ExtractVersionFromCliOutput(sourceSnapshot?.RawRoot);

        if (!string.IsNullOrWhiteSpace(configuredVersion)
            && !string.IsNullOrWhiteSpace(cliVersion)
            && !string.Equals(configuredVersion, cliVersion, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Version check failed: configured target version '{configuredVersion}' != CLI version file '{cliVersion}'.");
        }

        if (!string.IsNullOrWhiteSpace(configuredVersion)
            && !string.IsNullOrWhiteSpace(sourceFolderVersion)
            && !string.Equals(configuredVersion, sourceFolderVersion, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Source folder version check failed: configured target version '{configuredVersion}' != source metadata folder version '{sourceFolderVersion}'.");
        }

        if (!string.IsNullOrWhiteSpace(cliVersion)
            && !string.IsNullOrWhiteSpace(cliOutputVersion)
            && !string.Equals(cliVersion, cliOutputVersion, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"CLI output version check failed: CLI version file '{cliVersion}' != CLI output JSON version '{cliOutputVersion}'.");
        }

        if (!string.IsNullOrWhiteSpace(sourceFolderVersion)
            && !string.IsNullOrWhiteSpace(sourceJsonVersion)
            && !string.Equals(sourceFolderVersion, sourceJsonVersion, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Source JSON version check failed: source metadata folder version '{sourceFolderVersion}' != source CLI JSON version '{sourceJsonVersion}'.");
        }

        return new ValidatorResult(Name, issues.Count == 0, issues);
    }

    public static string ResolveSourceVersion(PipelineContext context)
    {
        var sourceFolderVersion = ExtractVersionFromSourcePath(context.SourceCliOutput?.FilePath);
        if (!string.IsNullOrWhiteSpace(sourceFolderVersion))
        {
            return sourceFolderVersion;
        }

        var cliOutputVersion = ExtractVersionFromCliOutput(context.SourceCliOutput?.RawRoot);
        if (!string.IsNullOrWhiteSpace(cliOutputVersion))
        {
            return cliOutputVersion;
        }

        return "unknown";
    }

    public static string? ExtractVersionFromSourcePath(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        var directoryPath = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return null;
        }

        var directory = new DirectoryInfo(directoryPath);
        while (directory is not null)
        {
            var match = VersionFolderRegex().Match(directory.Name);
            if (match.Success)
            {
                return match.Groups["version"].Value;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static async ValueTask<string?> ReadConfiguredVersionAsync(string repoRoot, CancellationToken cancellationToken)
    {
        var versionPath = GetConfiguredVersionPath(repoRoot);
        if (!File.Exists(versionPath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(versionPath, cancellationToken);
        return SemverVersionNormalizer.StripBuildMetadata(content);
    }

    public static string? ResolveConfiguredVersion(string repoRoot)
    {
        var versionPath = GetConfiguredVersionPath(repoRoot);
        return File.Exists(versionPath)
            ? SemverVersionNormalizer.StripBuildMetadata(File.ReadAllText(versionPath))
            : null;
    }

    private static string GetConfiguredVersionPath(string repoRoot)
        => Path.Combine(repoRoot, MetadataConstants.McpToolVersionFileName);

    public static string? ExtractVersionFromCliOutput(JsonElement? rawRoot)
    {
        if (rawRoot is null || rawRoot.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!rawRoot.Value.TryGetProperty(MetadataConstants.VersionPropertyName, out var versionProperty))
        {
            return null;
        }

        return versionProperty.ValueKind == JsonValueKind.String
            ? SemverVersionNormalizer.StripBuildMetadata(versionProperty.GetString())
            : null;
    }

    private static async ValueTask<SourceCliMetadataResolution> ResolveSourceSnapshotAsync(
        PipelineContext context,
        string? configuredVersion,
        CancellationToken cancellationToken)
    {
        var currentPathVersion = ExtractVersionFromSourcePath(context.CliOutput?.FilePath);
        if (!string.IsNullOrWhiteSpace(currentPathVersion)
            && context.CliOutput is not null
            && IsSourceMetadataPath(context.CliOutput.FilePath))
        {
            return SourceCliMetadataResolution.FromSnapshot(context.CliOutput);
        }

        return await SourceCliMetadataResolver.ResolveAsync(context.RepoRoot, configuredVersion, cancellationToken);
    }

    private static bool IsSourceMetadataPath(string sourcePath)
        => sourcePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => string.Equals(segment, MetadataConstants.McpCliMetadataDirectoryName, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"^(?<version>\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?)(?:\+[A-Za-z0-9]+)?$")]
    private static partial Regex VersionFolderRegex();
}
