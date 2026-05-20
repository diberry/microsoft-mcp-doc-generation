// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using PipelineRunner.Validation;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Tests for <see cref="CompositionOutputValidator"/> covering all 6 composition
/// scenarios from the spec plus edge cases.
/// </summary>
public sealed class CompositionOutputValidatorTests : IDisposable
{
    private readonly string _outputRoot;

    public CompositionOutputValidatorTests()
    {
        _outputRoot = Path.Combine(Path.GetTempPath(), $"composition-validator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputRoot))
            Directory.Delete(_outputRoot, recursive: true);
    }

    // ── 1. Standalone success ──────────────────────────────────────────────────

    [Fact]
    public void Validate_StandaloneSuccess_NoIssues()
    {
        CreateFile("tool-family", "azure-container-apps.md");
        var entries = new[]
        {
            new BrandMappingEntry("containerapps", "Azure Container Apps", "Container Apps", "azure-container-apps", "standalone"),
        };

        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    // ── 2. Standalone failure ──────────────────────────────────────────────────

    [Fact]
    public void Validate_StandaloneFileMissing_ReportsIssue()
    {
        // Do NOT create azure-container-apps.md
        var entries = new[]
        {
            new BrandMappingEntry("containerapps", "Azure Container Apps", "Container Apps", "azure-container-apps", "standalone"),
        };

        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        Assert.False(result.Success);
        Assert.Single(result.Issues);
        Assert.Equal("containerapps", result.Issues[0].Identifier);
        Assert.Equal("standalone", result.Issues[0].IssueType);
        Assert.Contains("azure-container-apps.md", result.Issues[0].Message);
    }

    // ── 3. Merge success ───────────────────────────────────────────────────────

    [Fact]
    public void Validate_MergeSuccess_AllIntermediatesPlusMergedOutput()
    {
        // Primary produces azure-functions.md; secondary produces azure-functions-development.md
        // The merged output keyed by mergeGroup "azure-functions" = azure-functions.md (same as primary).
        CreateFile("tool-family", "azure-functions.md");
        CreateFile("tool-family", "azure-functions-development.md");

        var entries = new BrandMappingEntry[]
        {
            new("functionapp", "Azure Functions", "Functions", "azure-functions",
                Composition: "merge", MergeGroup: "azure-functions", MergeOrder: 1, MergeRole: "primary"),
            new("functions", "Azure Functions", "Functions", "azure-functions-development",
                Composition: "merge", MergeGroup: "azure-functions", MergeOrder: 2, MergeRole: "secondary"),
        };

        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    // ── 4. Merge failure (partial group) ──────────────────────────────────────

    [Fact]
    public void Validate_MergePartialGroup_SecondaryIntermediateMissing_ReportsIssue()
    {
        // Primary intermediate exists but secondary is missing.
        CreateFile("tool-family", "azure-functions.md");
        // azure-functions-development.md intentionally absent

        var entries = new BrandMappingEntry[]
        {
            new("functionapp", "Azure Functions", "Functions", "azure-functions",
                Composition: "merge", MergeGroup: "azure-functions", MergeOrder: 1, MergeRole: "primary"),
            new("functions", "Azure Functions", "Functions", "azure-functions-development",
                Composition: "merge", MergeGroup: "azure-functions", MergeOrder: 2, MergeRole: "secondary"),
        };

        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        Assert.False(result.Success);
        // Secondary intermediate missing
        Assert.Contains(result.Issues, i =>
            i.Identifier == "functions" &&
            i.IssueType == "merge-intermediate" &&
            i.Message.Contains("azure-functions-development.md"));
    }

    [Fact]
    public void Validate_MergeGroupMergedOutputMissing_ReportsIssue()
    {
        // Both intermediates exist but the merged output file is absent.
        // For this test we use a group where the mergeGroup key differs from primary's fileName.
        CreateFile("tool-family", "azure-monitor.md");
        CreateFile("tool-family", "azure-workbooks.md");
        // Drop the mergeGroup file to force the merged-output check to fail.
        // Since primary fileName == mergeGroup for azure-monitor, we delete the file and
        // use a synthetic scenario where the merged key differs.

        // Use a synthetic merge group where mergeGroup "azure-observability" differs from
        // both member fileNames so we can test the merged-output check independently.
        var entries = new BrandMappingEntry[]
        {
            new("monitorns", "Azure Monitor", "Monitor", "azure-monitor",
                Composition: "merge", MergeGroup: "azure-observability", MergeOrder: 1, MergeRole: "primary"),
            new("workbooksns", "Azure Workbooks", "Workbooks", "azure-workbooks",
                Composition: "merge", MergeGroup: "azure-observability", MergeOrder: 2, MergeRole: "secondary"),
            // azure-observability.md intentionally absent
        };

        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, i =>
            i.Identifier == "azure-observability" &&
            i.IssueType == "merge-output" &&
            i.Message.Contains("azure-observability.md"));
    }

    // ── 5. Split success ───────────────────────────────────────────────────────

    [Fact]
    public void Validate_SplitSuccess_EachNamespaceProducesItsOwnFile()
    {
        CreateFile("tool-family", "azure-cli-extension-generate.md");
        CreateFile("tool-family", "azure-cli-extension-install.md");

        var entries = new BrandMappingEntry[]
        {
            new("extension_cli_generate", "Azure CLI Extension", "CLI Extension", "azure-cli-extension-generate",
                Composition: "split", MergeGroup: "azure-cli-extension", MergeOrder: 1, MergeRole: "primary"),
            new("extension_cli_install", "Azure CLI Extension", "CLI Extension", "azure-cli-extension-install",
                Composition: "split", MergeGroup: "azure-cli-extension", MergeOrder: 2, MergeRole: "secondary"),
        };

        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    // ── 6. Split failure ───────────────────────────────────────────────────────

    [Fact]
    public void Validate_SplitFileMissing_ReportsIssue()
    {
        CreateFile("tool-family", "azure-cli-extension-install.md");
        // azure-cli-extension-generate.md intentionally absent

        var entries = new BrandMappingEntry[]
        {
            new("extension_cli_generate", "Azure CLI Extension", "CLI Extension", "azure-cli-extension-generate",
                Composition: "split", MergeGroup: "azure-cli-extension", MergeOrder: 1, MergeRole: "primary"),
            new("extension_cli_install", "Azure CLI Extension", "CLI Extension", "azure-cli-extension-install",
                Composition: "split", MergeGroup: "azure-cli-extension", MergeOrder: 2, MergeRole: "secondary"),
        };

        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, i =>
            i.Identifier == "extension_cli_generate" &&
            i.IssueType == "split" &&
            i.Message.Contains("azure-cli-extension-generate.md"));
    }

    // ── Edge cases ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyEntries_SuccessWithNoIssues()
    {
        var result = CompositionOutputValidator.Validate(_outputRoot, []);

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_OutputDirectoryMissing_ReturnsDirectoryIssue()
    {
        Directory.Delete(_outputRoot, recursive: true);

        var result = CompositionOutputValidator.Validate(_outputRoot, []);

        Assert.False(result.Success);
        Assert.Single(result.Issues);
        Assert.Equal("directory", result.Issues[0].Identifier);
        Assert.Equal("N/A", result.Issues[0].IssueType);
        Assert.Contains(_outputRoot, result.Issues[0].Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_InvalidOutputPath_ThrowsArgumentException(string? outputPath)
    {
        Assert.ThrowsAny<ArgumentException>(() => CompositionOutputValidator.Validate(outputPath!, []));
    }

    [Fact]
    public void Validate_NullEntries_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CompositionOutputValidator.Validate(_outputRoot, null!));
    }

    [Fact]
    public void Validate_NullComposition_TreatedAsStandalone()
    {
        // An entry with no Composition field defaults to standalone behaviour.
        CreateFile("tool-family", "azure-compute.md");
        var entries = new[]
        {
            new BrandMappingEntry("compute", "Azure Compute", "Compute", "azure-compute", Composition: null),
        };

        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_NullCompositionFileMissing_ReportsStandaloneIssue()
    {
        // azure-compute.md absent; null Composition → treated as standalone.
        var entries = new[]
        {
            new BrandMappingEntry("compute", "Azure Compute", "Compute", "azure-compute", Composition: null),
        };

        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        Assert.False(result.Success);
        Assert.Single(result.Issues);
        Assert.Equal("standalone", result.Issues[0].IssueType);
    }

    [Fact]
    public void Validate_MultipleStandaloneMissing_ReportsAllIssues()
    {
        var entries = new[]
        {
            new BrandMappingEntry("containerapps", "Azure Container Apps", "Container Apps", "azure-container-apps", "standalone"),
            new BrandMappingEntry("aks", "Azure Kubernetes Service", "AKS", "azure-kubernetes-service", "standalone"),
        };
        // Neither file exists

        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        Assert.False(result.Success);
        Assert.Equal(2, result.Issues.Count);
    }

    [Fact]
    public void Validate_MergeGroupWithNoMergeGroupField_MergedOutputCheckSkipped()
    {
        // A merge entry without a MergeGroup field is unusual (config error), but should not crash.
        CreateFile("tool-family", "azure-orphan.md");
        var entries = new[]
        {
            new BrandMappingEntry("orphan", "Azure Orphan", "Orphan", "azure-orphan",
                Composition: "merge", MergeGroup: null),
        };

        // Should not throw; the group with null MergeGroup is excluded from group validation.
        var result = CompositionOutputValidator.Validate(_outputRoot, entries);

        // The entry has no mergeGroup so it isn't part of any group check — no issues raised.
        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    // ── Per-namespace IPostValidator tests ─────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_StandaloneNamespace_FileExists_ReturnsSuccess()
    {
        var (context, outputPath) = CreateContext("containerapps", "azure-container-apps");
        CreateFile(outputPath, "tool-family", "azure-container-apps.md");
        InjectEntries(context,
            new BrandMappingEntry("containerapps", "Azure Container Apps", "Container Apps", "azure-container-apps", "standalone"));

        var validator = new CompositionOutputValidator();
        var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidateAsync_StandaloneNamespace_FileMissing_ReturnsFail()
    {
        var (context, _) = CreateContext("containerapps", "azure-container-apps");
        // File intentionally absent
        InjectEntries(context,
            new BrandMappingEntry("containerapps", "Azure Container Apps", "Container Apps", "azure-container-apps", "standalone"));

        var validator = new CompositionOutputValidator();
        var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("azure-container-apps.md"));
    }

    [Fact]
    public async Task ValidateAsync_MergeSecondaryNamespace_IntermediateExists_ReturnsSuccess()
    {
        var (context, outputPath) = CreateContext("functions", "azure-functions-development");
        CreateFile(outputPath, "tool-family", "azure-functions-development.md");
        InjectEntries(context,
            new BrandMappingEntry("functions", "Azure Functions", "Functions", "azure-functions-development",
                Composition: "merge", MergeGroup: "azure-functions", MergeOrder: 2, MergeRole: "secondary"));

        var validator = new CompositionOutputValidator();
        var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateAsync_NamespaceNotInMapping_ReturnsSuccessWithNoWarnings()
    {
        // When a namespace has no brand mapping entry, the validator cannot check the
        // composition type and falls back to checking the outputFileName from context.
        // If the fallback file exists, it passes.
        var (context, outputPath) = CreateContext("unknown-ns", "azure-unknown");
        CreateFile(outputPath, "tool-family", "azure-unknown.md");
        InjectEntries(context); // empty entries

        var validator = new CompositionOutputValidator();
        var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateAsync_ValidatorNameIsCorrect()
    {
        var (context, _) = CreateContext("compute", "azure-compute");
        InjectEntries(context);

        var validator = new CompositionOutputValidator();

        Assert.Equal("CompositionOutputValidator", validator.Name);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void CreateFile(string subdirectory, string fileName)
    {
        var dir = Path.Combine(_outputRoot, subdirectory);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), "# Placeholder\n");
    }

    private static void CreateFile(string basePath, string subdirectory, string fileName)
    {
        var dir = Path.Combine(basePath, subdirectory);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), "# Placeholder\n");
    }

    private (PipelineContext Context, string OutputPath) CreateContext(string namespaceName, string outputFileName)
    {
        var outputPath = Path.Combine(_outputRoot, $"generated-{namespaceName}");
        Directory.CreateDirectory(outputPath);

        var context = new PipelineContext
        {
            Request = new PipelineRequest(namespaceName, [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
            RepoRoot = _outputRoot,
            McpToolsRoot = Path.Combine(_outputRoot, "mcp-tools"),
            OutputPath = outputPath,
            ProcessRunner = new RecordingProcessRunner(),
            Workspaces = new WorkspaceManager(),
            CliMetadataLoader = new StubCliMetadataLoader(),
            TargetMatcher = new TargetMatcher(),
            FilteredCliWriter = new StubFilteredCliWriter(),
            BuildCoordinator = new StubBuildCoordinator(),
            AiCapabilityProbe = new StubAiCapabilityProbe(),
            Reports = new BufferedReportWriter(),
            CliVersion = "1.2.3",
            SelectedNamespaces = [namespaceName],
        };

        context.Items[ToolFamilyPostAssemblyValidator.FamilyNameContextKey] = namespaceName;
        context.Items[ToolFamilyPostAssemblyValidator.OutputFileNameContextKey] = outputFileName;

        return (context, outputPath);
    }

    private static void InjectEntries(PipelineContext context, params BrandMappingEntry[] entries)
    {
        context.Items[CompositionOutputValidator.BrandEntriesContextKey] =
            (IReadOnlyList<BrandMappingEntry>)entries;
    }

    private sealed class FakeStep : IPipelineStep
    {
        public int Id => 4;
        public string Name => "Generate tool-family article";
        public StepScope Scope => StepScope.Namespace;
        public FailurePolicy FailurePolicy => FailurePolicy.Fatal;
        public IReadOnlyList<int> DependsOn => [];
        public IReadOnlyList<IPostValidator> PostValidators => [];
        public int MaxRetries => 0;
        public bool RequiresCliOutput => true;
        public bool RequiresCliVersion => true;
        public bool RequiresAiConfiguration => false;
        public bool CreatesFilteredCliView => false;
        public bool UsesIsolatedWorkspace => false;
        public IReadOnlyList<string> ExpectedOutputs => [];
        public string Implementation => "Typed";

        public ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(StepResult.DryRun([]));
    }
}
