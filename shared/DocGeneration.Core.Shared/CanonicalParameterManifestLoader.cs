// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Shared;

/// <summary>
/// Strict, fail-closed loader for v2 canonical parameter manifests.
/// Never returns null. Never swallows exceptions. Never returns an empty fallback.
/// </summary>
public static class CanonicalParameterManifestLoader
{
    private const string SupportedSchema = "2.0";

    /// <summary>
    /// Loads and validates a v2 parameter manifest synchronously.
    /// </summary>
    public static CanonicalParameterManifest Load(
        string manifestPath,
        string expectedCommand,
        string? expectedNamespace = null,
        string? currentAzureMcpBuild = null,
        bool requireNonEmptyParameters = false)
    {
        if (!File.Exists(manifestPath))
        {
            throw new ParameterManifestException(
                ParameterManifestErrorCode.PARAM_MANIFEST_NOT_FOUND,
                manifestPath,
                $"Parameter manifest not found: '{manifestPath}'. Ensure Step 1 completed for this tool.");
        }

        string json;
        try
        {
            json = File.ReadAllText(manifestPath);
        }
        catch (IOException ex)
        {
            throw new ParameterManifestException(
                ParameterManifestErrorCode.PARAM_MANIFEST_NOT_FOUND,
                manifestPath,
                $"Parameter manifest not found: '{manifestPath}'. {ex.Message}",
                ex);
        }

        return ParseAndValidate(json, manifestPath, expectedCommand, expectedNamespace, currentAzureMcpBuild, requireNonEmptyParameters);
    }

    /// <summary>
    /// Loads and validates a v2 parameter manifest asynchronously.
    /// </summary>
    public static async Task<CanonicalParameterManifest> LoadAsync(
        string manifestPath,
        string expectedCommand,
        string? expectedNamespace = null,
        string? currentAzureMcpBuild = null,
        bool requireNonEmptyParameters = false,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(manifestPath))
        {
            throw new ParameterManifestException(
                ParameterManifestErrorCode.PARAM_MANIFEST_NOT_FOUND,
                manifestPath,
                $"Parameter manifest not found: '{manifestPath}'. Ensure Step 1 completed for this tool.");
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new ParameterManifestException(
                ParameterManifestErrorCode.PARAM_MANIFEST_NOT_FOUND,
                manifestPath,
                $"Parameter manifest not found: '{manifestPath}'. {ex.Message}",
                ex);
        }

        return ParseAndValidate(json, manifestPath, expectedCommand, expectedNamespace, currentAzureMcpBuild, requireNonEmptyParameters);
    }

    private static CanonicalParameterManifest ParseAndValidate(
        string json,
        string manifestPath,
        string expectedCommand,
        string? expectedNamespace,
        string? currentAzureMcpBuild,
        bool requireNonEmptyParameters)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ParameterManifestException(
                ParameterManifestErrorCode.PARAM_MANIFEST_MALFORMED,
                manifestPath,
                $"Parameter manifest '{manifestPath}' is malformed JSON: {ex.Message}",
                ex);
        }

        using (doc)
        {
            // Check for legacy bare-array format
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                throw new ParameterManifestException(
                    ParameterManifestErrorCode.PARAM_MANIFEST_LEGACY_FORMAT,
                    manifestPath,
                    $"Parameter manifest '{manifestPath}' uses legacy bare-array format. Rerun Step 1.");
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ParameterManifestException(
                    ParameterManifestErrorCode.PARAM_MANIFEST_MALFORMED,
                    manifestPath,
                    $"Parameter manifest '{manifestPath}' is malformed JSON: root element is not an object.");
            }

            // Schema version check
            var schemaVersion = doc.RootElement.TryGetProperty("schemaVersion", out var sv) ? sv.GetString() : null;
            if (!string.Equals(schemaVersion, SupportedSchema, StringComparison.Ordinal))
            {
                throw new ParameterManifestException(
                    ParameterManifestErrorCode.PARAM_MANIFEST_SCHEMA_UNKNOWN,
                    manifestPath,
                    $"Parameter manifest '{manifestPath}' has unrecognized schemaVersion '{schemaVersion ?? "null"}'. Expected '2.0'.");
            }

            // Command mismatch
            var toolCommand = doc.RootElement.TryGetProperty("toolCommand", out var tc) ? tc.GetString() : null;
            if (!string.Equals(toolCommand, expectedCommand, StringComparison.OrdinalIgnoreCase))
            {
                throw new ParameterManifestException(
                    ParameterManifestErrorCode.PARAM_MANIFEST_COMMAND_MISMATCH,
                    manifestPath,
                    $"Parameter manifest '{manifestPath}' command '{toolCommand}' does not match expected '{expectedCommand}'.");
            }

            // Namespace mismatch (skipped when null per C4)
            var ns = doc.RootElement.TryGetProperty("namespace", out var nsEl) ? nsEl.GetString() : null;
            if (expectedNamespace is not null && !string.Equals(ns, expectedNamespace, StringComparison.OrdinalIgnoreCase))
            {
                throw new ParameterManifestException(
                    ParameterManifestErrorCode.PARAM_MANIFEST_NAMESPACE_MISMATCH,
                    manifestPath,
                    $"Parameter manifest '{manifestPath}' namespace '{ns}' does not match expected '{expectedNamespace}'.");
            }

            // Source staleness (skipped when null per C4)
            string? manifestBuild = null;
            if (doc.RootElement.TryGetProperty("sourceIdentity", out var si) && si.TryGetProperty("azureMcpBuild", out var mb))
            {
                manifestBuild = mb.GetString();
            }
            if (currentAzureMcpBuild is not null && !string.Equals(manifestBuild, currentAzureMcpBuild, StringComparison.Ordinal))
            {
                throw new ParameterManifestException(
                    ParameterManifestErrorCode.PARAM_MANIFEST_SOURCE_STALE,
                    manifestPath,
                    $"Parameter manifest '{manifestPath}' was generated from build '{manifestBuild}' but current is '{currentAzureMcpBuild}'. Rerun Step 1.");
            }

            // Parse parameters
            var parameters = new List<CanonicalParameterEntry>();
            if (doc.RootElement.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in paramsEl.EnumerateArray())
                {
                    var canonical = p.TryGetProperty("canonicalName", out var cn) ? cn.GetString() ?? "" : "";
                    var displayName = p.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
                    var displayAliases = ReadStringArray(p, "displayAliases");
                    var placeholderAliases = ReadStringArray(p, "placeholderAliases");
                    var required = p.TryGetProperty("required", out var req) && req.GetBoolean();
                    var requiredText = p.TryGetProperty("requiredText", out var rt) ? rt.GetString() ?? "" : "";
                    var isConditional = p.TryGetProperty("isConditionalRequired", out var ic) && ic.GetBoolean();
                    var description = p.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";

                    parameters.Add(new CanonicalParameterEntry(
                        canonical, displayName, displayAliases, placeholderAliases,
                        required, requiredText, isConditional, description));
                }
            }

            // Empty params check (C5)
            if (requireNonEmptyParameters && parameters.Count == 0)
            {
                throw new ParameterManifestException(
                    ParameterManifestErrorCode.PARAM_MANIFEST_EMPTY_PARAMS,
                    manifestPath,
                    $"Parameter manifest '{manifestPath}' has empty parameters array but tool has CLI options.");
            }

            // Validate parameters
            ValidateParameters(parameters, manifestPath);

            // Build source identity
            var generatedAt = "";
            if (doc.RootElement.TryGetProperty("sourceIdentity", out var siEl))
            {
                if (siEl.TryGetProperty("generatedAtUtc", out var ga))
                    generatedAt = ga.GetString() ?? "";
            }
            var sourceIdentity = new ManifestSourceIdentity(manifestBuild ?? "", generatedAt);

            // Build placeholder alias index
            var placeholderIndex = BuildPlaceholderIndex(parameters, manifestPath);

            return new CanonicalParameterManifest(
                schemaVersion!,
                toolCommand!,
                ns ?? "",
                sourceIdentity,
                parameters,
                placeholderIndex);
        }
    }

    private static string[] ReadStringArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        return arr.EnumerateArray()
            .Select(e => e.GetString() ?? "")
            .ToArray();
    }

    private static void ValidateParameters(List<CanonicalParameterEntry> parameters, string manifestPath)
    {
        // Check empty aliases
        foreach (var p in parameters)
        {
            foreach (var alias in p.DisplayAliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    throw new ParameterManifestException(
                        ParameterManifestErrorCode.PARAM_MANIFEST_EMPTY_ALIAS,
                        manifestPath,
                        $"Parameter '{p.CanonicalName}' in '{manifestPath}' has empty alias entry.");
                }
            }
            foreach (var alias in p.PlaceholderAliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    throw new ParameterManifestException(
                        ParameterManifestErrorCode.PARAM_MANIFEST_EMPTY_ALIAS,
                        manifestPath,
                        $"Parameter '{p.CanonicalName}' in '{manifestPath}' has empty alias entry.");
                }
            }
        }

        // Duplicate canonical names
        var canonicalSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
        {
            if (!canonicalSet.Add(p.CanonicalName))
            {
                throw new ParameterManifestException(
                    ParameterManifestErrorCode.PARAM_MANIFEST_DUPLICATE_CANONICAL,
                    manifestPath,
                    $"Duplicate canonicalName '{p.CanonicalName}' in '{manifestPath}'.");
            }
        }

        // Normalization collision
        var normalizedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
        {
            var norm = CanonicalParameterNormalizer.Normalize(p.CanonicalName);
            if (normalizedMap.TryGetValue(norm, out var existing))
            {
                throw new ParameterManifestException(
                    ParameterManifestErrorCode.PARAM_MANIFEST_NORMALIZATION_COLLISION,
                    manifestPath,
                    $"Canonical names '{existing}' and '{p.CanonicalName}' normalize to the same token '{norm}' in '{manifestPath}'.");
            }
            normalizedMap[norm] = p.CanonicalName;
        }

        // Alias collision (displayAliases across parameters)
        var displayAliasOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
        {
            foreach (var alias in p.DisplayAliases)
            {
                if (displayAliasOwners.TryGetValue(alias, out var owner) && !string.Equals(owner, p.CanonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ParameterManifestException(
                        ParameterManifestErrorCode.PARAM_MANIFEST_ALIAS_COLLISION,
                        manifestPath,
                        $"Alias '{alias}' in '{manifestPath}' maps to both '{owner}' and '{p.CanonicalName}'.");
                }
                displayAliasOwners.TryAdd(alias, p.CanonicalName);
            }
        }

        // Alias shadows canonical
        foreach (var p in parameters)
        {
            foreach (var alias in p.DisplayAliases)
            {
                if (canonicalSet.Contains(alias) && !string.Equals(alias, p.CanonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ParameterManifestException(
                        ParameterManifestErrorCode.PARAM_MANIFEST_ALIAS_SHADOWS_CANONICAL,
                        manifestPath,
                        $"Alias '{alias}' of '{p.CanonicalName}' shadows canonicalName of another parameter in '{manifestPath}'.");
                }
            }
        }

        // Placeholder multi-bind
        var placeholderOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
        {
            foreach (var alias in p.PlaceholderAliases)
            {
                if (placeholderOwners.TryGetValue(alias, out var owner) && !string.Equals(owner, p.CanonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ParameterManifestException(
                        ParameterManifestErrorCode.PARAM_MANIFEST_PLACEHOLDER_MULTI_BIND,
                        manifestPath,
                        $"Placeholder alias '{alias}' binds to both '{owner}' and '{p.CanonicalName}' in '{manifestPath}'.");
                }
                placeholderOwners.TryAdd(alias, p.CanonicalName);
            }
        }
    }

    private static IReadOnlyDictionary<string, string> BuildPlaceholderIndex(
        List<CanonicalParameterEntry> parameters, string manifestPath)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
        {
            foreach (var alias in p.PlaceholderAliases)
            {
                index.TryAdd(alias, p.CanonicalName);
            }
        }
        return index;
    }
}
