using DocGeneration.McpCliMetadata;
using System.Text.Json;
using System.Text;
using PipelineRunner.Services;
using Shared;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: McpCliMetadata <output-dir>");
    return 1;
}

var outputDir = args[0];
var cliDir = Path.Combine(outputDir, "cli");
Directory.CreateDirectory(cliDir);

var runner = new AzmcpRunner();

try
{
    Console.WriteLine("Extracting CLI version...");
    var version = await runner.GetVersionAsync();
    var versionJson = JsonSerializer.Serialize(new { version });
    await File.WriteAllTextAsync(Path.Combine(cliDir, "cli-version.json"), versionJson);
    Console.WriteLine($"✓ CLI version: {version}");

    Console.WriteLine("Extracting tool metadata...");
    var toolsJson = await runner.GetToolsJsonAsync();
    await File.WriteAllTextAsync(Path.Combine(cliDir, "cli-output.json"), toolsJson);
    Console.WriteLine("✓ Tool metadata written to cli-output.json");

    Console.WriteLine("Extracting namespace metadata...");
    var namespaceJson = await runner.GetNamespaceJsonAsync();
    await File.WriteAllTextAsync(Path.Combine(cliDir, "cli-namespace.json"), namespaceJson);
    Console.WriteLine("✓ Namespace metadata written to cli-namespace.json");

    // Generate namespace-mapping.json for content-impact analysis
    Console.WriteLine("Generating namespace mapping...");
    var mcpToolsRoot = FindMcpToolsRoot(cliDir);
    var brandMappingPath = Path.Combine(mcpToolsRoot, "data", "brand-to-server-mapping.json");
    
    if (!File.Exists(brandMappingPath))
    {
        Console.WriteLine($"⚠️  Warning: brand-to-server-mapping.json not found at {brandMappingPath}, skipping namespace mapping");
    }
    else
    {
        var brandMappingJson = await File.ReadAllTextAsync(brandMappingPath);
        var brandMappings = JsonSerializer.Deserialize<List<BrandMappingEntry>>(brandMappingJson, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        }) ?? throw new InvalidOperationException("Failed to deserialize brand mappings");

        if (brandMappings.Count == 0)
        {
            Console.WriteLine("⚠️  Warning: brand mapping file is empty, skipping namespace mapping");
        }
        else
        {
            // Parse CLI output JSON to extract tools. Some CLI builds can emit
            // stray control chars in string fields; strip them before parsing.
            var sanitizedToolsJson = StripInvalidControlCharacters(toolsJson);
            var jsonDoc = JsonDocument.Parse(sanitizedToolsJson);
            var results = jsonDoc.RootElement.GetProperty("results");
            var tools = new List<CliTool>();
            
            foreach (var result in results.EnumerateArray())
            {
                var command = result.GetProperty("command").GetString() ?? "";
                var name = result.GetProperty("name").GetString() ?? "";
                var description = result.TryGetProperty("description", out var descProp) 
                    ? descProp.GetString() 
                    : null;
                
                tools.Add(new CliTool(command, name, description, result));
            }
            
            var cliOutputSnapshot = new CliMetadataSnapshot(
                Path.Combine(cliDir, "cli-output.json"),
                jsonDoc.RootElement,
                tools);

            var emitter = new NamespaceMappingEmitter();
            var unmatchedTools = await emitter.EmitAsync(
                brandMappings,
                cliOutputSnapshot,
                version,
                cliDir,
                CancellationToken.None);

            Console.WriteLine("✓ Namespace mapping written to namespace-mapping.json");
            
            if (unmatchedTools.Count > 0)
            {
                Console.WriteLine($"⚠️  Warning: {unmatchedTools.Count} tools did not match any namespace prefix");
            }
        }
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"⛔ McpCliMetadata failed: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 1;
}

static string FindMcpToolsRoot(string startPath)
{
    // Navigate up from cli dir to find mcp-tools root
    var current = new DirectoryInfo(startPath);
    while (current != null)
    {
        var dataPath = Path.Combine(current.FullName, "data", "brand-to-server-mapping.json");
        if (File.Exists(dataPath))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    
    // Fallback: assume standard structure (cli is under generated/cli, mcp-tools is ../../mcp-tools)
    return Path.GetFullPath(Path.Combine(startPath, "..", "..", "mcp-tools"));
}

static string StripInvalidControlCharacters(string json)
{
    if (string.IsNullOrEmpty(json))
    {
        return json;
    }

    var sb = new StringBuilder(json.Length);
    var inString = false;
    var escaping = false;

    foreach (var ch in json)
    {
        if (inString)
        {
            if (escaping)
            {
                sb.Append(ch);
                escaping = false;
                continue;
            }

            if (ch == '\\')
            {
                sb.Append(ch);
                escaping = true;
                continue;
            }

            if (ch == '"')
            {
                sb.Append(ch);
                inString = false;
                continue;
            }

            if (char.IsControl(ch))
            {
                continue;
            }

            sb.Append(ch);
            continue;
        }

        sb.Append(ch);
        if (ch == '"')
        {
            inString = true;
        }
    }

    return sb.ToString();
}
