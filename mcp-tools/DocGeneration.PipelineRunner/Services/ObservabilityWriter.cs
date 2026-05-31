using System.Text;
using System.Text.Json;
using PipelineRunner.Contracts;

namespace PipelineRunner.Services;

/// <summary>
/// Writes observability files (metrics.json, validation.json, summary.md) to step workspaces.
/// </summary>
public static class ObservabilityWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void WriteMetrics(
        string directory,
        string stepName,
        TimeSpan duration,
        int inputCount,
        int outputCount,
        string? validationStatus)
    {
        var metrics = new
        {
            stepName,
            durationMs = (long)duration.TotalMilliseconds,
            inputArtifactCount = inputCount,
            outputArtifactCount = outputCount,
            validationStatus = validationStatus ?? "skipped",
        };

        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, StageOutputContract.MetricsFileName),
            JsonSerializer.Serialize(metrics, Options));
    }

    public static void WriteValidation(string directory, string stepName, IReadOnlyList<ValidatorResult> results)
    {
        var overallStatus = results.Count == 0
            ? "skipped"
            : results.All(r => r.Success) ? "passed" : "failed";

        var validation = new
        {
            stepName,
            validatorResults = results.Select(r => new { r.Name, r.Success, r.Warnings }),
            overallStatus,
        };

        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, StageOutputContract.ValidationFileName),
            JsonSerializer.Serialize(validation, Options));
    }

    public static void WriteSummary(
        string directory,
        string stepName,
        bool success,
        TimeSpan duration,
        IReadOnlyList<string> warnings)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {stepName}");
        builder.AppendLine();
        builder.AppendLine($"**Status:** {(success ? "✅ Success" : "❌ Failed")}");
        builder.AppendLine($"**Duration:** {duration.TotalSeconds:F1}s");

        if (warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Warnings");
            foreach (var warning in warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, StageOutputContract.SummaryFileName),
            builder.ToString());
    }

    public static void WritePromptPreviewNa(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, StageOutputContract.PromptPreviewNaFileName),
            "N/A — deterministic step (no AI prompt)");
    }

    public static void WritePromptPreview(string directory, string content)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, StageOutputContract.PromptPreviewFileName),
            content);
    }
}
