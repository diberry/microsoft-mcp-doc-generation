using DocGeneration.McpCliMetadata;
using System.Text.Json;
using PipelineRunner.Services;

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

    Console.WriteLine("Extracting tool metadata...");
    var toolsJson = await runner.GetToolsJsonAsync();

    Console.WriteLine("Extracting namespace metadata...");
    var namespaceJson = await runner.GetNamespaceJsonAsync();

    // Single source-side sanitization point: strip any raw control chars the
    // upstream azmcp CLI can emit inside JSON strings BEFORE writing to disk, so
    // every downstream reader receives valid JSON and never needs to sanitize.
    await CliMetadataWriter.WriteArtifactsAsync(cliDir, versionJson, toolsJson, namespaceJson);
    Console.WriteLine($"✓ CLI version: {version}");
    Console.WriteLine("✓ Tool metadata written to cli-output.json");
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
            // Reuse the same source-side sanitizer for the in-memory mapping parse
            // so this matches exactly what was written to cli-output.json.
            var sanitizedToolsJson = CliMetadataWriter.Sanitize(toolsJson);
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

