using System.CommandLine;
using AzmcpCommandParser.Parsing;
using AzmcpCommandParser.Serialization;

var fileOption = new Option<string>(
    "--file",
    description: "Path to azmcp-commands.md file")
{ IsRequired = true };

var outputOption = new Option<string>(
    "--output",
    description: "Output JSON file path (default: stdout)");

var rootCommand = new RootCommand("Parses azmcp-commands.md into structured JSON")
{
    fileOption,
    outputOption
};

rootCommand.SetHandler(async (string file, string? output) =>
{
    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"File not found: {file}");
        Environment.Exit(1);
    }

    var parser = new MarkdownCommandParser();
    var document = parser.ParseFile(file);

    // Summary
    var totalCommands = document.ServiceSections.Sum(s =>
        s.Commands.Count + s.SubSections.Sum(ss => ss.Commands.Count));
    var definitionCommands = document.ServiceSections.Sum(s =>
        s.Commands.Count(c => !c.IsExample) +
        s.SubSections.Sum(ss => ss.Commands.Count(c => !c.IsExample)));

    Console.Error.WriteLine($"Parsed: {document.Title}");
    Console.Error.WriteLine($"  Global options: {document.GlobalOptions.Count}");
    Console.Error.WriteLine($"  Service sections: {document.ServiceSections.Count}");
    Console.Error.WriteLine($"  Total commands: {totalCommands} ({definitionCommands} definitions, {totalCommands - definitionCommands} examples)");

    if (!string.IsNullOrEmpty(output))
    {
        CommandDocumentSerializer.SerializeToFile(document, output);
        Console.Error.WriteLine($"  Output: {output}");
    }
    else
    {
        Console.WriteLine(CommandDocumentSerializer.Serialize(document));
    }

    await Task.CompletedTask;
}, fileOption, outputOption);

return await rootCommand.InvokeAsync(args);
