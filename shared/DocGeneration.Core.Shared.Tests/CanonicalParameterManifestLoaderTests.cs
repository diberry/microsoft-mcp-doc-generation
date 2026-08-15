// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Core.Shared.Tests;

/// <summary>
/// Group 2 — Strict loader fails closed (one named test per stable error code).
/// Each test asserts: typed ParameterManifestException, the exact ErrorCode constant,
/// the manifest path on the exception, and actionable diagnostic message.
/// The loader NEVER returns null and NEVER returns an empty fallback.
/// </summary>
public class CanonicalParameterManifestLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public CanonicalParameterManifestLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"manifest-loader-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteTempManifest(string filename, string content)
    {
        var path = Path.Combine(_tempDir, filename);
        File.WriteAllText(path, content);
        return path;
    }

    // ── PARAM_MANIFEST_NOT_FOUND ─────────────────────────────────────

    [Fact]
    public void Load_MissingFile_ThrowsNotFound()
    {
        var path = Path.Combine(_tempDir, "nonexistent-params.json");

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp storage account list", "storage"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_NOT_FOUND, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── PARAM_MANIFEST_MALFORMED ─────────────────────────────────────

    [Fact]
    public void Load_MalformedJson_ThrowsMalformed()
    {
        var path = WriteTempManifest("malformed-params.json", "{ this is not valid json }}}");

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp storage account list", "storage"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_MALFORMED, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── PARAM_MANIFEST_SCHEMA_UNKNOWN ────────────────────────────────

    [Fact]
    public void Load_UnknownSchemaVersion_ThrowsSchemaUnknown()
    {
        var json = """
        {
          "schemaVersion": "3.0",
          "toolCommand": "azmcp storage account list",
          "namespace": "storage",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": []
        }
        """;
        var path = WriteTempManifest("schema-unknown-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp storage account list", "storage"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_SCHEMA_UNKNOWN, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("3.0", ex.Message);
        Assert.Contains("2.0", ex.Message);
    }

    // ── PARAM_MANIFEST_LEGACY_FORMAT ─────────────────────────────────

    [Fact]
    public void Load_LegacyBareArray_ThrowsLegacyFormat()
    {
        // Legacy format is a bare JSON array (pre-v2)
        var json = """
        [
          { "Name": "account", "DisplayName": "Account name", "Required": true, "RequiredText": "Required", "Description": "The account." }
        ]
        """;
        var path = WriteTempManifest("legacy-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp appconfig account list", "appconfig"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_LEGACY_FORMAT, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("legacy", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rerun Step 1", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates that a frozen beta.34 legacy bare-array manifest (real format from the baseline)
    /// is rejected with PARAM_MANIFEST_LEGACY_FORMAT. Uses representative content — does NOT
    /// modify anything under DocGeneration.Baseline.Beta34.Tests/Fixtures/.
    /// </summary>
    [Fact]
    public void Load_FrozenBeta34LegacyManifest_ThrowsLegacyFormat()
    {
        // Representative beta.34 legacy manifest shape (bare array, no envelope)
        var legacyJson = """
        [
          { "Name": "account", "DisplayName": "Account name", "Required": true, "RequiredText": "Required", "IsConditionalRequired": false, "Description": "The name of the App Configuration account." },
          { "Name": "key", "DisplayName": "Key", "Required": true, "RequiredText": "Required", "IsConditionalRequired": false, "Description": "The key to retrieve." },
          { "Name": "label", "DisplayName": "Label", "Required": false, "RequiredText": "Optional", "IsConditionalRequired": false, "Description": "The label filter." }
        ]
        """;
        var path = WriteTempManifest("beta34-legacy-params.json", legacyJson);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp appconfig kv get", "appconfig"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_LEGACY_FORMAT, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
    }

    // ── PARAM_MANIFEST_COMMAND_MISMATCH ──────────────────────────────

    [Fact]
    public void Load_CommandMismatch_ThrowsCommandMismatch()
    {
        var json = BuildValidManifest("azmcp keyvault secret list", "keyvault");
        var path = WriteTempManifest("cmd-mismatch-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp keyvault key list", "keyvault"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_COMMAND_MISMATCH, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("azmcp keyvault secret list", ex.Message);
        Assert.Contains("azmcp keyvault key list", ex.Message);
    }

    // ── PARAM_MANIFEST_NAMESPACE_MISMATCH ────────────────────────────

    [Fact]
    public void Load_NamespaceMismatch_ThrowsNamespaceMismatch()
    {
        var json = BuildValidManifest("azmcp cosmos container list", "cosmos");
        var path = WriteTempManifest("ns-mismatch-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp cosmos container list", "storage"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_NAMESPACE_MISMATCH, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("cosmos", ex.Message);
        Assert.Contains("storage", ex.Message);
    }

    // ── PARAM_MANIFEST_SOURCE_STALE ──────────────────────────────────

    [Fact]
    public void Load_StaleBuild_ThrowsSourceStale()
    {
        var json = BuildValidManifest("azmcp monitor webtests get", "monitor", azureMcpBuild: "1.0.0-old");
        var path = WriteTempManifest("stale-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(
                path, "azmcp monitor webtests get", "monitor",
                currentAzureMcpBuild: "2.0.0-new"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_SOURCE_STALE, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("1.0.0-old", ex.Message);
        Assert.Contains("2.0.0-new", ex.Message);
        Assert.Contains("Rerun Step 1", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── C4: Nullable checks — namespace check skipped when null ───────

    [Fact]
    public void Load_NullExpectedNamespace_SkipsNamespaceCheck()
    {
        var json = BuildValidManifest("azmcp cosmos container list", "cosmos");
        var path = WriteTempManifest("null-ns-params.json", json);

        // Should not throw — namespace check is skipped when expectedNamespace is null
        var manifest = CanonicalParameterManifestLoader.Load(
            path, "azmcp cosmos container list", expectedNamespace: null);

        Assert.NotNull(manifest);
    }

    [Fact]
    public void Load_NullCurrentBuild_SkipsStalenessCheck()
    {
        var json = BuildValidManifest("azmcp speech recognize", "speech", azureMcpBuild: "1.0.0-old");
        var path = WriteTempManifest("null-build-params.json", json);

        // Should not throw — staleness check is skipped when currentAzureMcpBuild is null
        var manifest = CanonicalParameterManifestLoader.Load(
            path, "azmcp speech recognize", "speech",
            currentAzureMcpBuild: null);

        Assert.NotNull(manifest);
    }

    // ── PARAM_MANIFEST_EMPTY_PARAMS (C5) ─────────────────────────────

    [Fact]
    public void Load_EmptyParams_WithRequireNonEmpty_ThrowsEmptyParams()
    {
        var json = BuildValidManifest("azmcp aks cluster list", "aks");
        var path = WriteTempManifest("empty-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(
                path, "azmcp aks cluster list", "aks",
                requireNonEmptyParameters: true));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_EMPTY_PARAMS, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("empty parameters", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_EmptyParams_WithoutRequireNonEmpty_Succeeds()
    {
        var json = BuildValidManifest("azmcp aks cluster list", "aks");
        var path = WriteTempManifest("empty-params-ok.json", json);

        // Default is requireNonEmptyParameters = false — empty array is valid
        var manifest = CanonicalParameterManifestLoader.Load(
            path, "azmcp aks cluster list", "aks");

        Assert.NotNull(manifest);
    }

    // ── PARAM_MANIFEST_EMPTY_ALIAS ───────────────────────────────────

    [Fact]
    public void Load_EmptyAlias_ThrowsEmptyAlias()
    {
        var json = """
        {
          "schemaVersion": "2.0",
          "toolCommand": "azmcp sql db list",
          "namespace": "sql",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": [
            {
              "canonicalName": "server",
              "displayName": "Server name",
              "displayAliases": ["server-name", ""],
              "placeholderAliases": ["server", "server_name"],
              "required": true,
              "requiredText": "Required",
              "isConditionalRequired": false,
              "description": "The SQL server."
            }
          ]
        }
        """;
        var path = WriteTempManifest("empty-alias-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp sql db list", "sql"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_EMPTY_ALIAS, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("server", ex.Message);
    }

    // ── PARAM_MANIFEST_DUPLICATE_CANONICAL ───────────────────────────

    [Fact]
    public void Load_DuplicateCanonical_ThrowsDuplicateCanonical()
    {
        var json = """
        {
          "schemaVersion": "2.0",
          "toolCommand": "azmcp monitor alerts list",
          "namespace": "monitor",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": [
            {
              "canonicalName": "resource-group",
              "displayName": "Resource group",
              "displayAliases": ["resource-group"],
              "placeholderAliases": ["resource-group", "resource_group"],
              "required": true, "requiredText": "Required", "isConditionalRequired": false,
              "description": "The resource group."
            },
            {
              "canonicalName": "resource-group",
              "displayName": "Resource Group Name",
              "displayAliases": ["resource-group-name"],
              "placeholderAliases": ["resource-group-name"],
              "required": false, "requiredText": "Optional", "isConditionalRequired": false,
              "description": "Duplicate."
            }
          ]
        }
        """;
        var path = WriteTempManifest("dup-canonical-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp monitor alerts list", "monitor"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_DUPLICATE_CANONICAL, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("resource-group", ex.Message);
    }

    // ── PARAM_MANIFEST_ALIAS_COLLISION ───────────────────────────────

    [Fact]
    public void Load_AliasCollision_ThrowsAliasCollision()
    {
        var json = """
        {
          "schemaVersion": "2.0",
          "toolCommand": "azmcp storage blob list",
          "namespace": "storage",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": [
            {
              "canonicalName": "account",
              "displayName": "Account name",
              "displayAliases": ["account-name", "account"],
              "placeholderAliases": ["account", "account-name", "account_name"],
              "required": true, "requiredText": "Required", "isConditionalRequired": false,
              "description": "Storage account."
            },
            {
              "canonicalName": "container",
              "displayName": "Container name",
              "displayAliases": ["container-name", "account-name"],
              "placeholderAliases": ["container", "container_name"],
              "required": true, "requiredText": "Required", "isConditionalRequired": false,
              "description": "Blob container."
            }
          ]
        }
        """;
        var path = WriteTempManifest("alias-collision-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp storage blob list", "storage"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_ALIAS_COLLISION, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("account-name", ex.Message);
    }

    // ── PARAM_MANIFEST_ALIAS_SHADOWS_CANONICAL ───────────────────────

    [Fact]
    public void Load_AliasShadowsCanonical_ThrowsAliasShadowsCanonical()
    {
        var json = """
        {
          "schemaVersion": "2.0",
          "toolCommand": "azmcp eventhubs consumer list",
          "namespace": "eventhubs",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": [
            {
              "canonicalName": "eventhub",
              "displayName": "Event Hub name",
              "displayAliases": ["event-hub-name"],
              "placeholderAliases": ["eventhub", "event_hub"],
              "required": true, "requiredText": "Required", "isConditionalRequired": false,
              "description": "The Event Hub."
            },
            {
              "canonicalName": "consumer-group",
              "displayName": "Consumer group",
              "displayAliases": ["consumer-group", "eventhub"],
              "placeholderAliases": ["consumer-group", "consumer_group"],
              "required": true, "requiredText": "Required", "isConditionalRequired": false,
              "description": "Consumer group name."
            }
          ]
        }
        """;
        var path = WriteTempManifest("shadow-canonical-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp eventhubs consumer list", "eventhubs"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_ALIAS_SHADOWS_CANONICAL, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("eventhub", ex.Message);
    }

    // ── PARAM_MANIFEST_NORMALIZATION_COLLISION ────────────────────────

    [Fact]
    public void Load_NormalizationCollision_ThrowsNormalizationCollision()
    {
        var json = """
        {
          "schemaVersion": "2.0",
          "toolCommand": "azmcp loadtesting run list",
          "namespace": "loadtesting",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": [
            {
              "canonicalName": "test-run-id",
              "displayName": "Test run ID",
              "displayAliases": ["test-run-id"],
              "placeholderAliases": ["test-run-id", "test_run_id"],
              "required": true, "requiredText": "Required", "isConditionalRequired": false,
              "description": "ID of the test run."
            },
            {
              "canonicalName": "test_run_id",
              "displayName": "Test Run Identifier",
              "displayAliases": ["test-run-identifier"],
              "placeholderAliases": ["test_run_identifier"],
              "required": false, "requiredText": "Optional", "isConditionalRequired": false,
              "description": "Alternate test run id."
            }
          ]
        }
        """;
        var path = WriteTempManifest("norm-collision-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp loadtesting run list", "loadtesting"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_NORMALIZATION_COLLISION, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("test-run-id", ex.Message);
    }

    // ── PARAM_MANIFEST_PLACEHOLDER_MULTI_BIND ────────────────────────

    [Fact]
    public void Load_PlaceholderMultiBind_ThrowsPlaceholderMultiBind()
    {
        var json = """
        {
          "schemaVersion": "2.0",
          "toolCommand": "azmcp search index list",
          "namespace": "search",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": [
            {
              "canonicalName": "knowledge-base",
              "displayName": "Knowledge base",
              "displayAliases": ["knowledge-base"],
              "placeholderAliases": ["knowledge-base", "knowledge_base", "kb-name"],
              "required": true, "requiredText": "Required", "isConditionalRequired": false,
              "description": "The knowledge base."
            },
            {
              "canonicalName": "index-name",
              "displayName": "Index name",
              "displayAliases": ["index-name"],
              "placeholderAliases": ["index-name", "index_name", "kb-name"],
              "required": true, "requiredText": "Required", "isConditionalRequired": false,
              "description": "The search index."
            }
          ]
        }
        """;
        var path = WriteTempManifest("multi-bind-params.json", json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp search index list", "search"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_PLACEHOLDER_MULTI_BIND, ex.ErrorCode);
        Assert.Equal(path, ex.ManifestPath);
        Assert.Contains("kb-name", ex.Message);
    }

    // ── Loader never returns null, never returns empty fallback ───────

    [Fact]
    public void Load_ValidManifest_NeverReturnsNull()
    {
        var json = BuildValidManifestWithParams("azmcp advisor recommendation list", "advisor");
        var path = WriteTempManifest("valid-params.json", json);

        var result = CanonicalParameterManifestLoader.Load(
            path, "azmcp advisor recommendation list", "advisor");

        Assert.NotNull(result);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string BuildValidManifest(string command, string ns, string azureMcpBuild = "1.0.0")
    {
        return $$"""
        {
          "schemaVersion": "2.0",
          "toolCommand": "{{command}}",
          "namespace": "{{ns}}",
          "sourceIdentity": { "azureMcpBuild": "{{azureMcpBuild}}", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": []
        }
        """;
    }

    private static string BuildValidManifestWithParams(string command, string ns)
    {
        return $$"""
        {
          "schemaVersion": "2.0",
          "toolCommand": "{{command}}",
          "namespace": "{{ns}}",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": [
            {
              "canonicalName": "resource-group",
              "displayName": "Resource group",
              "displayAliases": ["resource-group"],
              "placeholderAliases": ["resource-group", "resource_group"],
              "required": true,
              "requiredText": "Required",
              "isConditionalRequired": false,
              "description": "The resource group name."
            }
          ]
        }
        """;
    }
}
