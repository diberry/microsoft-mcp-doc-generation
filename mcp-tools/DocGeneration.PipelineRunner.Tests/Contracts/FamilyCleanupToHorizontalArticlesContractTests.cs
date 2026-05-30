using Shared;
using Xunit;

namespace DocGeneration.PipelineRunner.Tests.Contracts;

[Trait("Category", "Contract")]
public class FamilyCleanupToHorizontalArticlesContractTests : IDisposable
{
    private readonly string _fixtureDir;

    public FamilyCleanupToHorizontalArticlesContractTests()
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
            Step = "Generate tool-family article",
            StepName = "step-4-generate-tool-family-article",
            Namespace = "azure-monitor",
            OutputFileCount = 1,
            DurationMs = 3100,
            Timestamp = "2026-05-29T10:10:00Z",
            OutputArtifacts =
            [
                new ArtifactReference { Path = "tool-family/azure-monitor.md", Sha256 = "abc123" }
            ]
        };
        StepResultWriter.Write(_fixtureDir, envelope);

        var success = StepResultReader.TryRead(_fixtureDir, out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal("1.0", result!.SchemaVersion);
        Assert.Equal("step-4-generate-tool-family-article", result.StepName);
        Assert.Equal(StepResultStatus.Success, result.Status);
        Assert.NotNull(result.OutputArtifacts);
        Assert.Single(result.OutputArtifacts!);
        Assert.Equal("tool-family/azure-monitor.md", result.OutputArtifacts[0].Path);
    }

    [Fact]
    public void MismatchedSchemaVersion_ThrowsStepResultSchemaException()
    {
        var json = """
        {
            "version": 3,
            "schemaVersion": "99.0",
            "status": "success",
            "step": "Generate tool-family article",
            "stepName": "step-4-generate-tool-family-article",
            "namespace": "azure-monitor",
            "outputFileCount": 1
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
            "step": "Generate tool-family article",
            "namespace": "azure-monitor",
            "outputFileCount": 1,
            "warnings": [],
            "errors": [],
            "duration": "00:03:05.000"
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
