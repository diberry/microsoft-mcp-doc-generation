// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

/// <summary>
/// Tests for CliContentAssembler — deterministic CLI markdown assembly from AI-improved prose + structural data.
/// </summary>
public class CliContentAssemblerTests : IDisposable
{
    private readonly string _tempDir;

    public CliContentAssemblerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cli-assembler-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static CliToolInfo MakeTool(
        string command = "storage account list",
        string description = "Lists all storage accounts in the subscription.",
        params CliSwitch[] switches)
    {
        return new CliToolInfo(command, description, switches.Length > 0 ? switches : Array.Empty<CliSwitch>());
    }

    private static CliToolInfo MakeToolWithSwitches(
        string command = "storage account list",
        string description = "Lists all storage accounts.")
    {
        return new CliToolInfo(command, description, new[]
        {
            new CliSwitch("--resource-group", "The name of the resource group.", "string", IsRequired: true),
            new CliSwitch("--location", "The Azure region.", "string", Default: "eastus"),
        });
    }

    // ── AssembleCliContent (single tool) ─────────────────────────────

    [Fact]
    public void AssembleCliContent_DoesNotIncludeDescription()
    {
        var tool = MakeTool(description: "Lists all storage accounts in the subscription.");

        var result = CliContentAssembler.AssembleCliContent(tool);

        Assert.DoesNotContain("Lists all storage accounts in the subscription.", result);
    }

    [Fact]
    public void AssembleCliContent_IncludesExampleCommands()
    {
        var tool = MakeTool();
        var exampleContent = "```bash\nazmcp storage account list --subscription my-sub\n```";

        var result = CliContentAssembler.AssembleCliContent(tool, exampleCommandsContent: exampleContent);

        Assert.Contains("azmcp storage account list --subscription my-sub", result);
    }

    [Fact]
    public void AssembleCliContent_IncludesParameterContent()
    {
        var tool = MakeTool();
        var paramContent = "| Parameter | Type | Description |\n|---|---|---|\n| `--sub` | string | The sub |";

        var result = CliContentAssembler.AssembleCliContent(tool, parameterCliContent: paramContent);

        Assert.Contains("`--sub`", result);
    }

    [Fact]
    public void AssembleCliContent_NullContent_SkipsSection()
    {
        var tool = MakeTool(); // no switches, no include content

        var result = CliContentAssembler.AssembleCliContent(tool);

        Assert.Equal(string.Empty, result);
        Assert.DoesNotContain("| Parameter", result);
    }

    [Fact]
    public void AssembleCliContent_NoIncludeFiles_InlineParameterTable()
    {
        var tool = MakeToolWithSwitches();

        // No parameterCliContent, no exampleCommandsContent → inline table
        var result = CliContentAssembler.AssembleCliContent(tool);

        Assert.Contains("| Parameter | Type | Required | Description |", result);
        Assert.Contains("`--resource-group`", result);
        Assert.Contains("`--location`", result);
    }

    [Fact]
    public void AssembleCliContent_NoSwitches_NoParameterTable()
    {
        var tool = MakeTool(); // empty switches

        var result = CliContentAssembler.AssembleCliContent(tool);

        Assert.DoesNotContain("| Parameter", result);
        Assert.DoesNotContain("|---", result);
    }

    [Fact]
    public void AssembleCliContent_SwitchNamesFromModel_NotHardcoded()
    {
        var tool = new CliToolInfo("custom tool", "A custom tool.", new[]
        {
            new CliSwitch("--my-custom-flag", "A custom flag.", "boolean"),
            new CliSwitch("--another-param", "Another param.", "int"),
        });

        var result = CliContentAssembler.AssembleCliContent(tool);

        Assert.Contains("`--my-custom-flag`", result);
        Assert.Contains("`--another-param`", result);
        // Ensure these come from the model, not hardcoded
        Assert.DoesNotContain("--subscription", result);
    }

    [Fact]
    public void AssembleCliContent_DefaultValueNotInTable()
    {
        var tool = new CliToolInfo("test", "Test.", new[]
        {
            new CliSwitch("--region", "The region.", "string", Default: "eastus"),
        });

        var result = CliContentAssembler.AssembleCliContent(tool);

        // Default column removed — default values should NOT appear in the table
        Assert.DoesNotContain("eastus", result);
        Assert.Contains("`--region`", result);
    }

    [Fact]
    public void AssembleCliContent_NullDefault_NoDefaultColumn()
    {
        var tool = new CliToolInfo("test", "Test.", new[]
        {
            new CliSwitch("--region", "The region.", "string"),
        });

        var result = CliContentAssembler.AssembleCliContent(tool);

        // No Default column at all
        Assert.DoesNotContain("| Default", result);
        Assert.Contains("| Parameter | Type | Required | Description |", result);
    }

    // ── AssembleAllCliContentAsync ────────────────────────────────────

    [Fact]
    public async Task AssembleAllCliContentAsync_ReadsIncludeFiles()
    {
        var paramDir = Path.Combine(_tempDir, "parameters-cli");
        var exampleDir = Path.Combine(_tempDir, "example-commands");
        Directory.CreateDirectory(paramDir);
        Directory.CreateDirectory(exampleDir);

        var ctx = new FileNameContext(
            new Dictionary<string, BrandMapping>(),
            new Dictionary<string, string>(),
            new HashSet<string>());

        var command = "storage account list";
        var tool = MakeTool(command, "Lists storage accounts.");

        // Create include files with the expected names
        var paramFileName = ToolFileNameBuilder.BuildParameterCliFileName(command, ctx);
        var exampleFileName = ToolFileNameBuilder.BuildExampleCommandsFileName(command, ctx);

        await File.WriteAllTextAsync(
            Path.Combine(paramDir, paramFileName),
            "---\ntitle: params\n---\n| Param | Type |\n|---|---|\n| `--sub` | string |");
        await File.WriteAllTextAsync(
            Path.Combine(exampleDir, exampleFileName),
            "---\ntitle: examples\n---\n```bash\nazmcp storage account list\n```");

        var tools = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [command] = tool
        };

        var result = await CliContentAssembler.AssembleAllCliContentAsync(tools, paramDir, exampleDir, ctx);

        Assert.Single(result);
        var content = result.Values.First();
        Assert.Contains("`--sub`", content);
        Assert.Contains("azmcp storage account list", content);
    }

    [Fact]
    public async Task AssembleAllCliContentAsync_MissingFiles_GracefulFallback()
    {
        var paramDir = Path.Combine(_tempDir, "params-missing");
        var exampleDir = Path.Combine(_tempDir, "examples-missing");
        Directory.CreateDirectory(paramDir);
        Directory.CreateDirectory(exampleDir);

        var ctx = new FileNameContext(
            new Dictionary<string, BrandMapping>(),
            new Dictionary<string, string>(),
            new HashSet<string>());

        var tool = MakeToolWithSwitches();
        var tools = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account list"] = tool
        };

        // Dirs exist but no matching files — should fall back to inline table
        var result = await CliContentAssembler.AssembleAllCliContentAsync(tools, paramDir, exampleDir, ctx);

        Assert.Single(result);
        var content = result.Values.First();
        // Should have inline parameter table since no include files
        Assert.Contains("| Parameter | Type | Required | Description |", content);
        Assert.Contains("`--resource-group`", content);
    }

    // ── Pipe character escaping ───────────────────────────────────────

    [Fact]
    public void AssembleCliContent_PipeInDescription_Escaped()
    {
        var tool = new CliToolInfo("test", "Test.", new[]
        {
            new CliSwitch("--format", "Output format: json | table | tsv", "string"),
        });

        var result = CliContentAssembler.AssembleCliContent(tool);

        // Pipes in description must be escaped to avoid breaking markdown table
        Assert.Contains(@"json \| table \| tsv", result);
        Assert.DoesNotContain("json | table | tsv", result);
    }

    [Fact]
    public void AssembleCliContent_PipeInDefault_NotRendered()
    {
        var tool = new CliToolInfo("test", "Test.", new[]
        {
            new CliSwitch("--sep", "Separator char.", "string", Default: "|"),
        });

        var result = CliContentAssembler.AssembleCliContent(tool);

        // Default column removed — pipe default value should NOT appear
        Assert.DoesNotContain("| Default", result);
        Assert.Contains("`--sep`", result);
    }

    [Fact]
    public void EscapePipe_NoPipes_Unchanged()
    {
        Assert.Equal("hello world", CliContentAssembler.EscapePipe("hello world"));
    }

    [Fact]
    public void EscapePipe_MultiplePipes_AllEscaped()
    {
        Assert.Equal(@"a \| b \| c", CliContentAssembler.EscapePipe("a | b | c"));
    }

    // ── Boundary tests: empty/whitespace fields ───────────────────────

    [Fact]
    public void AssembleCliContent_EmptyDescription_StillRenders()
    {
        var tool = new CliToolInfo("cmd", "", Array.Empty<CliSwitch>());

        var result = CliContentAssembler.AssembleCliContent(tool);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void AssembleCliContent_WhitespaceDescription_Renders()
    {
        var tool = new CliToolInfo("cmd", "   ", Array.Empty<CliSwitch>());

        var result = CliContentAssembler.AssembleCliContent(tool);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void AssembleCliContent_SwitchWithEmptyDescription_DashInTable()
    {
        var tool = new CliToolInfo("test", "Tool desc.", new[]
        {
            new CliSwitch("--flag", "", "boolean"),
        });

        var result = CliContentAssembler.AssembleCliContent(tool);

        // Empty description renders but doesn't break the table
        Assert.Contains("`--flag`", result);
        Assert.Contains("| boolean |", result);
    }
}
