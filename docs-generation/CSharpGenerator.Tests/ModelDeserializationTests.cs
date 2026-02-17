// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using CSharpGenerator.Models;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests JSON deserialization of all model classes used by the pipeline.
/// Priority: P0 — models are the foundation of every generator.
/// </summary>
public class ModelDeserializationTests
{
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── CliOutput ──────────────────────────────────────────────

    [Fact]
    public void CliOutput_DeserializesResultsList()
    {
        var cliOutput = TestHelpers.LoadCliOutput();

        Assert.NotNull(cliOutput.Results);
        Assert.Equal(5, cliOutput.Results.Count);
    }

    [Fact]
    public void CliOutput_EmptyResults_DeserializesToEmptyList()
    {
        var json = """{"Results": []}""";
        var cliOutput = JsonSerializer.Deserialize<CliOutput>(json, CaseInsensitiveOptions);

        Assert.NotNull(cliOutput);
        Assert.Empty(cliOutput!.Results);
    }

    // ── Tool ───────────────────────────────────────────────────

    [Fact]
    public void Tool_DeserializesAllProperties()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var tool = cliOutput.Results[0];

        Assert.Equal("azmcp storage account list", tool.Name);
        Assert.Equal("storage account list", tool.Command);
        Assert.Equal("List all storage accounts in a subscription.", tool.Description);
        Assert.NotNull(tool.Option);
        Assert.Equal(5, tool.Option!.Count);
    }

    [Fact]
    public void Tool_DefaultValues_AreCorrect()
    {
        var tool = new Tool();

        Assert.Null(tool.Name);
        Assert.Null(tool.Command);
        Assert.Null(tool.Description);
        Assert.False(tool.HasConditionalRequired);
        Assert.False(tool.HasAnnotation);
        Assert.False(tool.HasParameters);
        Assert.False(tool.HasExamplePrompts);
        Assert.Null(tool.ConditionalRequiredParameters);
        Assert.Null(tool.ConditionalRequiredNote);
    }

    // ── Option ─────────────────────────────────────────────────

    [Fact]
    public void Option_DeserializesAllProperties()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        // storage account get → --resource-group is Required
        var tool = cliOutput.Results[1];
        var requiredOpt = tool.Option!.First(o => o.Name == "--resource-group");

        Assert.Equal("--resource-group", requiredOpt.Name);
        Assert.Equal("string", requiredOpt.Type);
        Assert.True(requiredOpt.Required);
        Assert.Equal("The resource group name.", requiredOpt.Description);
    }

    [Fact]
    public void Option_DefaultRequiredText_IsEmptyString()
    {
        var option = new Option();
        Assert.Equal("", option.RequiredText);
    }

    // ── ToolMetadata ───────────────────────────────────────────

    [Fact]
    public void ToolMetadata_DeserializesJsonPropertyNames()
    {
        var json = """
        {
            "destructive": { "value": true, "description": "Deletes data." },
            "idempotent": { "value": false, "description": "Not idempotent." },
            "openWorld": { "value": true, "description": "Open world." },
            "readOnly": { "value": false, "description": "Writes data." },
            "secret": { "value": true, "description": "Has secrets." },
            "localRequired": { "value": false, "description": "No local." }
        }
        """;
        var metadata = JsonSerializer.Deserialize<ToolMetadata>(json, CaseInsensitiveOptions);

        Assert.NotNull(metadata);
        Assert.True(metadata!.Destructive!.Value);
        Assert.Equal("Deletes data.", metadata.Destructive.Description);
        Assert.False(metadata.Idempotent!.Value);
        Assert.True(metadata.OpenWorld!.Value);
        Assert.False(metadata.ReadOnly!.Value);
        Assert.True(metadata.Secret!.Value);
        Assert.False(metadata.LocalRequired!.Value);
    }

    [Fact]
    public void ToolMetadata_NullableProperties_DefaultToNull()
    {
        var metadata = new ToolMetadata();

        Assert.Null(metadata.Destructive);
        Assert.Null(metadata.Idempotent);
        Assert.Null(metadata.OpenWorld);
        Assert.Null(metadata.ReadOnly);
        Assert.Null(metadata.Secret);
        Assert.Null(metadata.LocalRequired);
    }

    [Fact]
    public void ToolMetadata_PartialDeserialization_LeavesOthersNull()
    {
        var json = """{ "readOnly": { "value": true, "description": "Read only." } }""";
        var metadata = JsonSerializer.Deserialize<ToolMetadata>(json, CaseInsensitiveOptions);

        Assert.NotNull(metadata);
        Assert.NotNull(metadata!.ReadOnly);
        Assert.True(metadata.ReadOnly!.Value);
        Assert.Null(metadata.Destructive);
        Assert.Null(metadata.Secret);
    }

    // ── MetadataValue ──────────────────────────────────────────

    [Fact]
    public void MetadataValue_DeserializesValueAndDescription()
    {
        var json = """{ "value": true, "description": "Test description" }""";
        var mv = JsonSerializer.Deserialize<MetadataValue>(json, CaseInsensitiveOptions);

        Assert.NotNull(mv);
        Assert.True(mv!.Value);
        Assert.Equal("Test description", mv.Description);
    }

    [Fact]
    public void MetadataValue_MissingDescription_IsNull()
    {
        var json = """{ "value": false }""";
        var mv = JsonSerializer.Deserialize<MetadataValue>(json, CaseInsensitiveOptions);

        Assert.NotNull(mv);
        Assert.False(mv!.Value);
        Assert.Null(mv.Description);
    }

    // ── CommonParameter / CommonParameterDefinition ────────────

    [Fact]
    public void CommonParameter_DefaultValues_AreCorrect()
    {
        var cp = new CommonParameter();

        Assert.Equal("", cp.Name);
        Assert.Equal("", cp.Type);
        Assert.False(cp.IsRequired);
        Assert.Equal("", cp.Description);
        Assert.Equal(0, cp.UsagePercent);
        Assert.False(cp.IsHidden);
        Assert.Equal("", cp.Source);
        Assert.Equal("", cp.RequiredText);
        Assert.Equal("", cp.NL_Name);
    }

    [Fact]
    public void CommonParameterDefinition_DeserializesFromJson()
    {
        var json = """
        [
            { "Name": "--tenant", "Type": "string", "IsRequired": false, "Description": "The tenant ID." },
            { "Name": "--subscription", "Type": "string", "IsRequired": false, "Description": "The subscription ID." }
        ]
        """;
        var defs = JsonSerializer.Deserialize<List<CommonParameterDefinition>>(json, CaseInsensitiveOptions);

        Assert.NotNull(defs);
        Assert.Equal(2, defs!.Count);
        Assert.Equal("--tenant", defs[0].Name);
        Assert.False(defs[0].IsRequired);
    }

    // ── TransformedData ────────────────────────────────────────

    [Fact]
    public void TransformedData_DefaultValues_AreCorrect()
    {
        var td = new TransformedData();

        Assert.Equal("", td.Version);
        Assert.Empty(td.Tools);
        Assert.Empty(td.Areas);
        Assert.Empty(td.SourceDiscoveredCommonParams);
    }

    // ── AreaData ───────────────────────────────────────────────

    [Fact]
    public void AreaData_DefaultValues_AreCorrect()
    {
        var ad = new AreaData();

        Assert.Equal("", ad.Description);
        Assert.Equal(0, ad.ToolCount);
        Assert.Empty(ad.Tools);
    }

    // ── Full round-trip: JSON → CliOutput → Tool with Metadata ──

    [Fact]
    public void FullDeserialization_ToolWithConditionalDescription()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        // "storage account get" has conditional requirement text
        var tool = cliOutput.Results[1];

        Assert.Contains("Requires at least one", tool.Description);
        Assert.NotNull(tool.Metadata);
        Assert.NotNull(tool.Metadata!.ReadOnly);
        Assert.True(tool.Metadata.ReadOnly!.Value);
    }

    [Fact]
    public void FullDeserialization_MetadataOnUploadTool()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        // "storage blob upload" has destructive=true
        var tool = cliOutput.Results[2];

        Assert.NotNull(tool.Metadata);
        Assert.True(tool.Metadata!.Destructive!.Value);
        Assert.True(tool.Metadata.Idempotent!.Value);
        Assert.Null(tool.Metadata.ReadOnly);
    }
}
