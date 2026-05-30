using Shared;
using Xunit;

namespace DocGeneration.PipelineRunner.Tests.Contracts;

[Trait("Category", "Contract")]
public class ToolGenerationToFamilyCleanupContractTests : IDisposable
{
    private readonly string _fixtureDir;

    public ToolGenerationToFamilyCleanupContractTests()
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
            Step = "Compose and improve tool files",
            StepName = "step-3-compose-and-improve-tool-files",
            Namespace = "azure-monitor",
            OutputFileCount = 2,
            DurationMs = 2400,
            Timestamp = "2026-05-29T10:05:00Z",
            OutputArtifacts =
            [
                new ArtifactReference { Path = "tools/azure-monitor-list-workspaces.md", Sha256 = "abc123" },
                new ArtifactReference { Path = "tools/azure-monitor-query-logs.md", Sha256 = "def456" }
            ]
        };
        StepResultWriter.Write(_fixtureDir, envelope);

        var success = StepResultReader.TryRead(_fixtureDir, out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal("1.0", result!.SchemaVersion);
        Assert.Equal("step-3-compose-and-improve-tool-files", result.StepName);
        Assert.Equal(StepResultStatus.Success, result.Status);
        Assert.NotNull(result.OutputArtifacts);
        Assert.Equal(2, result.OutputArtifacts!.Count);
        Assert.All(result.OutputArtifacts, artifact => Assert.StartsWith("tools/", artifact.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void MismatchedSchemaVersion_ThrowsStepResultSchemaException()
    {
        var json = """
        {
            "version": 3,
            "schemaVersion": "99.0",
            "status": "success",
            "step": "Compose and improve tool files",
            "stepName": "step-3-compose-and-improve-tool-files",
            "namespace": "azure-monitor",
            "outputFileCount": 2
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
            "step": "Compose and improve tool files",
            "namespace": "azure-monitor",
            "outputFileCount": 2,
            "warnings": [],
            "errors": [],
            "duration": "00:02:15.000"
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
