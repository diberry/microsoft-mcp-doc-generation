using System.Text.Json;
using Shared;

namespace PipelineRunner.Services;

public sealed class CliMetadataLoader : ICliMetadataLoader
{
    private readonly Dictionary<string, CliMetadataSnapshot> _cliOutputCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _versionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _namespaceCache = new(StringComparer.OrdinalIgnoreCase);

    public bool CliOutputExists(string outputPath)
        => File.Exists(GetCliOutputPath(outputPath));

    public bool CliVersionExists(string outputPath)
        => File.Exists(GetCliVersionPath(outputPath));

    public bool NamespaceMetadataExists(string outputPath)
        => File.Exists(GetNamespacePath(outputPath));

    public async ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken cancellationToken)
    {
        var normalizedOutputPath = NormalizeOutputPath(outputPath);
        if (_cliOutputCache.TryGetValue(normalizedOutputPath, out var cached))
        {
            return cached;
        }

        var cliOutputPath = GetCliOutputPath(normalizedOutputPath);
        if (!File.Exists(cliOutputPath))
        {
            throw new FileNotFoundException("CLI output metadata file was not found.", cliOutputPath);
        }

        await using var stream = File.OpenRead(cliOutputPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var rawRoot = document.RootElement.Clone();
        var tools = rawRoot.GetProperty("results")
            .EnumerateArray()
            .Select(tool => new CliTool(
                tool.GetProperty("command").GetString() ?? string.Empty,
                tool.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() ?? string.Empty : string.Empty,
                tool.TryGetProperty("description", out var descriptionProperty) ? descriptionProperty.GetString() : null,
                tool.Clone()))
            .ToArray();

        var snapshot = new CliMetadataSnapshot(cliOutputPath, rawRoot, tools);
        _cliOutputCache[normalizedOutputPath] = snapshot;
        return snapshot;
    }

    public async ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken cancellationToken)
    {
        var normalizedOutputPath = NormalizeOutputPath(outputPath);
        if (_versionCache.TryGetValue(normalizedOutputPath, out var cached))
        {
            return cached;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var version = await CliVersionReader.ReadCliVersionAsync(normalizedOutputPath);
        _versionCache[normalizedOutputPath] = version;
        return version;
    }

    public async ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken cancellationToken)
    {
        var normalizedOutputPath = NormalizeOutputPath(outputPath);
        if (_namespaceCache.TryGetValue(normalizedOutputPath, out var cached))
        {
            return cached;
        }

        var namespacePath = GetNamespacePath(normalizedOutputPath);
        if (!File.Exists(namespacePath))
        {
            throw new FileNotFoundException("CLI namespace metadata file was not found.", namespacePath);
        }

        await using var stream = File.OpenRead(namespacePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var namespaces = document.RootElement.GetProperty("results")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("name").GetString())
            .OfType<string>()
            .ToArray();

        _namespaceCache[normalizedOutputPath] = namespaces;
        return namespaces;
    }

    private static string NormalizeOutputPath(string outputPath)
        => Path.GetFullPath(outputPath);

    private static string GetCliOutputPath(string outputPath)
        => Path.Combine(NormalizeOutputPath(outputPath), "cli", "cli-output.json");

    private static string GetCliVersionPath(string outputPath)
        => Path.Combine(NormalizeOutputPath(outputPath), "cli", "cli-version.json");

    private static string GetNamespacePath(string outputPath)
        => Path.Combine(NormalizeOutputPath(outputPath), "cli", "cli-namespace.json");
}
