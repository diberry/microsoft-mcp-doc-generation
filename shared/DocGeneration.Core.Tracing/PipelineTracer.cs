using System.Collections.Concurrent;
using DocGeneration.Core.Tracing.Models;

namespace DocGeneration.Core.Tracing;

/// <summary>
/// In-memory tracer scoped to a single pipeline run. Call <see cref="FlushAsync"/> from a finally block.
/// </summary>
public sealed class PipelineTracer : IPipelineTracer
{
    private readonly ConcurrentBag<TraceEvent> _steps = [];
    private readonly ConcurrentBag<AiInteraction> _aiInteractions = [];
    private readonly string _pipelineName;
    private readonly string _runId = Guid.NewGuid().ToString("D");
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private long _sequenceNumber;

    public PipelineTracer(string pipelineName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
        _pipelineName = pipelineName;
    }

    public IStepHandle StartStep(string stepName, StepClassification stepType, string? targetName = null, string? inputSummary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

        return new StepHandle(
            this,
            GetNextSequenceNumber(),
            stepName,
            stepType,
            targetName,
            inputSummary,
            DateTimeOffset.UtcNow);
    }

    public void RecordAiCall(AiInteractionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _aiInteractions.Add(new AiInteraction
        {
            SequenceNumber = GetNextSequenceNumber(),
            SkillOrToolName = record.SkillOrToolName,
            Operation = record.Operation,
            SystemPrompt = record.SystemPrompt,
            UserPrompt = record.UserPrompt,
            ResponseContent = record.ResponseContent,
            Model = record.Model,
            TotalTokens = record.TotalTokens,
            DurationMs = record.DurationMs,
            RetryCount = record.RetryCount,
            Timestamp = record.Timestamp
        });
    }

    /// <summary>
    /// Writes buffered trace artifacts to the supplied directory.
    /// Call this from finally blocks so traces are preserved for success, failure, and cancellation paths.
    /// </summary>
    public Task FlushAsync(string outputDirectory, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ct.ThrowIfCancellationRequested();

        var orderedSteps = _steps.OrderBy(step => step.SequenceNumber).ToArray();
        var orderedAiInteractions = _aiInteractions.OrderBy(interaction => interaction.SequenceNumber).ToArray();
        var endedAt = GetRunEndTime(orderedSteps, orderedAiInteractions);

        var pipelineTrace = new PipelineTrace
        {
            PipelineName = _pipelineName,
            RunId = _runId,
            StartedAt = _startedAt,
            EndedAt = endedAt,
            DurationMs = Math.Max(0, (long)(endedAt - _startedAt).TotalMilliseconds),
            Steps = orderedSteps
        };

        return new TraceWriter().WriteAsync(outputDirectory, pipelineTrace, orderedAiInteractions, ct);
    }

    private long GetNextSequenceNumber() => Interlocked.Increment(ref _sequenceNumber);

    private void FinalizeStep(
        long sequenceNumber,
        string stepName,
        StepClassification stepType,
        string? targetName,
        string? inputSummary,
        DateTimeOffset startedAt,
        StepStatus status,
        string? outputSummary,
        string? error)
    {
        var endedAt = DateTimeOffset.UtcNow;

        _steps.Add(new TraceEvent
        {
            SequenceNumber = sequenceNumber,
            StepName = stepName,
            StepType = stepType,
            Status = status,
            TargetName = targetName,
            InputSummary = inputSummary,
            OutputSummary = outputSummary,
            Error = error,
            StartedAt = startedAt,
            EndedAt = endedAt,
            DurationMs = Math.Max(0, (long)(endedAt - startedAt).TotalMilliseconds)
        });
    }

    private DateTimeOffset GetRunEndTime(IReadOnlyCollection<TraceEvent> steps, IReadOnlyCollection<AiInteraction> aiInteractions)
    {
        var latestStep = steps.Count == 0 ? _startedAt : steps.Max(step => step.EndedAt);
        var latestAi = aiInteractions.Count == 0 ? _startedAt : aiInteractions.Max(interaction => interaction.Timestamp);
        return latestStep >= latestAi ? latestStep : latestAi;
    }

    private sealed class StepHandle : IStepHandle
    {
        private readonly PipelineTracer _owner;
        private readonly long _sequenceNumber;
        private readonly string _stepName;
        private readonly StepClassification _stepType;
        private readonly string? _targetName;
        private readonly string? _inputSummary;
        private readonly DateTimeOffset _startedAt;
        private int _isFinalized;

        public StepHandle(
            PipelineTracer owner,
            long sequenceNumber,
            string stepName,
            StepClassification stepType,
            string? targetName,
            string? inputSummary,
            DateTimeOffset startedAt)
        {
            _owner = owner;
            _sequenceNumber = sequenceNumber;
            _stepName = stepName;
            _stepType = stepType;
            _targetName = targetName;
            _inputSummary = inputSummary;
            _startedAt = startedAt;
        }

        public void Complete(string? outputSummary = null) => Finalize(StepStatus.Completed, outputSummary, error: null);

        public void Fail(string error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);
            Finalize(StepStatus.Failed, outputSummary: null, error);
        }

        public void Dispose() => Finalize(StepStatus.Incomplete, outputSummary: null, "Disposed without Complete() or Fail().");

        private void Finalize(StepStatus status, string? outputSummary, string? error)
        {
            if (Interlocked.Exchange(ref _isFinalized, 1) != 0)
            {
                return;
            }

            _owner.FinalizeStep(
                _sequenceNumber,
                _stepName,
                _stepType,
                _targetName,
                _inputSummary,
                _startedAt,
                status,
                outputSummary,
                error);
        }
    }
}
