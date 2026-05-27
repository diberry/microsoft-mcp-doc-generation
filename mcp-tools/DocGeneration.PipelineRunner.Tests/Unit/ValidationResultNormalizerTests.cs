using System.Text.Json;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Direct unit tests for <see cref="ValidationResultNormalizer"/>.
/// Covers paths not exercised through the step integration tests:
/// stale timestamps, missing fields, unrecognized verdicts, etc.
/// </summary>
public class ValidationResultNormalizerTests
{
    private const string ExpectedRunId = "test-run-id-123";
    private const string ExpectedNamespace = "storage";

    // ── Script error path ────────────────────────────────────────────────────

    [Fact]
    public void Normalize_ScriptFailedNoArtifact_ReturnsScriptError()
    {
        var scriptResult = new ValidationScriptResult(
            ExitCode: 1,
            OutputJsonPath: Path.Combine(Path.GetTempPath(), "nonexistent.json"),
            StdOut: "",
            StdErr: "pwsh: script crashed",
            JsonArtifactExists: false,
            StartedAt: DateTimeOffset.UtcNow.AddSeconds(-5),
            CompletedAt: DateTimeOffset.UtcNow);

        var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

        Assert.Equal(ValidationVerdict.ScriptError, result.Verdict);
        Assert.Contains(result.Diagnostics, d => d.Contains("exit") || d.Contains("code"));
        Assert.Contains(result.Diagnostics, d => d.Contains("pwsh: script crashed"));
    }

    [Fact]
    public void Normalize_ScriptSucceededNoArtifact_ReturnsArtifactError()
    {
        var scriptResult = new ValidationScriptResult(
            ExitCode: 0,
            OutputJsonPath: Path.Combine(Path.GetTempPath(), "missing.json"),
            StdOut: "",
            StdErr: "",
            JsonArtifactExists: false,
            StartedAt: DateTimeOffset.UtcNow.AddSeconds(-2),
            CompletedAt: DateTimeOffset.UtcNow);

        var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

        Assert.Equal(ValidationVerdict.ArtifactError, result.Verdict);
        Assert.Contains(result.Diagnostics, d => d.Contains("not written") || d.Contains("artifact"));
    }

    // ── Malformed JSON ───────────────────────────────────────────────────────

    [Fact]
    public void Normalize_MalformedJson_ReturnsArtifactError()
    {
        var artifactPath = WriteTempArtifact("NOT VALID JSON {{{");
        try
        {
            var scriptResult = CreateSuccessResult(artifactPath);
            var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

            Assert.Equal(ValidationVerdict.ArtifactError, result.Verdict);
            Assert.Contains(result.Diagnostics, d => d.Contains("malformed"));
        }
        finally { CleanupTempFile(artifactPath); }
    }

    // ── Missing schemaVersion ────────────────────────────────────────────────

    [Fact]
    public void Normalize_MissingSchemaVersion_ReturnsArtifactError()
    {
        var json = JsonSerializer.Serialize(new
        {
            runId = ExpectedRunId,
            @namespace = ExpectedNamespace,
            generatedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            verdict = "pass"
        });
        var artifactPath = WriteTempArtifact(json);
        try
        {
            var scriptResult = CreateSuccessResult(artifactPath);
            var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

            Assert.Equal(ValidationVerdict.ArtifactError, result.Verdict);
            Assert.Contains(result.Diagnostics, d => d.Contains("schemaVersion"));
        }
        finally { CleanupTempFile(artifactPath); }
    }

    // ── RunId mismatch ───────────────────────────────────────────────────────

    [Fact]
    public void Normalize_RunIdMismatch_ReturnsArtifactError()
    {
        var json = BuildArtifactJson("wrong-run-id", ExpectedNamespace, "pass");
        var artifactPath = WriteTempArtifact(json);
        try
        {
            var scriptResult = CreateSuccessResult(artifactPath);
            var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

            Assert.Equal(ValidationVerdict.ArtifactError, result.Verdict);
            Assert.Contains(result.Diagnostics, d => d.Contains("mismatch") || d.Contains("Run-id"));
        }
        finally { CleanupTempFile(artifactPath); }
    }

    // ── Stale timestamp ──────────────────────────────────────────────────────

    [Fact]
    public void Normalize_StaleTimestamp_ReturnsArtifactError()
    {
        var staleTime = DateTimeOffset.UtcNow.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var json = BuildArtifactJsonWithTimestamp(ExpectedRunId, ExpectedNamespace, "pass", staleTime);
        var artifactPath = WriteTempArtifact(json);
        try
        {
            var scriptResult = CreateSuccessResult(artifactPath);
            var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

            Assert.Equal(ValidationVerdict.ArtifactError, result.Verdict);
            Assert.Contains(result.Diagnostics, d => d.Contains("stale"));
        }
        finally { CleanupTempFile(artifactPath); }
    }

    [Fact]
    public void Normalize_FreshTimestamp_DoesNotRejectAsStale()
    {
        var freshTime = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var json = BuildArtifactJsonWithTimestamp(ExpectedRunId, ExpectedNamespace, "pass", freshTime);
        var artifactPath = WriteTempArtifact(json);
        try
        {
            var scriptResult = CreateSuccessResult(artifactPath);
            var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

            Assert.Equal(ValidationVerdict.Pass, result.Verdict);
        }
        finally { CleanupTempFile(artifactPath); }
    }

    // ── Missing generatedAt accepted (no stale check) ────────────────────────

    [Fact]
    public void Normalize_MissingGeneratedAt_AcceptsArtifact()
    {
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            runId = ExpectedRunId,
            @namespace = ExpectedNamespace,
            verdict = "pass"
        });
        var artifactPath = WriteTempArtifact(json);
        try
        {
            var scriptResult = CreateSuccessResult(artifactPath);
            var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

            Assert.Equal(ValidationVerdict.Pass, result.Verdict);
        }
        finally { CleanupTempFile(artifactPath); }
    }

    // ── Missing verdict field ────────────────────────────────────────────────

    [Fact]
    public void Normalize_MissingVerdict_ReturnsArtifactError()
    {
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            runId = ExpectedRunId,
            @namespace = ExpectedNamespace,
            generatedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        });
        var artifactPath = WriteTempArtifact(json);
        try
        {
            var scriptResult = CreateSuccessResult(artifactPath);
            var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

            Assert.Equal(ValidationVerdict.ArtifactError, result.Verdict);
            Assert.Contains(result.Diagnostics, d => d.Contains("verdict"));
        }
        finally { CleanupTempFile(artifactPath); }
    }

    // ── Unrecognized verdict value ───────────────────────────────────────────

    [Fact]
    public void Normalize_UnrecognizedVerdict_ReturnsArtifactError()
    {
        var json = BuildArtifactJson(ExpectedRunId, ExpectedNamespace, "unknown-value");
        var artifactPath = WriteTempArtifact(json);
        try
        {
            var scriptResult = CreateSuccessResult(artifactPath);
            var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

            Assert.Equal(ValidationVerdict.ArtifactError, result.Verdict);
            Assert.Contains(result.Diagnostics, d => d.Contains("unrecognized"));
        }
        finally { CleanupTempFile(artifactPath); }
    }

    // ── Valid verdicts ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("pass", ValidationVerdict.Pass)]
    [InlineData("warn", ValidationVerdict.Warn)]
    [InlineData("fail", ValidationVerdict.Fail)]
    public void Normalize_ValidVerdict_MapsCorrectly(string scriptVerdict, ValidationVerdict expected)
    {
        var json = BuildArtifactJson(ExpectedRunId, ExpectedNamespace, scriptVerdict);
        var artifactPath = WriteTempArtifact(json);
        try
        {
            var scriptResult = CreateSuccessResult(artifactPath);
            var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

            Assert.Equal(expected, result.Verdict);
            Assert.Equal(scriptVerdict, result.ScriptVerdict);
        }
        finally { CleanupTempFile(artifactPath); }
    }

    // ── Checks diagnostics are collected ─────────────────────────────────────

    [Fact]
    public void Normalize_WarnChecks_CollectedInDiagnostics()
    {
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            runId = ExpectedRunId,
            @namespace = ExpectedNamespace,
            generatedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            verdict = "warn",
            checks = new[]
            {
                new { name = "frontmatter.ms.date", status = "pass", detail = "" },
                new { name = "tokens.placeholder-detected", status = "warn", detail = "Found 2 placeholders" },
            }
        });
        var artifactPath = WriteTempArtifact(json);
        try
        {
            var scriptResult = CreateSuccessResult(artifactPath);
            var result = ValidationResultNormalizer.Normalize(scriptResult, ExpectedRunId, ExpectedNamespace);

            Assert.Equal(ValidationVerdict.Warn, result.Verdict);
            Assert.Contains(result.Diagnostics, d => d.Contains("WARN") && d.Contains("tokens.placeholder-detected"));
        }
        finally { CleanupTempFile(artifactPath); }
    }

    // ── Multi-file path in ValidationScriptRunner ────────────────────────────

    [Fact]
    public async Task ValidationScriptRunner_MultipleArticles_UsesArticlesDir()
    {
        var recorder = new RecordingProcessRunner();
        var runner = new ValidationScriptRunner(recorder);
        var tempDir = Path.Combine(Path.GetTempPath(), $"vsr-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var articlesDir = Path.Combine(tempDir, "articles");
        Directory.CreateDirectory(articlesDir);
        File.WriteAllText(Path.Combine(articlesDir, "a.md"), "# A");
        File.WriteAllText(Path.Combine(articlesDir, "b.md"), "# B");

        try
        {
            var request = new ValidationScriptRequest(
                ScriptPath: "Test-ArticleHealth.ps1",
                RunId: "run-123",
                Namespace: "test",
                RepoRoot: tempDir,
                OutputRoot: tempDir,
                OutputJsonPath: Path.Combine(tempDir, "output.json"),
                ArticlePaths: [Path.Combine(articlesDir, "a.md"), Path.Combine(articlesDir, "b.md")],
                AdditionalArguments: new Dictionary<string, string>());

            await runner.RunAsync(request, CancellationToken.None);

            // Verify that -ArticlesDir was used (not -ArticlePath)
            var lastCall = recorder.Invocations.Last();
            Assert.Contains("-ArticlesDir", lastCall.Arguments.ToList());
            Assert.DoesNotContain("-ArticlePath", lastCall.Arguments.ToList());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ValidationScriptRunner_SingleArticle_UsesArticlePath()
    {
        var recorder = new RecordingProcessRunner();
        var runner = new ValidationScriptRunner(recorder);
        var tempDir = Path.Combine(Path.GetTempPath(), $"vsr-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var articlePath = Path.Combine(tempDir, "storage.md");
        File.WriteAllText(articlePath, "# Storage");

        try
        {
            var request = new ValidationScriptRequest(
                ScriptPath: "Test-ArticleHealth.ps1",
                RunId: "run-456",
                Namespace: "storage",
                RepoRoot: tempDir,
                OutputRoot: tempDir,
                OutputJsonPath: Path.Combine(tempDir, "output.json"),
                ArticlePaths: [articlePath],
                AdditionalArguments: new Dictionary<string, string>());

            await runner.RunAsync(request, CancellationToken.None);

            var lastCall = recorder.Invocations.Last();
            Assert.Contains("-ArticlePath", lastCall.Arguments.ToList());
            Assert.DoesNotContain("-ArticlesDir", lastCall.Arguments.ToList());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildArtifactJson(string runId, string ns, string verdict)
    {
        return BuildArtifactJsonWithTimestamp(runId, ns, verdict,
            DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }

    private static string BuildArtifactJsonWithTimestamp(string runId, string ns, string verdict, string generatedAt)
    {
        var obj = new
        {
            schemaVersion = "1.0",
            runId,
            @namespace = ns,
            generatedAt,
            verdict,
            articleFiles = new[] { "storage.md" },
            filesChecked = 1,
            summary = new { pass = 5, warn = 0, fail = 0 },
            checks = new[]
            {
                new { name = "frontmatter.ms.date", status = "pass", detail = "" },
            }
        };
        return JsonSerializer.Serialize(obj);
    }

    private static ValidationScriptResult CreateSuccessResult(string artifactPath)
    {
        return new ValidationScriptResult(
            ExitCode: 0,
            OutputJsonPath: artifactPath,
            StdOut: "",
            StdErr: "",
            JsonArtifactExists: true,
            StartedAt: DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAt: DateTimeOffset.UtcNow);
    }

    private static string WriteTempArtifact(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"normalizer-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        return path;
    }

    private static void CleanupTempFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
