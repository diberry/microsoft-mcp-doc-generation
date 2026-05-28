using DocGeneration.McpCliMetadata;
using System.Text.Json;

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

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"⛔ McpCliMetadata failed: {ex.Message}");
    return 1;
}
