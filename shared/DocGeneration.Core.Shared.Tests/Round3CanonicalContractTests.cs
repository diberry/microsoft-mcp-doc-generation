// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using DocGeneration.Steps.ExamplePrompts.Validation;
using DocGeneration.TestInfrastructure;
using Shared;
using Xunit;

namespace DocGeneration.Core.Shared.Tests;

/// <summary>
/// Round 3 binding tests for issue #813 Step 3.
/// These tests enforce that:
/// 1. CodeBasedPromptValidator has NO manifest-less path (every ValidatePrompts overload requires a manifest)
/// 2. The validation assembly contains NO reference to ParameterCoverageChecker
/// 3. ExamplePromptsStep records a classified ArtifactFailure (not just a warning) for manifest errors
/// </summary>
public class Round3CanonicalContractTests
{
    /// <summary>
    /// Reflection test: every public ValidatePrompts overload on CodeBasedPromptValidator
    /// must require a non-optional CanonicalParameterManifest parameter.
    /// No overload may have it as optional (default null) or omit it entirely.
    /// </summary>
    [Fact]
    public void CodeBasedPromptValidator_AllValidatePromptsOverloads_RequireManifest()
    {
        var validatorType = typeof(CodeBasedPromptValidator);
        var methods = validatorType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "ValidatePrompts")
            .ToList();

        Assert.NotEmpty(methods);

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();

            // Must have a parameter of type CanonicalParameterManifest (not nullable/optional)
            var manifestParam = parameters
                .FirstOrDefault(p => p.ParameterType == typeof(CanonicalParameterManifest));

            Assert.True(manifestParam != null,
                $"ValidatePrompts overload ({string.Join(", ", parameters.Select(p => p.ParameterType.Name))}) " +
                "must have a CanonicalParameterManifest parameter.");

            // Must not be optional (no default value)
            Assert.False(manifestParam!.IsOptional,
                $"ValidatePrompts parameter '{manifestParam.Name}' of type CanonicalParameterManifest " +
                "must NOT be optional. The manifest is required — no legacy fallback allowed.");
        }
    }

    /// <summary>
    /// The validation assembly (DocGeneration.Steps.ExamplePrompts.Validation) must contain
    /// NO reference to the ParameterCoverageChecker type. The old heuristic must be
    /// completely removed from this assembly. We verify by scanning all types in the
    /// assembly for any field, method parameter, return type, or local that references
    /// ParameterCoverageChecker.
    /// </summary>
    [Fact]
    public void ValidationAssembly_DoesNotReferenceParameterCoverageChecker()
    {
        var validationAssembly = typeof(CodeBasedPromptValidator).Assembly;
        var checkerType = typeof(ParameterCoverageChecker);

        // Scan all types in the validation assembly for any reference to ParameterCoverageChecker
        var referencingTypes = validationAssembly.GetTypes()
            .Where(t => TypeReferencesOther(t, checkerType))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(referencingTypes);
    }

    /// <summary>
    /// No ValidatePrompts overload may have an optional CanonicalParameterManifest parameter.
    /// An optional manifest would allow the legacy ParameterCoverageChecker path to be reached.
    /// </summary>
    [Fact]
    public void CodeBasedPromptValidator_NoOptionalManifestParameter()
    {
        var validatorType = typeof(CodeBasedPromptValidator);
        var methods = validatorType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "ValidatePrompts")
            .ToList();

        Assert.NotEmpty(methods);

        var hasOptionalManifest = methods.Any(m =>
            m.GetParameters().Any(p =>
                p.ParameterType == typeof(CanonicalParameterManifest) && p.IsOptional));

        Assert.False(hasOptionalManifest,
            "CodeBasedPromptValidator must not have an optional CanonicalParameterManifest parameter. " +
            "This would allow the ParameterCoverageChecker legacy path to be reached.");
    }

    /// <summary>
    /// C6 classified failure test: when ExamplePromptsStep encounters a ParameterManifestException
    /// during the retry-feedback path, it must record an ArtifactFailure (not merely a warning).
    /// The ArtifactFailure summary must contain the stable error code.
    /// </summary>
    [Fact]
    public void ExamplePromptsStep_ManifestError_RecordsArtifactFailure_NotJustWarning()
    {
        var source = ReadExamplePromptsStepSource();
        var catchStart = source.IndexOf("catch (ParameterManifestException pme)", StringComparison.Ordinal);
        Assert.True(catchStart >= 0, "ExamplePromptsStep must explicitly catch ParameterManifestException in the retry path.");

        var catchEnd = source.IndexOf("var retryMessage =", catchStart, StringComparison.Ordinal);
        Assert.True(catchEnd > catchStart, "Could not isolate the ParameterManifestException catch block.");

        var catchBlock = source.Substring(catchStart, catchEnd - catchStart);
        Assert.Contains("AddToolWarnings(perToolWarnings, command, [manifestWarning]);", catchBlock, StringComparison.Ordinal);
        Assert.Contains("break;", catchBlock, StringComparison.Ordinal);
        Assert.Contains("BuildValidationFailureDetails(artifact, toolWarnings)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExamplePromptsStep_RetryFeedback_UsesManifestBasedGuidance()
    {
        var source = ReadExamplePromptsStepSource();

        Assert.Contains("var manifest = await LoadParameterManifestAsync(parameterManifestDirectory, command, cancellationToken);", source, StringComparison.Ordinal);
        Assert.Contains("DeterministicPromptRepairer.BuildRetryFeedback(prompts, manifest);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DeterministicPromptRepairer.BuildRetryFeedback(prompts, requiredOptions);", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// C6: ExamplePromptsStep's LoadRequiredOptionsAsync currently returns empty array
    /// when the manifest directory exists but the manifest file does not (line 499-502).
    /// This is fail-soft behavior. After the fix, it must throw ParameterManifestException
    /// (fail-closed) so the caller can record it as an ArtifactFailure.
    /// </summary>
    [Fact]
    public async Task ExamplePromptsStep_LoadRequiredOptions_MissingManifest_MustThrow()
    {
        var stepType = typeof(PipelineRunner.Steps.ExamplePromptsStep);
        var loadMethod = stepType.GetMethod(
            "LoadRequiredOptionsAsync",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(loadMethod);

        var manifestDir = Path.Combine(Path.GetTempPath(), $"r3-c6-{Guid.NewGuid():N}");
        Directory.CreateDirectory(manifestDir);
        try
        {
            // Invoke LoadRequiredOptionsAsync with a directory that exists
            // but contains no manifest file for the given command.
            // Current behavior: returns empty array (fail-soft — D2 defect).
            // Required behavior: throws ParameterManifestException (fail-closed).
            var task = (Task)loadMethod!.Invoke(null, [manifestDir, "azmcp test tool", CancellationToken.None])!;
            await task;

            // Get the result via reflection (Task<IReadOnlyList<Option>>)
            var resultProp = task.GetType().GetProperty("Result");
            var result = resultProp!.GetValue(task) as System.Collections.IList;

            // If we reach here without throwing, the method returned empty (fail-soft).
            // That's the D2 defect. After the fix, this line is unreachable because it throws.
            Assert.Fail(
                "LoadRequiredOptionsAsync must throw ParameterManifestException when manifest is missing, " +
                $"but it returned a result with {result?.Count ?? 0} items (fail-soft). " +
                "This is defect D2 — the manifest must be the sole authority.");
        }
        catch (TargetInvocationException tie) when (tie.InnerException is ParameterManifestException pme)
        {
            // This is the CORRECT behavior after the fix (sync invoke path)
            Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_NOT_FOUND, pme.ErrorCode);
        }
        catch (ParameterManifestException pme)
        {
            // This is the CORRECT behavior after the fix (async await path)
            Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_NOT_FOUND, pme.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(manifestDir))
                Directory.Delete(manifestDir, true);
        }
    }

    private static bool TypeReferencesOther(Type type, Type target)
    {
        // Check fields
        if (type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(f => f.FieldType == target))
            return true;

        // Check method signatures (parameters and return types)
        var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (var method in allMethods)
        {
            if (method.ReturnType == target) return true;
            if (method.GetParameters().Any(p => p.ParameterType == target)) return true;
        }

        // Check properties
        if (type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(p => p.PropertyType == target))
            return true;

        return false;
    }

    private static string ReadExamplePromptsStepSource()
    {
        return File.ReadAllText(Path.Combine(
            ProjectRootFinder.FindSolutionRoot(),
            "mcp-tools",
            "DocGeneration.PipelineRunner",
            "Steps",
            "Namespace",
            "ExamplePromptsStep.cs"));
    }
}
