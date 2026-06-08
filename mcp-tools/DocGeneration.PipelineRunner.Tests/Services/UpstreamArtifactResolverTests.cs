using PipelineRunner.Services;
using Shared;
using Xunit;

namespace PipelineRunner.Tests.Services;

public sealed class UpstreamArtifactResolverTests : IDisposable
{
    private readonly string _testRoot;
    private readonly UpstreamArtifactResolver _resolver = new();

    public UpstreamArtifactResolverTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"upstream-artifact-resolver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public void TryReadUpstream_ReturnsEnvelope_WhenStepResultExists()
    {
        var upstreamDir = Path.Combine(_testRoot, "step-3-compose-and-improve-tool-files");
        StepResultWriter.Write(upstreamDir, new StepResultFile
        {
            SchemaVersion = "1.0",
            Status = StepResultStatus.Success,
            Step = "Compose and improve tool files",
            StepName = "step-3-compose-and-improve-tool-files",
            Namespace = "compute",
            OutputArtifacts =
            [
                new ArtifactReference { Path = "tools/compute-list.md", Sha256 = "abc123" }
            ]
        });

        var result = _resolver.TryReadUpstream(_testRoot, 3, "compose-and-improve-tool-files");

        Assert.NotNull(result);
        Assert.Equal("1.0", result!.SchemaVersion);
        Assert.Single(result.OutputArtifacts!);
    }

    [Fact]
    public void TryReadUpstream_ReturnsNull_WhenDirectoryDoesNotExist()
    {
        var result = _resolver.TryReadUpstream(_testRoot, 9, "missing-step");

        Assert.Null(result);
    }

    [Fact]
    public void TryReadUpstream_ReturnsNull_WhenStepResultIsMissing()
    {
        Directory.CreateDirectory(Path.Combine(_testRoot, "step-2-generate-example-prompts"));

        var result = _resolver.TryReadUpstream(_testRoot, 2, "generate-example-prompts");

        Assert.Null(result);
    }

    [Fact]
    public void TryReadUpstream_ThrowsStepResultSchemaException_OnVersionMismatch()
    {
        var upstreamDir = Path.Combine(_testRoot, "step-1-generate-annotations-parameters-and-raw-tools");
        Directory.CreateDirectory(upstreamDir);
        File.WriteAllText(
            Path.Combine(upstreamDir, StepResultWriter.FileName),
            """
            {
              "version": 3,
              "schemaVersion": "9.9",
              "status": "success",
              "step": "Generate annotations, parameters, and raw tools",
              "namespace": "compute"
            }
            """);

        var ex = Assert.Throws<StepResultSchemaException>(
            () => _resolver.TryReadUpstream(_testRoot, 1, "generate-annotations-parameters-and-raw-tools"));

        Assert.Equal("9.9", ex.ActualVersion);
    }
}
