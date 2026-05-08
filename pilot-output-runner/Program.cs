using CSharpGenerator.Generators;
using DocGeneration.Steps.ToolFamilyCleanup.Services;
using Shared;

var repoRoot = FindRepoRoot();
var outputRoot = Path.Combine(repoRoot, "pilot-output");
var templateFile = Path.Combine(repoRoot, "mcp-tools", "templates", "cli-parameter-template.hbs");
var nameContext = await FileNameContext.CreateAsync();
var namespaces = new[] { "storage", "compute", "appservice", "azurebackup", "functions" };

Console.WriteLine($"Generating CLI pilot output to: {outputRoot}");
Console.WriteLine($"Template: {templateFile}");
Console.WriteLine();

foreach (var ns in namespaces)
{
    var cliJsonPath = Path.Combine(repoRoot, $"generated-{ns}", "cli", "cli-output.json");
    if (!File.Exists(cliJsonPath))
    {
        Console.WriteLine($"[{ns}] SKIP - no cli-output.json");
        continue;
    }

    var json = await File.ReadAllTextAsync(cliJsonPath);
    var cliTools = CliJsonMapper.MapFromCliOutput(json);

    var paramDir = Path.Combine(outputRoot, ns, "parameters-cli");
    var exampleDir = Path.Combine(outputRoot, ns, "example-commands");
    var assembledDir = Path.Combine(outputRoot, ns, "assembled");
    var tabbedDir = Path.Combine(outputRoot, ns, "tabbed");

    Directory.CreateDirectory(paramDir);
    Directory.CreateDirectory(exampleDir);
    Directory.CreateDirectory(assembledDir);
    Directory.CreateDirectory(tabbedDir);

    // Generate parameter-cli files
    await CliParameterGenerator.GenerateParameterCliFilesAsync(
        cliTools, templateFile, paramDir, nameContext, "1.0.0-pilot", DateTime.UtcNow);

    // Generate example-commands files
    await CliExampleCommandGenerator.GenerateExampleCommandFilesAsync(
        cliTools, exampleDir, nameContext, "1.0.0-pilot", DateTime.UtcNow);

    // Assemble CLI content
    var assembled = await CliContentAssembler.AssembleAllCliContentAsync(
        cliTools, paramDir, exampleDir, nameContext);

    // Write assembled + tabbed files
    foreach (var (command, cliContent) in assembled)
    {
        var safeName = command.Replace(" ", "-");
        await File.WriteAllTextAsync(Path.Combine(assembledDir, $"{safeName}.md"), cliContent);

        var fakeMcpContent = $"This tool executes `{command}` via MCP Server.\n\nSee parameters below.";
        var tabbed = CliTabWrapper.WrapWithTabs(fakeMcpContent, cliContent);
        await File.WriteAllTextAsync(Path.Combine(tabbedDir, $"{safeName}.md"), tabbed);
    }

    var paramCount = Directory.GetFiles(paramDir, "*.md").Length;
    var exampleCount = Directory.GetFiles(exampleDir, "*.md").Length;
    Console.WriteLine($"[{ns}] {cliTools.Count} tools -> {paramCount} param, {exampleCount} example, {assembled.Count} assembled, {assembled.Count} tabbed");
}

Console.WriteLine("\nDone! Files at: " + outputRoot);

static string FindRepoRoot()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir, "mcp-doc-generation.sln")))
            return dir;
        dir = Directory.GetParent(dir)?.FullName;
    }
    throw new InvalidOperationException("Cannot find repo root (DocGeneration.sln)");
}
