using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocGeneration.Core.Tracing.Models;

namespace DocGeneration.Core.Tracing;

public sealed class TraceWriter
{
    internal static readonly JsonSerializerOptions TraceJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task WriteAsync(
        string outputDirectory,
        PipelineTrace pipelineTrace,
        IReadOnlyCollection<AiInteraction> aiInteractions,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(pipelineTrace);
        ArgumentNullException.ThrowIfNull(aiInteractions);
        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(outputDirectory);

        var pipelineTraceJson = JsonSerializer.Serialize(pipelineTrace, TraceJsonSerializerOptions);
        var aiInteractionsJson = JsonSerializer.Serialize(aiInteractions, TraceJsonSerializerOptions);
        var summary = BuildSummary(pipelineTrace, aiInteractions);

        await WriteAtomicAsync(Path.Combine(outputDirectory, "pipeline-trace.json"), pipelineTraceJson, ct);
        await WriteAtomicAsync(Path.Combine(outputDirectory, "ai-interactions.json"), aiInteractionsJson, ct);
        await WriteAtomicAsync(Path.Combine(outputDirectory, "summary.md"), summary, ct);
    }

    internal static string BuildSummary(PipelineTrace pipelineTrace, IReadOnlyCollection<AiInteraction> aiInteractions)
    {
        var builder = new StringBuilder();
        var orderedSteps = pipelineTrace.Steps.OrderBy(step => step.SequenceNumber).ToArray();
        var orderedErrors = orderedSteps
            .Where(step => !string.IsNullOrWhiteSpace(step.Error))
            .Select(step => $"- {EscapeMarkdownText(step.StepName)}: {EscapeMarkdownText(step.Error!)}")
            .ToArray();
        var totalTokens = aiInteractions.Sum(interaction => interaction.TotalTokens ?? 0);
        var averageLatency = Math.Round(
            aiInteractions.Select(interaction => (double)interaction.DurationMs).DefaultIfEmpty(0).Average(),
            MidpointRounding.AwayFromZero);

        builder.AppendLine("# Pipeline Trace Summary");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("|-------|-------|");
        builder.AppendLine($"| Pipeline | {EscapeCell(pipelineTrace.PipelineName)} |");
        builder.AppendLine($"| Run ID | {EscapeCell(pipelineTrace.RunId)} |");
        builder.AppendLine($"| Started | {pipelineTrace.StartedAt:O} |");
        builder.AppendLine($"| Duration | {pipelineTrace.DurationMs}ms |");
        builder.AppendLine();
        builder.AppendLine($"## Steps ({orderedSteps.Length})");
        builder.AppendLine();
        builder.AppendLine("| # | Step | Type | Target | Duration | Status |");
        builder.AppendLine("|---|------|------|--------|----------|--------|");

        for (var index = 0; index < orderedSteps.Length; index++)
        {
            var step = orderedSteps[index];
            builder.AppendLine($"| {index + 1} | {EscapeCell(step.StepName)} | {step.StepType.ToString().ToLowerInvariant()} | {EscapeCell(step.TargetName ?? "-")} | {step.DurationMs}ms | {GetStatusGlyph(step.Status)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## AI Statistics");
        builder.AppendLine();
        builder.AppendLine($"- Total AI calls: {aiInteractions.Count}");
        builder.AppendLine($"- Total tokens: {totalTokens}");
        builder.AppendLine($"- Average latency: {averageLatency}ms");
        builder.AppendLine();
        builder.AppendLine("## Errors");
        builder.AppendLine();

        if (orderedErrors.Length == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var error in orderedErrors)
            {
                builder.AppendLine(error);
            }
        }

        return builder.ToString();
    }

    private static async Task WriteAtomicAsync(string filePath, string content, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("Output directory could not be determined.");
        Directory.CreateDirectory(directory);

        var tempFilePath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(tempFilePath, content, Encoding.UTF8, ct);

            if (File.Exists(filePath))
            {
                File.Replace(tempFilePath, filePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempFilePath, filePath);
            }
        }
        catch
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            throw;
        }
    }

    private static string EscapeCell(string value) => EscapeMarkdownText(value);

    private static string EscapeMarkdownText(string value) => value.Replace("|", "\\|").Replace(Environment.NewLine, " ").Replace("\n", " ").Replace("\r", " ");

    private static string GetStatusGlyph(StepStatus status) => status switch
    {
        StepStatus.Completed => "✅",
        StepStatus.Failed => "❌",
        _ => "⚠️"
    };
}
