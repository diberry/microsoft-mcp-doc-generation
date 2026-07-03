using Shared;
using Xunit;

namespace DocGeneration.PipelineRunner.Tests.Contracts;

[Trait("Category", "Contract")]
public class AnnotationsToToolGenerationContractTests : IDisposable
{
    private readonly string _fixtureDir;

    public AnnotationsToToolGenerationContractTests()
    {
        _fixtureDir = Path.Combine(Path.GetTempPath(), $"contract-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_fixtureDir);
    }

    [Fact]
    public void ValidUpstreamEnvelope_DeserializesSuccessfully()
    {
        var envelope = new StepResultFile
        {
            Version = 3,
            SchemaVersion = "1.0",
            Status = StepResultStatus.Success,
            Step = "Generate annotations, parameters, and raw tools",
            StepName = "step-1-generate-annotations-parameters-and-raw-tools",
            Namespace = "azure-monitor",
            OutputFileCount = 3,
            DurationMs = 1200,
            Timestamp = "2026-05-29T10:00:00Z",
            OutputArtifacts =
            [
                new ArtifactReference { Path = "annotations/azure-monitor-list-workspaces-annotations.md", Sha256 = "abc123" },
                new ArtifactReference { Path = "parameters/azure-monitor-list-workspaces-parameters.md", Sha256 = "def456" },
                new ArtifactReference { Path = "tools-raw/azure-monitor-list-workspaces.md", Sha256 = "ghi789" }
            ]
        };
        StepResultWriter.Write(_fixtureDir, envelope);

        var success = StepResultReader.TryRead(_fixtureDir, out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal("1.0", result!.SchemaVersion);
        Assert.Equal("step-1-generate-annotations-parameters-and-raw-tools", result.StepName);
        Assert.Equal(StepResultStatus.Success, result.Status);
        Assert.NotNull(result.OutputArtifacts);
        Assert.Equal(3, result.OutputArtifacts!.Count);
    }

    [Fact]
    public void MismatchedSchemaVersion_ThrowsStepResultSchemaException()
    {
        var json = """
        {
            "version": 3,
            "schemaVersion": "99.0",
            "status": "success",
            "step": "Generate annotations, parameters, and raw tools",
            "stepName": "step-1-generate-annotations-parameters-and-raw-tools",
            "namespace": "azure-monitor",
            "outputFileCount": 3
        }
        """;
        File.WriteAllText(Path.Combine(_fixtureDir, "step-result.json"), json);

        Assert.Throws<StepResultSchemaException>(() => StepResultReader.TryRead(_fixtureDir, out _));
    }

    [Fact]
    public void LegacyV0Envelope_DeserializesWithoutException()
    {
        var json = """
        {
            "version": 1,
            "status": "success",
            "step": "Generate annotations, parameters, and raw tools",
            "namespace": "azure-monitor",
            "outputFileCount": 3,
            "warnings": [],
            "errors": [],
            "duration": "00:01:12.000"
        }
        """;
        File.WriteAllText(Path.Combine(_fixtureDir, "step-result.json"), json);

        var success = StepResultReader.TryRead(_fixtureDir, out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Null(result!.SchemaVersion);
        Assert.Null(result.StepName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_fixtureDir))
        {
            Directory.Delete(_fixtureDir, recursive: true);
        }
    }
}
