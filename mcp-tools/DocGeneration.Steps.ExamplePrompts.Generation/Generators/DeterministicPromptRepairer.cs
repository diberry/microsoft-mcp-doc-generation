// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ExamplePromptGeneratorStandalone.Models;
using ExamplePromptGeneratorStandalone.Sanitizers;
using Shared;
using System.Text;

namespace ExamplePromptGeneratorStandalone.Generators;

/// <summary>
/// Deterministically repairs AI-generated example prompts by injecting concrete values
/// for required parameters that lack coverage. Runs AFTER AI parse and BEFORE sanitization.
/// Post-repair verification runs AFTER sanitization to guarantee disk-level coverage.
/// </summary>
public static class DeterministicPromptRepairer
{
    /// <summary>
    /// Repairs prompts so that every non-blank prompt covers every required parameter.
    /// Uses the coverage checker to detect gaps, then injects values from the resolution chain.
    /// Coverage verification uses the display name (matching Step 4 validation) when available,
    /// falling back to the canonicalized CLI name.
    /// </summary>
    public static RepairResult Repair(IReadOnlyList<string> prompts, IReadOnlyList<Option> requiredParameters)
    {
        if (prompts.Count == 0 || requiredParameters.Count == 0)
            return new RepairResult(prompts.ToList(), [], []);

        var repairedPrompts = prompts.Select(p => p ?? "").ToList();
        var actions = new List<RepairAction>();
        var stillUncovered = new List<string>();

        foreach (var param in requiredParameters)
        {
            if (string.IsNullOrWhiteSpace(param.Name)) continue;

            var canonicalName = CanonicalizeParamName(param.Name);
            var coverageName = GetCoverageName(param);
            var coverage = GetEffectiveCoverage(repairedPrompts, coverageName, requiredParameters.Count, param.Description);

            if (coverage) continue;

            // Resolve a concrete value for this parameter
            var value = ResolveValue(canonicalName, param.Description);

            // Inject into EVERY non-blank prompt (blocking issue #1: not round-robin distribution)
            var injected = false;
            for (int i = 0; i < repairedPrompts.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(repairedPrompts[i])) continue;

                repairedPrompts[i] = InjectParameter(repairedPrompts[i], canonicalName, value);
                injected = true;
            }

            if (injected)
            {
                actions.Add(new RepairAction(canonicalName, value, "injected"));
            }
        }

        var sanitizedPrompts = repairedPrompts.Select(CredentialSanitizer.Sanitize).ToList();
        stillUncovered.AddRange(GetStillUncovered(sanitizedPrompts, requiredParameters));

        if (stillUncovered.Count > 0)
        {
            ApplyLastResortFallback(repairedPrompts, requiredParameters, stillUncovered);
            sanitizedPrompts = repairedPrompts.Select(CredentialSanitizer.Sanitize).ToList();
            stillUncovered = GetStillUncovered(sanitizedPrompts, requiredParameters).ToList();
        }

        return new RepairResult(repairedPrompts, actions, stillUncovered);
    }

    public static string BuildRetryFeedback(IReadOnlyList<string> prompts, IReadOnlyList<Option> requiredParameters)
    {
        var missing = requiredParameters
            .Where(param => !string.IsNullOrWhiteSpace(param.Name))
            .Where(param => !GetEffectiveCoverage(prompts, GetCoverageName(param), requiredParameters.Count, param.Description))
            .ToList();

        if (missing.Count == 0)
        {
            return string.Empty;
        }

        var promptIndices = prompts
            .Select((prompt, index) => new { prompt, index })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.prompt))
            .Select(entry => $"Prompt #{entry.index + 1}")
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## Actionable repair guidance");
        sb.AppendLine();
        sb.AppendLine($"- Missing required parameters by name: {string.Join(", ", missing.Select(GetCoverageName))}");
        sb.AppendLine($"- Prompt slots that can absorb the missing parameters: {(promptIndices.Length > 0 ? string.Join(", ", promptIndices) : "Prompt #1")}");

        var rewriteExample = BuildRewriteExample(prompts, missing[0]);
        if (!string.IsNullOrWhiteSpace(rewriteExample))
        {
            sb.AppendLine($"- Rewrite example: {rewriteExample}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Gets the name used for coverage checking: display name (matching Step 4 validator)
    /// when available, otherwise the canonicalized CLI name.
    /// </summary>
    internal static string GetCoverageName(Option param)
    {
        if (!string.IsNullOrWhiteSpace(param.DisplayName))
            return param.DisplayName;
        return CanonicalizeParamName(param.Name!);
    }

    /// <summary>
    /// Determines effective coverage: Covered OR PlaceholderDetected (blocking issue #3).
    /// </summary>
    internal static bool GetEffectiveCoverage(IReadOnlyList<string> prompts, string paramName, int totalRequired, string? description)
    {
        var coverage = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, paramName, totalRequired, description);
        return coverage.Covered || coverage.PlaceholderDetected;
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
        // 1. Enum values
        var allowedValues = ParameterCoverageChecker.ParseAllowedValues(description);
        if (allowedValues.Count > 0)
        {
            var candidate = allowedValues[0];
            // Reject values with control characters or excessive length
            if (IsValidValue(candidate))
                return candidate;
        }

        // 2. ValueBank lookup (excluding 'value' key which has credentials)
        if (!string.Equals(canonicalName, "value", StringComparison.OrdinalIgnoreCase)
            && ParameterValueBank.Bank.TryGetValue(canonicalName, out var bankValues)
            && bankValues.Length > 0)
        {
            return bankValues[0];
        }

        // 3. Name heuristics
        if (canonicalName.EndsWith("-id", StringComparison.OrdinalIgnoreCase)
            || canonicalName.Equals("id", StringComparison.OrdinalIgnoreCase))
            return "00000000-0000-0000-0000-000000000001";

        if (canonicalName.EndsWith("-endpoint", StringComparison.OrdinalIgnoreCase)
            || canonicalName.EndsWith("-url", StringComparison.OrdinalIgnoreCase)
            || canonicalName.EndsWith("-uri", StringComparison.OrdinalIgnoreCase))
            return "https://contoso.example.com";

        if (canonicalName.EndsWith("-date", StringComparison.OrdinalIgnoreCase)
            || canonicalName.Contains("date", StringComparison.OrdinalIgnoreCase))
            return "2026-01-15";

        // 4. Fallback — avoid embedding full slug (causes checker self-match)
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
            // Insert before final punctuation
            return trimmed[..^1] + clause + trimmed[^1];
        }

        // No punctuation — append with period
        return trimmed + $". Specify {naturalName} '{value}'.";
    }

    private static IReadOnlyList<string> GetStillUncovered(IReadOnlyList<string> sanitizedPrompts, IReadOnlyList<Option> requiredParameters)
        => requiredParameters
            .Where(param => !string.IsNullOrWhiteSpace(param.Name))
            .Select(param => new
            {
                CoverageName = GetCoverageName(param),
                Covered = GetEffectiveCoverage(sanitizedPrompts, GetCoverageName(param), requiredParameters.Count, param.Description)
            })
            .Where(result => !result.Covered)
            .Select(result => result.CoverageName)
            .ToArray();

    private static void ApplyLastResortFallback(List<string> prompts, IReadOnlyList<Option> requiredParameters, IReadOnlyList<string> unresolvedCoverageNames)
    {
        if (unresolvedCoverageNames.Count == 0)
        {
            return;
        }

        var targetIndices = prompts
            .Select((prompt, index) => new { prompt, index })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.prompt))
            .Select(entry => entry.index)
            .ToList();

        if (targetIndices.Count == 0)
        {
            prompts.Add(string.Empty);
            targetIndices.Add(prompts.Count - 1);
        }

        var writeIndex = 0;
        foreach (var coverageName in unresolvedCoverageNames)
        {
            var param = requiredParameters.FirstOrDefault(option =>
                string.Equals(GetCoverageName(option), coverageName, StringComparison.Ordinal));
            if (param is null || string.IsNullOrWhiteSpace(param.Name))
            {
                continue;
            }

            var targetPromptIndex = targetIndices[writeIndex % targetIndices.Count];
            var value = ResolveValue(CanonicalizeParamName(param.Name), param.Description);
            prompts[targetPromptIndex] = BuildLastResortPrompt(prompts[targetPromptIndex], coverageName, value);
            writeIndex++;
        }
    }

    private static string BuildRewriteExample(IReadOnlyList<string> prompts, Option param)
    {
        var promptIndex = prompts
            .Select((prompt, index) => new { prompt, index })
            .FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.prompt));
        var original = promptIndex?.prompt?.Trim() ?? "Use the tool.";
        var coverageName = GetCoverageName(param);
        var value = ResolveValue(CanonicalizeParamName(param.Name!), param.Description);
        var rewritten = BuildLastResortPrompt(original, coverageName, value);
        var label = promptIndex is null ? "Prompt #1" : $"Prompt #{promptIndex.index + 1}";
        return $"{label}: \"{original}\" → \"{rewritten}\"";
    }

    private static string BuildLastResortPrompt(string prompt, string coverageName, string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(prompt) ? "Use the tool" : prompt.Trim().TrimEnd('.', '!', '?');
        return $"{trimmed}. Specify {coverageName} '{value}'.";
    }
}

/// <summary>
/// Result of a deterministic prompt repair pass.
/// </summary>
public sealed record RepairResult(
    IReadOnlyList<string> RepairedPrompts,
    IReadOnlyList<RepairAction> Actions,
    IReadOnlyList<string> StillUncovered);

/// <summary>
/// Describes a single repair action taken on a prompt set.
/// </summary>
public sealed record RepairAction(string ParameterName, string InjectedValue, string ActionType);
