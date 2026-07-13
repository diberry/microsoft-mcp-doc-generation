using System.Text.Json;
using System.Text.RegularExpressions;
using PipelineRunner.Context;
using PipelineRunner.Contracts;

namespace PipelineRunner.Validation;

public static partial class SourceVersionVerificationGate
{
    public const string Name = "SourceVersionVerificationGate";

    public static async ValueTask<ValidatorResult> ValidateAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        var configuredVersion = await ReadConfiguredVersionAsync(context.RepoRoot, cancellationToken);
        var cliVersion = NormalizeVersion(context.CliVersion);
        var sourceFolderVersion = ExtractVersionFromSourcePath(context.CliOutput?.FilePath);
        var cliOutputVersion = ExtractVersionFromCliOutput(context.CliOutput?.RawRoot);

        if (!string.IsNullOrWhiteSpace(configuredVersion)
            && !string.IsNullOrWhiteSpace(cliVersion)
            && !string.Equals(configuredVersion, cliVersion, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Blocking: Version verification failed: configured target version '{configuredVersion}' does not match CLI metadata version '{cliVersion}'.");
        }

        if (!string.IsNullOrWhiteSpace(configuredVersion)
            && !string.IsNullOrWhiteSpace(sourceFolderVersion)
            && !string.Equals(configuredVersion, sourceFolderVersion, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Blocking: Version verification failed: configured target version '{configuredVersion}' does not match source metadata folder version '{sourceFolderVersion}'.");
        }

        if (!string.IsNullOrWhiteSpace(cliVersion)
            && !string.IsNullOrWhiteSpace(cliOutputVersion)
            && !string.Equals(cliVersion, cliOutputVersion, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Blocking: Version verification failed: CLI version file reports '{cliVersion}' but CLI output JSON reports '{cliOutputVersion}'.");
        }

        return new ValidatorResult(Name, issues.Count == 0, issues);
    }

    public static string ResolveSourceVersion(PipelineContext context)
    {
        var sourceFolderVersion = ExtractVersionFromSourcePath(context.CliOutput?.FilePath);
        if (!string.IsNullOrWhiteSpace(sourceFolderVersion))
        {
            return sourceFolderVersion;
        }

        var cliOutputVersion = ExtractVersionFromCliOutput(context.CliOutput?.RawRoot);
        if (!string.IsNullOrWhiteSpace(cliOutputVersion))
        {
            return cliOutputVersion;
        }

        return NormalizeVersion(context.CliVersion) ?? "unknown";
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
        var versionPath = Path.Combine(repoRoot, "mcp-tool-version.txt");
        if (!File.Exists(versionPath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(versionPath, cancellationToken);
        return NormalizeVersion(content);
    }

    public static string? ExtractVersionFromCliOutput(JsonElement? rawRoot)
    {
        if (rawRoot is null || rawRoot.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!rawRoot.Value.TryGetProperty("version", out var versionProperty))
        {
            return null;
        }

        return versionProperty.ValueKind == JsonValueKind.String
            ? NormalizeVersion(versionProperty.GetString())
            : null;
    }

    private static string? NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        return version.Trim();
    }

    [GeneratedRegex(@"^(?<version>\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?)(?:\+[A-Za-z0-9]+)?$")]
    private static partial Regex VersionFolderRegex();
}
