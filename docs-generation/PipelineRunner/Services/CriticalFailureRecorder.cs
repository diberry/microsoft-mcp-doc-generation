using System.Text.Json;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Steps;
using PipelineRunner.Validation;

namespace PipelineRunner.Services;

public sealed record CriticalFailureRecordReference(
    string ArtifactType,
    string ArtifactName,
    int StepId,
    string StepName,
    string Summary,
    string RecordPath);

public static class CriticalFailureRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static IReadOnlyList<CriticalFailureRecordReference> Persist(
        PipelineContext context,
        IPipelineStep step,
        StepResult result)
    {
        var failures = result.ArtifactFailures.Count > 0
            ? result.ArtifactFailures
            : BuildFallbackFailures(context, step, result);

        if (failures.Count == 0)
        {
            return Array.Empty<CriticalFailureRecordReference>();
        }

        var directory = Path.Combine(context.OutputPath, "critical-failures");
        Directory.CreateDirectory(directory);

        var recordedAt = DateTimeOffset.UtcNow;
        var persisted = new List<CriticalFailureRecordReference>(failures.Count);

        for (var index = 0; index < failures.Count; index++)
        {
            var failure = failures[index];
            var filePath = Path.Combine(directory, BuildRecordFileName(recordedAt, step, failure, index));
            var payload = new CriticalFailureRecord(
                recordedAt.UtcDateTime,
                ResolveNamespace(context),
                step.Id,
                step.Name,
                step.FailurePolicy.ToString(),
                failure.ArtifactType,
                failure.ArtifactName,
                failure.Summary,
                failure.Details,
                failure.RelatedPaths,
                result.Warnings,
                result.ProcessInvocations,
                result.ValidatorResults);

            File.WriteAllText(filePath, JsonSerializer.Serialize(payload, JsonOptions));
            persisted.Add(new CriticalFailureRecordReference(
                failure.ArtifactType,
                failure.ArtifactName,
                step.Id,
                step.Name,
                failure.Summary,
                filePath));
        }

        return persisted;
    }

    private static IReadOnlyList<ArtifactFailure> BuildFallbackFailures(
        PipelineContext context,
        IPipelineStep step,
        StepResult result)
    {
        if (result.Success)
        {
            return Array.Empty<ArtifactFailure>();
        }

        var summary = result.Warnings.FirstOrDefault(static warning => !string.IsNullOrWhiteSpace(warning))
            ?? $"{step.Name} failed.";

        return step switch
        {
            ExamplePromptsStep => BuildToolFallbackFailures(context, summary, result.Outputs),
            ToolGenerationStep => BuildToolFallbackFailures(context, summary, result.Outputs),
            ToolFamilyCleanupStep => [
                ArtifactFailure.Create(
                    "tool family",
                    ResolveFamilyName(context),
                    summary,
                    result.Warnings,
                    [
                        Path.Combine(context.OutputPath, "tool-family", $"{ResolveFamilyName(context)}.md"),
                        Path.Combine(context.OutputPath, "reports", $"tool-family-validation-{ResolveFamilyName(context)}.txt"),
                        .. result.Outputs,
                    ])
            ],
            SkillsRelevanceStep => [
                ArtifactFailure.Create(
                    "azure skill",
                    $"{ResolveNamespaceRoot(context)}-skills-relevance.md",
                    summary,
                    result.Warnings,
                    [
                        Path.Combine(context.OutputPath, "skills-relevance", $"{ResolveNamespaceRoot(context)}-skills-relevance.md"),
                        .. result.Outputs,
                    ])
            ],
            HorizontalArticlesStep => [
                ArtifactFailure.Create(
                    "horizontal article",
                    $"horizontal-article-{ResolveNamespaceRoot(context)}.md",
                    summary,
                    result.Warnings,
                    [
                        Path.Combine(context.OutputPath, "horizontal-articles", $"horizontal-article-{ResolveNamespaceRoot(context)}.md"),
                        Path.Combine(context.OutputPath, "horizontal-articles", $"error-{ResolveNamespaceRoot(context)}.txt"),
                        Path.Combine(context.OutputPath, "horizontal-articles", $"error-{ResolveNamespaceRoot(context)}-airesponse.txt"),
                        .. result.Outputs,
                    ])
            ],
            _ => [ArtifactFailure.Create("pipeline step", step.Name, summary, result.Warnings, result.Outputs)],
        };
    }

    private static IReadOnlyList<ArtifactFailure> BuildToolFallbackFailures(
        PipelineContext context,
        string summary,
        IReadOnlyList<string> outputs)
    {
        var tools = ResolveMatchingTools(context);
        if (tools.Count == 0)
        {
            return [ArtifactFailure.Create("tool", ResolveNamespace(context) ?? "unknown", summary, null, outputs)];
        }

        return tools
            .Select(tool => ArtifactFailure.Create("tool", tool.Command, summary, null, outputs))
            .ToArray();
    }

    private static IReadOnlyList<CliTool> ResolveMatchingTools(PipelineContext context)
    {
        var namespaceName = ResolveNamespace(context);
        if (context.CliOutput is null || string.IsNullOrWhiteSpace(namespaceName))
        {
            return Array.Empty<CliTool>();
        }

        return context.TargetMatcher.FindMatches(context.CliOutput.Tools, namespaceName);
    }

    private static string ResolveFamilyName(PipelineContext context)
    {
        if (context.Items.TryGetValue(ToolFamilyPostAssemblyValidator.FamilyNameContextKey, out var familyNameValue)
            && familyNameValue is string familyName
            && !string.IsNullOrWhiteSpace(familyName))
        {
            return familyName.Trim().ToLowerInvariant();
        }

        var namespaceRoot = ResolveNamespaceRoot(context);
        return string.IsNullOrWhiteSpace(namespaceRoot) ? "unknown" : namespaceRoot;
    }

    private static string ResolveNamespaceRoot(PipelineContext context)
    {
        var namespaceName = ResolveNamespace(context);
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return "unknown";
        }

        return namespaceName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]
            .Trim()
            .ToLowerInvariant();
    }

    private static string? ResolveNamespace(PipelineContext context)
        => context.Items.TryGetValue("Namespace", out var namespaceValue) && namespaceValue is string currentNamespace && !string.IsNullOrWhiteSpace(currentNamespace)
            ? currentNamespace.Trim()
            : null;

    private static string BuildRecordFileName(DateTimeOffset recordedAt, IPipelineStep step, ArtifactFailure failure, int index)
        => $"{recordedAt:yyyyMMddTHHmmssfffZ}-step-{step.Id:D2}-{Sanitize(failure.ArtifactType)}-{Sanitize(failure.ArtifactName)}-{index + 1:D2}.json";

    private static string Sanitize(string value)
    {
        var buffer = new char[value.Length];
        var length = 0;
        var previousDash = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = character;
                previousDash = false;
                continue;
            }

            if (!previousDash)
            {
                buffer[length++] = '-';
                previousDash = true;
            }
        }

        var sanitized = new string(buffer, 0, length).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "artifact" : sanitized;
    }

    private sealed record CriticalFailureRecord(
        DateTime RecordedAtUtc,
        string? Namespace,
        int StepId,
        string StepName,
        string FailurePolicy,
        string ArtifactType,
        string ArtifactName,
        string Summary,
        IReadOnlyList<string> Details,
        IReadOnlyList<string> RelatedPaths,
        IReadOnlyList<string> StepWarnings,
        IReadOnlyList<string> ProcessInvocations,
        IReadOnlyList<ValidatorResult> ValidatorResults);
}
