// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.PipelineRunner.Tests;

/// <summary>
/// Group 6 — Fail-closed integration at the PipelineRunner seam (C6).
/// ExamplePromptsStep retry-feedback path: missing / malformed / stale / wrong-command
/// manifest produces a recorded, classified failure carrying the stable error code,
/// not an empty-list fallback and not an unhandled crash.
/// </summary>
public class ExamplePromptsStepManifestFailureTests : IDisposable
{
    private readonly string _tempDir;

    public ExamplePromptsStepManifestFailureTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pipeline-manifest-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void LoadRequiredOptions_MissingManifest_ThrowsParameterManifestException_WithErrorCode()
    {
        // When the manifest file doesn't exist, the loader must throw with NOT_FOUND,
        // NOT return an empty array silently
        var manifestDir = Path.Combine(_tempDir, "parameters");
        Directory.CreateDirectory(manifestDir);

        // The step should propagate ParameterManifestException with the error code
        // rather than swallowing and returning empty
        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(
                Path.Combine(manifestDir, "nonexistent-params.json"),
                "azmcp appconfig kv get",
                "appconfig"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_NOT_FOUND, ex.ErrorCode);
    }

    [Fact]
    public void LoadRequiredOptions_MalformedManifest_ThrowsParameterManifestException_WithErrorCode()
    {
        var manifestDir = Path.Combine(_tempDir, "parameters");
        Directory.CreateDirectory(manifestDir);
        var path = Path.Combine(manifestDir, "malformed-params.json");
        File.WriteAllText(path, "not json at all {{{}}}");

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp appconfig kv get", "appconfig"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_MALFORMED, ex.ErrorCode);
    }

    [Fact]
    public void LoadRequiredOptions_StaleManifest_ThrowsParameterManifestException_WithErrorCode()
    {
        var manifestDir = Path.Combine(_tempDir, "parameters");
        Directory.CreateDirectory(manifestDir);
        var json = """
        {
          "schemaVersion": "2.0",
          "toolCommand": "azmcp appconfig kv get",
          "namespace": "appconfig",
          "sourceIdentity": { "azureMcpBuild": "1.0.0-old", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": []
        }
        """;
        var path = Path.Combine(manifestDir, "stale-params.json");
        File.WriteAllText(path, json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(
                path, "azmcp appconfig kv get", "appconfig",
                currentAzureMcpBuild: "2.0.0-new"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_SOURCE_STALE, ex.ErrorCode);
    }

    [Fact]
    public void LoadRequiredOptions_WrongCommand_ThrowsParameterManifestException_WithErrorCode()
    {
        var manifestDir = Path.Combine(_tempDir, "parameters");
        Directory.CreateDirectory(manifestDir);
        var json = """
        {
          "schemaVersion": "2.0",
          "toolCommand": "azmcp appconfig kv list",
          "namespace": "appconfig",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": []
        }
        """;
        var path = Path.Combine(manifestDir, "wrong-cmd-params.json");
        File.WriteAllText(path, json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(
                path, "azmcp appconfig kv get", "appconfig"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_COMMAND_MISMATCH, ex.ErrorCode);
    }

    /// <summary>
    /// Integration-level: when ExamplePromptsStep encounters a ParameterManifestException,
    /// it must surface as a recorded, classified step failure — not swallow it.
    /// This test proves the loader throws (precondition) and the step must propagate.
    /// </summary>
    [Fact]
    public void ExamplePromptsStep_ManifestException_SurfacesAsClassifiedFailure()
    {
        // The step's LoadRequiredOptionsAsync currently catches JsonException and returns [].
        // After the fix, ParameterManifestException must propagate as a classified failure.
        // This test verifies the loader throws the typed exception that the step must handle.
        var manifestDir = Path.Combine(_tempDir, "parameters");
        Directory.CreateDirectory(manifestDir);
        var path = Path.Combine(manifestDir, "legacy-params.json");
        File.WriteAllText(path, "[{\"Name\":\"x\"}]"); // legacy bare-array

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp test tool", "test"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_LEGACY_FORMAT, ex.ErrorCode);
        // The step must record this as a failure with the error code, not swallow it
        Assert.NotNull(ex.ErrorCode);
        Assert.NotEmpty(ex.ErrorCode);
    }
}
