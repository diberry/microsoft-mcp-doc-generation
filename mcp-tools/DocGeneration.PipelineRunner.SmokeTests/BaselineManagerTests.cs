using Xunit;

namespace DocGeneration.PipelineRunner.SmokeTests;

public class BaselineManagerTests
{
    [Fact]
    public void FindGeneratedDirectory_PrefersNewestTimestampedDirectory()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"baseline-manager-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoRoot);

        try
        {
            var explicitDir = Path.Combine(repoRoot, "generated-quota");
            Directory.CreateDirectory(explicitDir);
            Directory.SetLastWriteTimeUtc(explicitDir, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            Directory.CreateDirectory(Path.Combine(repoRoot, "generated-quota-20990101T000001000Z"));
            Directory.CreateDirectory(Path.Combine(repoRoot, "generated-quota-20990101T000002000Z"));

            var resolved = BaselineManager.FindGeneratedDirectory(repoRoot, "quota");

            Assert.NotNull(resolved);
            Assert.EndsWith("generated-quota-20990101T000002000Z", resolved, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(repoRoot, true);
        }
    }

    [Fact]
    public void CompareWithBaseline_UsesTimestampedGeneratedDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"baseline-compare-{Guid.NewGuid():N}");
        var testProjectRoot = Path.Combine(root, "tests");
        var repoRoot = Path.Combine(root, "repo");
        Directory.CreateDirectory(testProjectRoot);
        Directory.CreateDirectory(repoRoot);

        try
        {
            var baselineAnnotationsDir = Path.Combine(testProjectRoot, "Baselines", "quota", "annotations");
            Directory.CreateDirectory(baselineAnnotationsDir);
            File.WriteAllText(Path.Combine(baselineAnnotationsDir, "quota-annotations.md"), "same-content");

            var generatedAnnotationsDir = Path.Combine(repoRoot, "generated-quota-20990101T000001000Z", "annotations");
            Directory.CreateDirectory(generatedAnnotationsDir);
            File.WriteAllText(Path.Combine(generatedAnnotationsDir, "quota-annotations.md"), "same-content");

            var result = BaselineManager.CompareWithBaseline(testProjectRoot, repoRoot, "quota");

            Assert.True(result.Success, result.GetSummary());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
