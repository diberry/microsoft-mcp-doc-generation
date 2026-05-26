using System.Text.Json;
using DocGeneration.Core.Tracing.Models;
using FluentAssertions;
using Xunit;

namespace DocGeneration.Core.Tracing.Tests;

public sealed class TraceWriterTests : IDisposable
{
    private readonly string _outputDirectory = Path.Combine(AppContext.BaseDirectory, "test-results", $"trace-writer-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task WritesValidJsonToPipelineTraceFile()
    {
        var writer = new TraceWriter();

        await writer.WriteAsync(_outputDirectory, CreatePipelineTrace(), CreateAiInteractions());

        var json = await File.ReadAllTextAsync(Path.Combine(_outputDirectory, "pipeline-trace.json"));
        var trace = JsonSerializer.Deserialize<PipelineTrace>(json, TraceWriter.TraceJsonSerializerOptions);

        trace.Should().NotBeNull();
        trace!.PipelineName.Should().Be("mcp-pipeline");
        trace.Steps.Should().HaveCount(2);
    }

    [Fact]
    public async Task WritesValidJsonToAiInteractionsFile()
    {
        var writer = new TraceWriter();

        await writer.WriteAsync(_outputDirectory, CreatePipelineTrace(), CreateAiInteractions());

        var json = await File.ReadAllTextAsync(Path.Combine(_outputDirectory, "ai-interactions.json"));
        var interactions = JsonSerializer.Deserialize<IReadOnlyList<AiInteraction>>(json, TraceWriter.TraceJsonSerializerOptions);

        interactions.Should().NotBeNull();
        interactions.Should().ContainSingle();
        interactions![0].Model.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task WritesMarkdownSummary()
    {
        var writer = new TraceWriter();

        await writer.WriteAsync(_outputDirectory, CreatePipelineTrace(), CreateAiInteractions());

        var summary = await File.ReadAllTextAsync(Path.Combine(_outputDirectory, "summary.md"));

        summary.Should().Contain("# Pipeline Trace Summary");
        summary.Should().Contain("## Steps (2)");
        summary.Should().Contain("## AI Statistics");
    }

    [Fact]
    public async Task UsesTempFilesThenRenameForAtomicWrite()
    {
        var writer = new TraceWriter();
        Directory.CreateDirectory(_outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "pipeline-trace.json"), "old");
        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "ai-interactions.json"), "old");
        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "summary.md"), "old");

        await writer.WriteAsync(_outputDirectory, CreatePipelineTrace(), CreateAiInteractions());

        (await File.ReadAllTextAsync(Path.Combine(_outputDirectory, "pipeline-trace.json"))).Should().NotBe("old");
        Directory.EnumerateFiles(_outputDirectory, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task WritesEmptyAiInteractionsArray_WhenNoAiCalls()
    {
        var writer = new TraceWriter();

        await writer.WriteAsync(_outputDirectory, CreatePipelineTrace(), Array.Empty<AiInteraction>());

        var json = await File.ReadAllTextAsync(Path.Combine(_outputDirectory, "ai-interactions.json"));
        var interactions = JsonSerializer.Deserialize<IReadOnlyList<AiInteraction>>(json, TraceWriter.TraceJsonSerializerOptions);

        interactions.Should().NotBeNull();
        interactions.Should().BeEmpty();
    }

    [Fact]
    public void BuildSummary_EscapesPipesInErrorList()
    {
        var trace = CreatePipelineTrace() with
        {
            Steps =
            [
                new TraceEvent
                {
                    SequenceNumber = 1,
                    StepName = "rewrite|tool",
                    StepType = StepClassification.AI,
                    Status = StepStatus.Failed,
                    TargetName = "azure|functions",
                    Error = "bad | input",
                    StartedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 2, TimeSpan.Zero),
                    EndedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 5, TimeSpan.Zero),
                    DurationMs = 3000
                }
            ]
        };

        var summary = TraceWriter.BuildSummary(trace, Array.Empty<AiInteraction>());

        summary.Should().Contain("- rewrite\\|tool: bad \\| input");
    }

    [Fact]
    public async Task WriteAsync_WhenOutputDirectoryIsAFile_ThrowsWithoutLeavingTempFiles()
    {
        var writer = new TraceWriter();
        var blockedPath = Path.Combine(_outputDirectory, "blocked-output");
        Directory.CreateDirectory(_outputDirectory);
        await File.WriteAllTextAsync(blockedPath, "not a directory");

        var act = async () => await writer.WriteAsync(blockedPath, CreatePipelineTrace(), CreateAiInteractions());

        await act.Should().ThrowAsync<IOException>();
        Directory.EnumerateFiles(_outputDirectory, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    private static PipelineTrace CreatePipelineTrace() => new()
    {
        PipelineName = "mcp-pipeline",
        RunId = "run-123",
        StartedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        EndedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 5, TimeSpan.Zero),
        DurationMs = 5000,
        Steps =
        [
            new TraceEvent
            {
                SequenceNumber = 1,
                StepName = "fetch",
                StepType = StepClassification.Deterministic,
                Status = StepStatus.Completed,
                TargetName = "azure-functions",
                StartedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EndedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero),
                DurationMs = 1000
            },
            new TraceEvent
            {
                SequenceNumber = 3,
                StepName = "rewrite",
                StepType = StepClassification.AI,
                Status = StepStatus.Failed,
                TargetName = "azure-functions",
                Error = "timeout",
                StartedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 2, TimeSpan.Zero),
                EndedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 5, TimeSpan.Zero),
                DurationMs = 3000
            }
        ]
    };

    private static IReadOnlyCollection<AiInteraction> CreateAiInteractions() =>
    [
        new AiInteraction
        {
            SequenceNumber = 2,
            SkillOrToolName = "rewrite",
            Operation = "chat",
            SystemPrompt = "system",
            UserPrompt = "user",
            ResponseContent = "response",
            Model = "gpt-4o-mini",
            TotalTokens = 128,
            DurationMs = 2200,
            RetryCount = 0,
            Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 3, TimeSpan.Zero)
        }
    ];
}
