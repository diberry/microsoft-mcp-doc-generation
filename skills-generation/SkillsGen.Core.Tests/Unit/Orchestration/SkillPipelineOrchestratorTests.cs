using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.Assessment;
using SkillsGen.Core.Fetchers;
using SkillsGen.Core.Generation;
using SkillsGen.Core.Logging;
using SkillsGen.Core.Models;
using SkillsGen.Core.Orchestration;
using SkillsGen.Core.Parsers;
using SkillsGen.Core.PostProcessing;
using SkillsGen.Core.Validation;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Orchestration;

public class SkillPipelineOrchestratorTests
{
    private readonly ISkillSourceFetcher _fetcher = Substitute.For<ISkillSourceFetcher>();
    private readonly ISkillParser _parser = Substitute.For<ISkillParser>();
    private readonly ITriggerParser _triggerParser = Substitute.For<ITriggerParser>();
    private readonly ITierAssessor _tierAssessor = Substitute.For<ITierAssessor>();
    private readonly ILlmRewriter _rewriter = Substitute.For<ILlmRewriter>();
    private readonly ISkillPageGenerator _generator = Substitute.For<ISkillPageGenerator>();
    private readonly ISkillPageValidator _validator = Substitute.For<ISkillPageValidator>();
    private readonly ISkillsLogger _logger = Substitute.For<ISkillsLogger>();

    private readonly string _outputDir;
    private readonly AcrolinxPostProcessor _postProcessor;

    public SkillPipelineOrchestratorTests()
    {
        _outputDir = Path.Combine(AppContext.BaseDirectory, "test-output-" + Guid.NewGuid().ToString("N")[..8]);
        _postProcessor = new AcrolinxPostProcessor(null, null,
            Substitute.For<ILogger<AcrolinxPostProcessor>>());
    }

    private SkillPipelineOrchestrator CreateOrchestrator(bool dryRun = false, bool force = false)
    {
        return new SkillPipelineOrchestrator(
            _fetcher, _parser, _triggerParser, _tierAssessor,
            _rewriter, _generator, _postProcessor, _validator,
            _logger, _outputDir, dryRun, force);
    }

    private void SetupHappyPath()
    {
        _fetcher.FetchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SkillSourceFiles?>(
                new SkillSourceFiles("# Test", "const shouldTriggerPrompts = ['hello'];", "/test", null)));

        _parser.Parse(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new SkillData
            {
                Name = "azure-test",
                DisplayName = "Azure Test",
                Description = "Test skill description."
            });

        _triggerParser.Parse(Arg.Any<string?>())
            .Returns(new TriggerData(["hello"], [], null));

        _tierAssessor.Assess(Arg.Any<SkillData>(), Arg.Any<TriggerData>())
            .Returns(new TierAssessment(2, [], "Tier 2", false, true, false, false, false));

        _rewriter.RewriteIntroAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("Rewritten description."));

        _generator.Generate(Arg.Any<SkillData>(), Arg.Any<TriggerData>(),
            Arg.Any<TierAssessment>(), Arg.Any<SkillPrerequisites>())
            .Returns("---\ntitle: Test\ndescription: Test\n---\n\n# Test\n\n## Prerequisites\n\n- GitHub Copilot\n\n## When to use\n\n## What it provides\n\nContent here.");

        _validator.Validate(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SkillData>(), Arg.Any<TriggerData>())
            .Returns(new SkillValidationResult(true, [], [], 150, 4));
    }

    [Fact]
    public async Task ProcessSkillAsync_SuccessPath_ReturnsValidResult()
    {
        SetupHappyPath();
        var orchestrator = CreateOrchestrator();

        var result = await orchestrator.ProcessSkillAsync("azure-test");

        result.SkillName.Should().Be("azure-test");
        result.Tier.Should().Be(2);
        result.Validation.IsValid.Should().BeTrue();
        result.OutputPath.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessSkillAsync_FetchFails_ReturnsFailResult()
    {
        _fetcher.FetchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SkillSourceFiles?>(null));

        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.ProcessSkillAsync("nonexistent");

        result.Validation.IsValid.Should().BeFalse();
        result.OutputPath.Should().BeNull();
    }

    [Fact]
    public async Task ProcessBatchAsync_ProcessesAllSkills()
    {
        SetupHappyPath();
        var orchestrator = CreateOrchestrator();
        var skills = new List<SkillInventoryEntry>
        {
            new("azure-test", "Azure Test", "Test"),
            new("azure-other", "Azure Other", "Test")
        };

        var report = await orchestrator.ProcessBatchAsync(skills);

        report.Results.Should().HaveCount(2);
    }

    [Fact]
    public async Task ProcessBatchAsync_SingleFailure_DoesNotAbort()
    {
        // First call fails, second succeeds
        _fetcher.FetchAsync("azure-fail", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SkillSourceFiles?>(null));

        _fetcher.FetchAsync("azure-ok", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SkillSourceFiles?>(
                new SkillSourceFiles("# OK", null, "/ok", null)));

        _parser.Parse(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new SkillData { Name = "azure-ok", DisplayName = "Azure OK", Description = "OK" });
        _triggerParser.Parse(Arg.Any<string?>()).Returns(new TriggerData([], [], null));
        _tierAssessor.Assess(Arg.Any<SkillData>(), Arg.Any<TriggerData>())
            .Returns(new TierAssessment(2, [], "T2", false, false, false, false, false));
        _rewriter.RewriteIntroAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("OK"));
        _generator.Generate(Arg.Any<SkillData>(), Arg.Any<TriggerData>(),
            Arg.Any<TierAssessment>(), Arg.Any<SkillPrerequisites>())
            .Returns("---\ntitle: T\ndescription: T\n---\n\n## Prerequisites\n\n- GitHub Copilot\n\n## When to use\n\n## What it provides\n");
        _validator.Validate(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SkillData>(), Arg.Any<TriggerData>())
            .Returns(new SkillValidationResult(true, [], [], 50, 3));

        var orchestrator = CreateOrchestrator();
        var skills = new List<SkillInventoryEntry>
        {
            new("azure-fail", "Azure Fail", "Test"),
            new("azure-ok", "Azure OK", "Test")
        };

        var report = await orchestrator.ProcessBatchAsync(skills);

        report.Results.Should().HaveCount(2);
        report.Results[0].Validation.IsValid.Should().BeFalse();
        report.Results[1].Validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessSkillAsync_DryRun_DoesNotWriteFiles()
    {
        SetupHappyPath();
        var orchestrator = CreateOrchestrator(dryRun: true);

        var result = await orchestrator.ProcessSkillAsync("azure-test");

        result.OutputPath.Should().BeNull();
        Directory.Exists(_outputDir).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessSkillAsync_Force_WritesDespiteValidationErrors()
    {
        SetupHappyPath();
        _validator.Validate(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SkillData>(), Arg.Any<TriggerData>())
            .Returns(new SkillValidationResult(false, ["some error"], [], 10, 1));

        var orchestrator = CreateOrchestrator(force: true);
        var result = await orchestrator.ProcessSkillAsync("azure-test");

        result.OutputPath.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessBatchAsync_WritesManifest()
    {
        SetupHappyPath();
        var orchestrator = CreateOrchestrator();
        var skills = new List<SkillInventoryEntry>
        {
            new("azure-test", "Azure Test", "Test")
        };

        await orchestrator.ProcessBatchAsync(skills);

        var manifestPath = Path.Combine(_outputDir, "generation-manifest.json");
        File.Exists(manifestPath).Should().BeTrue();
    }
}
