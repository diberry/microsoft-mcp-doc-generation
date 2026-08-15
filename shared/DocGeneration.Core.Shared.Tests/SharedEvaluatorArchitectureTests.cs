// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Core.Shared.Tests;

/// <summary>
/// Group 4 — Architecture test: one shared evaluator across all seams.
/// Proves that no seam constructs its own private manifest model or coverage heuristic.
/// </summary>
public class SharedEvaluatorArchitectureTests
{
    /// <summary>
    /// Verifies that the types CanonicalParameterManifestLoader, CanonicalCoverageEvaluator,
    /// and CanonicalParameterNormalizer exist in the Shared namespace and are public static.
    /// </summary>
    [Fact]
    public void SharedTypes_ExistInSharedNamespace()
    {
        var loaderType = typeof(CanonicalParameterManifestLoader);
        var evaluatorType = typeof(CanonicalCoverageEvaluator);
        var normalizerType = typeof(CanonicalParameterNormalizer);

        Assert.True(loaderType.IsPublic);
        Assert.True(loaderType.IsAbstract && loaderType.IsSealed); // static class
        Assert.True(evaluatorType.IsPublic);
        Assert.True(evaluatorType.IsAbstract && evaluatorType.IsSealed);
        Assert.True(normalizerType.IsPublic);
        Assert.True(normalizerType.IsAbstract && normalizerType.IsSealed);
    }

    /// <summary>
    /// Verifies ParameterManifestException has ErrorCode and ManifestPath properties.
    /// </summary>
    [Fact]
    public void ParameterManifestException_HasRequiredProperties()
    {
        var type = typeof(ParameterManifestException);
        Assert.True(type.IsPublic);

        var errorCodeProp = type.GetProperty("ErrorCode");
        Assert.NotNull(errorCodeProp);
        Assert.Equal(typeof(string), errorCodeProp!.PropertyType);

        var manifestPathProp = type.GetProperty("ManifestPath");
        Assert.NotNull(manifestPathProp);
        Assert.Equal(typeof(string), manifestPathProp!.PropertyType);
    }

    /// <summary>
    /// Verifies ParameterManifestErrorCode has all required stable error code constants.
    /// </summary>
    [Fact]
    public void ParameterManifestErrorCode_HasAllStableConstants()
    {
        var type = typeof(ParameterManifestErrorCode);
        Assert.True(type.IsPublic);
        Assert.True(type.IsAbstract && type.IsSealed); // static class

        var expectedCodes = new[]
        {
            "PARAM_MANIFEST_NOT_FOUND",
            "PARAM_MANIFEST_MALFORMED",
            "PARAM_MANIFEST_SCHEMA_UNKNOWN",
            "PARAM_MANIFEST_LEGACY_FORMAT",
            "PARAM_MANIFEST_COMMAND_MISMATCH",
            "PARAM_MANIFEST_NAMESPACE_MISMATCH",
            "PARAM_MANIFEST_SOURCE_STALE",
            "PARAM_MANIFEST_EMPTY_PARAMS",
            "PARAM_MANIFEST_EMPTY_ALIAS",
            "PARAM_MANIFEST_DUPLICATE_CANONICAL",
            "PARAM_MANIFEST_ALIAS_COLLISION",
            "PARAM_MANIFEST_ALIAS_SHADOWS_CANONICAL",
            "PARAM_MANIFEST_NORMALIZATION_COLLISION",
            "PARAM_MANIFEST_PLACEHOLDER_MULTI_BIND",
        };

        foreach (var code in expectedCodes)
        {
            var field = type.GetField(code, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.NotNull(field);
            Assert.True(field!.IsLiteral); // const
            Assert.Equal(code, (string)field.GetValue(null)!);
        }
    }

    /// <summary>
    /// Verifies CoverageVerdict enum has all required values.
    /// </summary>
    [Fact]
    public void CoverageVerdict_HasAllValues()
    {
        Assert.True(typeof(CoverageVerdict).IsEnum);
        var names = Enum.GetNames(typeof(CoverageVerdict));
        Assert.Contains("Concrete", names);
        Assert.Contains("AuthorizedPlaceholder", names);
        Assert.Contains("Missing", names);
        Assert.Contains("Ambiguous", names);
    }

    /// <summary>
    /// Verifies CanonicalParameterManifest record has required properties.
    /// </summary>
    [Fact]
    public void CanonicalParameterManifest_HasRequiredProperties()
    {
        var type = typeof(CanonicalParameterManifest);
        Assert.NotNull(type.GetProperty("SchemaVersion"));
        Assert.NotNull(type.GetProperty("ToolCommand"));
        Assert.NotNull(type.GetProperty("Namespace"));
        Assert.NotNull(type.GetProperty("SourceIdentity"));
        Assert.NotNull(type.GetProperty("Parameters"));
        Assert.NotNull(type.GetProperty("PlaceholderAliasIndex"));
    }

    /// <summary>
    /// Verifies CanonicalParameterEntry record has required properties.
    /// </summary>
    [Fact]
    public void CanonicalParameterEntry_HasRequiredProperties()
    {
        var type = typeof(CanonicalParameterEntry);
        Assert.NotNull(type.GetProperty("CanonicalName"));
        Assert.NotNull(type.GetProperty("DisplayName"));
        Assert.NotNull(type.GetProperty("DisplayAliases"));
        Assert.NotNull(type.GetProperty("PlaceholderAliases"));
        Assert.NotNull(type.GetProperty("Required"));
        Assert.NotNull(type.GetProperty("RequiredText"));
        Assert.NotNull(type.GetProperty("IsConditionalRequired"));
        Assert.NotNull(type.GetProperty("Description"));
    }

    /// <summary>
    /// Architecture constraint: ParameterCrossCheckService.LoadValidParametersAsync must route
    /// through the canonical loader — it must NOT have its own private ParameterManifestEntry record
    /// once the v2 manifest is in place. This test verifies the inline record is replaced.
    /// </summary>
    [Fact]
    public void ParameterCrossCheckService_DoesNotDefinePrivateManifestModel()
    {
        var serviceType = typeof(ToolFamilyCleanup.Services.ParameterCrossCheckService);
        var nestedTypes = serviceType.GetNestedTypes(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        // After implementation, the private ParameterManifestEntry record should be removed
        var hasPrivateManifestEntry = nestedTypes.Any(t => t.Name == "ParameterManifestEntry");
        Assert.False(hasPrivateManifestEntry,
            "ParameterCrossCheckService should not define a private ParameterManifestEntry — " +
            "it must use the shared CanonicalParameterManifestLoader.");
    }

    // ── Defect 5 binding tests ────────────────────────────────────────────────

    /// <summary>
    /// Architecture binding: ExamplePromptsStep must NOT define a private ParameterManifestOption
    /// record (Defect 4). After the fix, the step uses CanonicalParameterManifestLoader directly.
    /// </summary>
    [Fact]
    public void ExamplePromptsStep_DoesNotDefinePrivateParameterManifestOptionRecord()
    {
        var stepType = typeof(PipelineRunner.Steps.ExamplePromptsStep);
        var nestedTypes = stepType.GetNestedTypes(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        var hasRecord = nestedTypes.Any(t => t.Name == "ParameterManifestOption");
        Assert.False(hasRecord,
            "ExamplePromptsStep must not define a private ParameterManifestOption record. " +
            "After Defect 4 is fixed, it must use CanonicalParameterManifestLoader.");
    }

    /// <summary>
    /// Architecture binding: CodeBasedPromptValidator.ValidatePrompts must have an overload
    /// that accepts CanonicalParameterManifest? so callers can use canonical evaluation (Defect 3).
    /// </summary>
    [Fact]
    public void CodeBasedPromptValidator_ValidatePrompts_AcceptsCanonicalManifestParameter()
    {
        var validatorType = typeof(DocGeneration.Steps.ExamplePrompts.Validation.CodeBasedPromptValidator);
        var methods = validatorType.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var hasCanonicalOverload = methods
            .Where(m => m.Name == "ValidatePrompts")
            .Any(m => m.GetParameters()
                .Any(p => p.ParameterType == typeof(CanonicalParameterManifest)));

        Assert.True(hasCanonicalOverload,
            "CodeBasedPromptValidator.ValidatePrompts must have an overload accepting " +
            "CanonicalParameterManifest? (Defect 3 fix).");
    }

    /// <summary>
    /// Architecture binding: ExamplePromptGenerator.GetPromptParameters must NOT reference
    /// the legacy ParameterManifestParameter type as its manifest parameter type (Defect 2).
    /// After the fix, it must accept CanonicalParameterManifest?.
    /// </summary>
    [Fact]
    public void ExamplePromptGenerator_GetPromptParameters_DoesNotAcceptLegacyManifestType()
    {
        var generatorType = typeof(ExamplePromptGeneratorStandalone.Generators.ExamplePromptGenerator);
        var method = generatorType.GetMethod(
            "GetPromptParameters",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        // After migration, none of the parameters should be of the legacy
        // ParameterManifestParameter list type
        var hasLegacyParam = method!.GetParameters()
            .Any(p => p.ParameterType.IsGenericType &&
                      p.ParameterType.GetGenericArguments().Any(a =>
                          a.Name == "ParameterManifestParameter"));

        Assert.False(hasLegacyParam,
            "ExamplePromptGenerator.GetPromptParameters must not accept legacy " +
            "IReadOnlyList<ParameterManifestParameter>. After Defect 2 is fixed it must " +
            "accept CanonicalParameterManifest?.");
    }
}
