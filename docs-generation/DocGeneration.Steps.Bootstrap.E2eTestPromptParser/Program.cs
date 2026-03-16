using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using E2eTestPromptParser;
using E2eTestPromptParser.Models;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static async Task<int> Main(string[] args)
    {
        var format = "json";
        string? outputFile = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--format" && i + 1 < args.Length)
            {
                format = args[++i].ToLowerInvariant();
            }
            else if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                outputFile = args[i];
            }
        }

        // Load config
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"Error: config.json not found at {configPath}");
            return 1;
        }

        var configJson = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializer.Deserialize<ParserConfig>(configJson);
        if (config is null || string.IsNullOrWhiteSpace(config.RemoteUrl))
        {
            Console.Error.WriteLine("Error: config.json is missing or has empty remoteUrl");
            return 1;
        }

        // Download remote file
        Console.WriteLine($"Downloading: {config.RemoteUrl}");
        var localPath = Path.Combine(AppContext.BaseDirectory, config.LocalFileName);

        using var httpClient = new HttpClient();
        try
        {
            var markdown = await httpClient.GetStringAsync(config.RemoteUrl);
            await File.WriteAllTextAsync(localPath, markdown);
            Console.WriteLine($"Saved to:    {localPath}");
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Error downloading file: {ex.Message}");
            return 1;
        }

        // Parse
        var doc = TestPromptMarkdownParser.ParseFile(localPath);

        var output = format switch
        {
            "summary" => FormatSummary(doc),
            _ => FormatJson(doc)
        };

        if (outputFile is not null)
        {
            var fullOutputPath = Path.GetFullPath(outputFile);
            var outputDir = Path.GetDirectoryName(fullOutputPath);
            if (outputDir is not null)
            {
                Directory.CreateDirectory(outputDir);
            }
            await File.WriteAllTextAsync(fullOutputPath, output);
            Console.WriteLine($"✅ Written to {fullOutputPath}");
        }
        else
        {
            Console.Write(output);
        }

        Console.Error.WriteLine($"   Sections: {doc.Sections.Count}");
        Console.Error.WriteLine($"   Tools:    {doc.ToolNames.Count}");
        Console.Error.WriteLine($"   Prompts:  {doc.AllEntries.Count}");

        return 0;
    }

    private static string FormatJson(E2eTestPromptDocument doc)
    {
        var exportModel = new
        {
            doc.Title,
            TotalSections = doc.Sections.Count,
            TotalTools = doc.ToolNames.Count,
            TotalPrompts = doc.AllEntries.Count,
            Sections = doc.Sections.Select(s => new
            {
                s.Heading,
                ToolCount = s.Entries.Select(e => e.ToolName).Distinct().Count(),
                PromptCount = s.Entries.Count,
                Tools = s.Entries
                    .GroupBy(e => e.ToolName, StringComparer.Ordinal)
                    .Select(g => new
                    {
                        ToolName = g.Key,
                        TestPrompts = g.Select(e => e.TestPrompt).ToList()
                    }).ToList()
            }).ToList()
        };

        return JsonSerializer.Serialize(exportModel, JsonOptions);
    }

    private static string FormatSummary(E2eTestPromptDocument doc)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(doc.Title);
        sb.AppendLine(new string('=', doc.Title.Length));
        sb.AppendLine();
        sb.AppendLine($"Sections: {doc.Sections.Count}");
        sb.AppendLine($"Tools:    {doc.ToolNames.Count}");
        sb.AppendLine($"Prompts:  {doc.AllEntries.Count}");
        sb.AppendLine();

        foreach (var section in doc.Sections)
        {
            var toolCount = section.Entries.Select(e => e.ToolName).Distinct().Count();
            sb.AppendLine($"  {section.Heading}");
            sb.AppendLine($"    Tools: {toolCount}, Prompts: {section.Entries.Count}");
        }

        sb.AppendLine();
        sb.AppendLine("Tools by namespace:");

        foreach (var kvp in doc.GetEntriesByNamespace().OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count} prompts");
        }

        return sb.ToString();
    }
}
