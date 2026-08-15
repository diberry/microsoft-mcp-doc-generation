// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ExamplePromptGeneratorStandalone.Sanitizers;
using Shared;
using System.Text;
using System.Text.RegularExpressions;

namespace ExamplePromptGeneratorStandalone.Generators;

/// <summary>
/// Deterministically repairs AI-generated example prompts by injecting concrete values
/// for required parameters that lack coverage. Runs AFTER AI parse and BEFORE sanitization.
/// Post-repair verification runs AFTER sanitization to guarantee disk-level coverage.
/// </summary>
public static class DeterministicPromptRepairer
{
    /// <summary>
    /// Manifest-aware repair overload: uses canonical coverage evaluator for identity
    /// and appends bounded clauses in manifest order for missing required parameters.
    /// Byte-identical when already covered. Idempotent (second pass = no-op).
    /// </summary>
    public static RepairResult Repair(IReadOnlyList<string> prompts, CanonicalParameterManifest manifest)
    {
        var initialCoverage = EvaluateRequiredCoverage(prompts, manifest);
        if (prompts.Count == 0 || initialCoverage.Count == 0)
        {
            return new RepairResult(prompts.ToList(), [], [])
            {
                InitialCoverage = initialCoverage,
                FinalCoverage = initialCoverage
            };
        }

        var repairedPrompts = prompts.Select(prompt => prompt ?? string.Empty).ToList();
        var actions = new List<RepairAction>();
        var repairedParamNames = new List<string>();
        var requiredParams = manifest.Parameters
            .Where(static parameter => parameter.Required)
            .ToDictionary(parameter => parameter.CanonicalName, StringComparer.OrdinalIgnoreCase);

        var missingParams = initialCoverage
            .Where(static coverage => !IsCovered(coverage.Verdict))
            .Select(coverage => requiredParams[coverage.CanonicalName])
            .ToList();

        if (missingParams.Count > 0)
        {
            var coveredCanonicalNames = initialCoverage
                .Where(static coverage => IsCovered(coverage.Verdict))
                .Select(static coverage => coverage.CanonicalName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var needsPrepend = manifest.Parameters
                .Where(parameter => coveredCanonicalNames.Contains(parameter.CanonicalName))
                .Any(parameter => repairedPrompts.Any(prompt =>
                    !string.IsNullOrWhiteSpace(prompt) &&
                    Regex.IsMatch(
                        prompt,
                        $@"(?<![\w\-_]){Regex.Escape(parameter.CanonicalName)}(?![\w\-_])",
                        RegexOptions.IgnoreCase)));

            if (needsPrepend)
            {
                for (int i = 0; i < repairedPrompts.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(repairedPrompts[i]))
                    {
                        continue;
                    }

                    var clauses = missingParams.Select(parameter =>
                    {
                        var value = ResolveValue(parameter.CanonicalName, parameter.Description);
                        return $"{parameter.CanonicalName.Replace('-', ' ')} '{value}'";
                    });
                    repairedPrompts[i] = $"For {string.Join(", ", clauses)}: {repairedPrompts[i]}";
                }

                foreach (var parameter in missingParams)
                {
                    var value = ResolveValue(parameter.CanonicalName, parameter.Description);
                    actions.Add(new RepairAction(parameter.CanonicalName, value, "injected"));
                    repairedParamNames.Add(parameter.CanonicalName);
                }
            }
            else
            {
                foreach (var parameter in missingParams)
                {
                    var value = ResolveValue(parameter.CanonicalName, parameter.Description);
                    var injected = false;
                    for (int i = 0; i < repairedPrompts.Count; i++)
                    {
                        if (string.IsNullOrWhiteSpace(repairedPrompts[i]))
                        {
                            continue;
                        }

                        repairedPrompts[i] = InjectParameter(repairedPrompts[i], parameter.CanonicalName, value);
                        injected = true;
                    }

                    if (injected)
                    {
                        actions.Add(new RepairAction(parameter.CanonicalName, value, "injected"));
                        repairedParamNames.Add(parameter.CanonicalName);
                    }
                }
            }
        }

        var finalCoverage = EvaluateRequiredCoverage(repairedPrompts, manifest);
        var stillUncovered = finalCoverage
            .Where(static coverage => !IsCovered(coverage.Verdict))
            .Select(static coverage => coverage.CanonicalName)
            .ToArray();

        var provenance = new List<RepairProvenance>();
        if (repairedParamNames.Count > 0 || stillUncovered.Length > 0)
        {
            provenance.Add(new RepairProvenance(
                "ai-generated",
                repairedParamNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                manifest.SchemaVersion,
                manifest.SourceIdentity.AzureMcpBuild));
        }

        return new RepairResult(repairedPrompts, actions, stillUncovered)
        {
            RepairProvenance = provenance,
            InitialCoverage = initialCoverage,
            FinalCoverage = finalCoverage
        };
    }

    public static string BuildRetryFeedback(IReadOnlyList<string> prompts, CanonicalParameterManifest manifest)
    {
        var coverage = EvaluateRequiredCoverage(prompts, manifest);
        var missingCoverage = coverage
            .Where(static result => !IsCovered(result.Verdict))
            .ToArray();

        if (missingCoverage.Length == 0)
        {
            return string.Empty;
        }

        var requiredParams = manifest.Parameters
            .Where(static parameter => parameter.Required)
            .ToDictionary(parameter => parameter.CanonicalName, StringComparer.OrdinalIgnoreCase);

        var promptEntries = prompts
            .Select((prompt, index) => new { prompt, index })
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.prompt))
            .ToArray();
        var promptIndices = promptEntries
            .Select(static entry => $"Prompt #{entry.index + 1}")
            .ToArray();

        var original = promptEntries.FirstOrDefault()?.prompt?.Trim() ?? "Use the tool.";
        var firstMissing = requiredParams[missingCoverage[0].CanonicalName];
        var rewriteValue = ResolveValue(firstMissing.CanonicalName, firstMissing.Description);
        var rewriteExample = InjectParameter(original, firstMissing.CanonicalName, rewriteValue);
        var rewriteLabel = promptEntries.FirstOrDefault() is { } firstEntry
            ? $"Prompt #{firstEntry.index + 1}"
            : "Prompt #1";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## Actionable repair guidance");
        sb.AppendLine();
        sb.AppendLine($"- Missing required parameters by canonical name: {string.Join(", ", missingCoverage.Select(static item => item.CanonicalName))}");
        sb.AppendLine($"- Prompt slots that can absorb the missing parameters: {(promptIndices.Length > 0 ? string.Join(", ", promptIndices) : "Prompt #1")}");
        sb.AppendLine($"- Rewrite example: {rewriteLabel}: \"{original}\" → \"{rewriteExample}\"");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Strips leading '--' from CLI parameter names for consistent lookup.
    /// </summary>
    internal static string CanonicalizeParamName(string name)
    {
        return name.TrimStart('-');
    }

    /// <summary>
    /// Resolves a concrete value using the precedence chain:
    /// 1. Enum from ParseAllowedValues → first value
    /// 2. ParameterValueBank lookup → first safe entry
    /// 3. Name heuristic: -id→GUID, -endpoint→URI, -date→ISO date
    /// 4. Fallback: "my-{slug}"
    /// </summary>
    internal static string ResolveValue(string canonicalName, string? description)
    {
        // ParseAllowedValues only extracts enum-like candidates from description text;
        // canonical coverage decisions are made exclusively by CanonicalCoverageEvaluator.
        var allowedValues = ParameterCoverageChecker.ParseAllowedValues(description);
        if (allowedValues.Count > 0)
        {
            var candidate = allowedValues[0];
            if (IsValidValue(candidate))
            {
                return candidate;
            }
        }

        if (!string.Equals(canonicalName, "value", StringComparison.OrdinalIgnoreCase)
            && ParameterValueBank.Bank.TryGetValue(canonicalName, out var bankValues)
            && bankValues.Length > 0)
        {
            return bankValues[0];
        }

        if (canonicalName.EndsWith("-id", StringComparison.OrdinalIgnoreCase)
            || canonicalName.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            return "00000000-0000-0000-0000-000000000001";
        }

        if (canonicalName.EndsWith("-endpoint", StringComparison.OrdinalIgnoreCase)
            || canonicalName.EndsWith("-url", StringComparison.OrdinalIgnoreCase)
            || canonicalName.EndsWith("-uri", StringComparison.OrdinalIgnoreCase))
        {
            return "https://contoso.example.com";
        }

        if (canonicalName.EndsWith("-date", StringComparison.OrdinalIgnoreCase)
            || canonicalName.Contains("date", StringComparison.OrdinalIgnoreCase))
        {
            return "2026-01-15";
        }

        var words = canonicalName.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 ? $"contoso-{words[0]}-01" : "contoso-resource-01";
    }

    /// <summary>
    /// Validates that a value is safe to inject (no control chars, reasonable length).
    /// </summary>
    internal static bool IsValidValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length > 200) return false;
        if (value.Any(c => char.IsControl(c) && c != ' ')) return false;
        if (value.Contains('\'') || value.Contains('\n') || value.Contains('\r')) return false;
        return true;
    }

    /// <summary>
    /// Injects a parameter clause into a prompt.
    /// Grammar: append " for {natural-name} '{value}'" before final punctuation.
    /// </summary>
    internal static string InjectParameter(string prompt, string paramName, string value)
    {
        var naturalName = paramName.Replace('-', ' ');
        var clause = $" for {naturalName} '{value}'";

        var trimmed = prompt.TrimEnd();
        if (trimmed.Length > 0 && ".!?".Contains(trimmed[^1]))
        {
            return trimmed[..^1] + clause + trimmed[^1];
        }

        return trimmed + $". Specify {naturalName} '{value}'.";
    }

    private static IReadOnlyList<SingleParameterCoverage> EvaluateRequiredCoverage(
        IReadOnlyList<string> prompts,
        CanonicalParameterManifest manifest)
    {
        var requiredParams = manifest.Parameters
            .Where(static parameter => parameter.Required)
            .ToArray();

        if (requiredParams.Length == 0)
        {
            return Array.Empty<SingleParameterCoverage>();
        }

        return requiredParams
            .Select(parameter => CanonicalCoverageEvaluator.EvaluateSingleParameter(
                prompts,
                parameter,
                manifest.PlaceholderAliasIndex))
            .ToArray();
    }

    private static bool IsCovered(CoverageVerdict verdict)
        => verdict == CoverageVerdict.Concrete || verdict == CoverageVerdict.AuthorizedPlaceholder;
}

/// <summary>
/// Result of a deterministic prompt repair pass.
/// </summary>
public sealed record RepairResult(
    IReadOnlyList<string> RepairedPrompts,
    IReadOnlyList<RepairAction> Actions,
    IReadOnlyList<string> StillUncovered)
{
    /// <summary>
    /// Provenance records for manifest-aware repair (populated by the manifest overload).
    /// </summary>
    public IReadOnlyList<RepairProvenance> RepairProvenance { get; init; } = Array.Empty<RepairProvenance>();

    /// <summary>
    /// Canonical verdicts before repair, in required-parameter manifest order.
    /// </summary>
    public IReadOnlyList<SingleParameterCoverage> InitialCoverage { get; init; } = Array.Empty<SingleParameterCoverage>();

    /// <summary>
    /// Canonical verdicts after repair, in required-parameter manifest order.
    /// </summary>
    public IReadOnlyList<SingleParameterCoverage> FinalCoverage { get; init; } = Array.Empty<SingleParameterCoverage>();
}

/// <summary>
/// Describes a single repair action taken on a prompt set.
/// </summary>
public sealed record RepairAction(string ParameterName, string InjectedValue, string ActionType);

/// <summary>
/// Provenance/telemetry for a manifest-based repair pass.
/// </summary>
public sealed record RepairProvenance(
    string PromptSource,
    string[] RepairedParameters,
    string ManifestSchemaVersion,
    string ManifestSourceBuild);
