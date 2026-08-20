using Azure;
using System.Diagnostics;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocGeneration.Core.Tracing;
using Microsoft.Extensions.AI;
using System.ClientModel;

namespace GenerativeAI;

public class GenerativeAIClient
{
    private const int CharactersPerTokenEstimate = 4;
    private const int MaxRetries = 5;
    private readonly IChatClient _chatClient;
    private readonly IPipelineTracer _tracer;
    private readonly string? _modelName;
    private readonly Action<string> _statusLogger;

    /// <summary>
    /// Process environment variable that, when set to a truthy value, disables every further
    /// Azure OpenAI call made through <see cref="GetChatCompletionAsync"/> — the single,
    /// service-agnostic choke point all AI-dependent pipeline steps (2, 3, 4, 6) call through.
    /// Set by PipelineRunner's bootstrap step only after its early live-endpoint probe fails and
    /// an interactive operator explicitly selects the "partial_explicit" offline continuation.
    /// Because environment variables are inherited by child processes, setting this once in the
    /// parent pipeline process also disables AI calls made by every subprocess-based step.
    /// </summary>
    public const string OfflineEnvironmentVariable = "PIPELINE_AI_ENDPOINT_OFFLINE";

    /// <summary>
    /// Returns true when <see cref="OfflineEnvironmentVariable"/> is set to a truthy value
    /// (1/true/yes/on, case-insensitive) in the current process environment.
    /// </summary>
    public static bool IsOfflineModeActive()
        => IsTruthy(Environment.GetEnvironmentVariable(OfflineEnvironmentVariable));

    private static bool IsTruthy(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("on", StringComparison.OrdinalIgnoreCase));

    public GenerativeAIClient(GenerativeAIOptions? opts = null, IPipelineTracer? tracer = null)
        : this(CreateConfiguredChatClient(opts), tracer)
    {
    }

    private GenerativeAIClient((IChatClient ChatClient, string? ModelName) configuredClient, IPipelineTracer? tracer)
        : this(configuredClient.ChatClient, tracer, configuredClient.ModelName)
    {
    }

    public GenerativeAIClient(
        IChatClient chatClient,
        IPipelineTracer? tracer = null,
        string? modelName = null,
        Action<string>? statusLogger = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _tracer = tracer ?? NullTracer.Instance;
        _modelName = modelName;
        _statusLogger = statusLogger ?? Console.WriteLine;
    }

    public async Task<string> GetChatCompletionAsync(
        string systemPrompt,
        string userPrompt,
        int maxTokens = 8000,
        CancellationToken ct = default,
        string? toolOrNamespace = null,
        string? operation = null,
        ReasoningEffort? reasoningEffort = null,
        ChatResponseFormat? responseFormat = null)
    {
        if (IsOfflineModeActive())
        {
            throw new AiEndpointOfflineException(
                $"Azure OpenAI calls are disabled for this run ({OfflineEnvironmentVariable} is set). " +
                "An earlier live endpoint probe failed at pipeline bootstrap and the operator explicitly " +
                "chose partial deterministic/verbatim-only continuation. No further Azure OpenAI calls " +
                $"are made for the remainder of this run (requested by: {toolOrNamespace ?? "unknown"} / {operation ?? "GetChatCompletion"}).");
        }

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = maxTokens,
            Reasoning = reasoningEffort is null
                ? null
                : new ReasoningOptions { Effort = reasoningEffort },
            ResponseFormat = responseFormat
        };

        var sw = Stopwatch.StartNew();
        ChatResponse response;
        try
        {
            response = await _chatClient.GetResponseAsync(messages, options, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogHttpStatus(
                GetHttpStatusCode(ex)?.ToString() ?? "unavailable",
                "failure",
                operation,
                toolOrNamespace,
                sw.ElapsedMilliseconds);
            throw;
        }

        sw.Stop();
        LogHttpStatus("200", "success", operation, toolOrNamespace, sw.ElapsedMilliseconds);

        var responseText = response.Messages.FirstOrDefault()?.Text ?? string.Empty;
        if (response.FinishReason == Microsoft.Extensions.AI.ChatFinishReason.Length)
        {
            throw new AiResponseTruncatedException(
                responseText,
                response.Usage?.TotalTokenCount ?? 0,
                maxTokens);
        }

        _tracer.RecordAiCall(new AiInteractionRecord
        {
            SkillOrToolName = toolOrNamespace ?? "unknown",
            Operation = operation ?? "GetChatCompletion",
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            ResponseContent = responseText,
            Model = _modelName ?? "unknown",
            TotalTokens = response.Usage?.TotalTokenCount is long totalTokenCount ? (int?)totalTokenCount : null,
            DurationMs = sw.ElapsedMilliseconds,
            RetryCount = 0
        });

        return responseText;
    }

    private void LogHttpStatus(
        string status,
        string outcome,
        string? operation,
        string? toolOrNamespace,
        long durationMs)
        => _statusLogger(
            $"[Azure OpenAI] status={status} outcome={outcome} " +
            $"operation={operation ?? "GetChatCompletion"} " +
            $"target={toolOrNamespace ?? "unknown"} durationMs={durationMs}");

    private static int? GetHttpStatusCode(Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
            {
                var status = GetHttpStatusCode(innerException);
                if (status.HasValue)
                {
                    return status;
                }
            }
        }

        if (exception is ClientResultException { Status: > 0 } clientResultException)
        {
            return clientResultException.Status;
        }

        if (exception is RequestFailedException { Status: > 0 } requestFailedException)
        {
            return requestFailedException.Status;
        }

        if (exception is HttpRequestException { StatusCode: not null } httpRequestException)
        {
            return (int)httpRequestException.StatusCode.Value;
        }

        return exception.InnerException is null
            ? null
            : GetHttpStatusCode(exception.InnerException);
    }

    /// <summary>
    /// Public helper that extracts an HTTP status code (if any) from an exception thrown by the
    /// underlying chat client — <see cref="ClientResultException"/>, <see cref="RequestFailedException"/>,
    /// <see cref="HttpRequestException"/>, or any of these wrapped in an <see cref="AggregateException"/>.
    /// Callers (e.g. <c>HorizontalArticleGenerator.IsRetryableAiFailure</c>) use this to distinguish
    /// non-retryable client errors (400/401) from transient failures worth retrying, without duplicating
    /// the status-extraction logic.
    /// </summary>
    public static int? TryGetHttpStatusCode(Exception exception) => GetHttpStatusCode(exception);

    public static int CalculateDynamicMaxTokens(
        string systemPrompt,
        string userPrompt,
        double outputMultiplier = 2.0,
        int minimumTokens = 1500,
        int maximumTokens = 16384)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputMultiplier);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumTokens);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTokens);

        if (minimumTokens > maximumTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTokens), "minimumTokens cannot exceed maximumTokens.");
        }

        var systemLength = systemPrompt?.Length ?? 0;
        var userLength = userPrompt?.Length ?? 0;
        var estimatedInputTokens = (int)Math.Ceiling((systemLength + userLength) / (double)CharactersPerTokenEstimate);
        var estimatedOutputTokens = (int)Math.Ceiling(estimatedInputTokens * outputMultiplier);

        return Math.Clamp(estimatedOutputTokens, minimumTokens, maximumTokens);
    }

    private static (IChatClient ChatClient, string? ModelName) CreateConfiguredChatClient(GenerativeAIOptions? opts)
    {
        var resolvedOptions = opts ?? GenerativeAIOptions.LoadFromEnvironmentOrDotEnv();
        return (CreateChatClient(resolvedOptions), resolvedOptions.Deployment);
    }

    private static IChatClient CreateChatClient(GenerativeAIOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        if (string.IsNullOrEmpty(opts.Endpoint) ||
            string.IsNullOrEmpty(opts.Deployment) ||
            (string.IsNullOrEmpty(opts.ApiKey) && !opts.UseDefaultCredential))
        {
            throw new InvalidOperationException("Azure OpenAI configuration incomplete");
        }

        var azureClient = string.IsNullOrWhiteSpace(opts.ApiKey)
            ? new AzureOpenAIClient(new Uri(opts.Endpoint!), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(opts.Endpoint!), new ApiKeyCredential(opts.ApiKey));

        var chatClient = azureClient.GetChatClient(opts.Deployment!).AsIChatClient();

        return new ChatClientBuilder(chatClient)
            .Use(
                static (messages, options, next, cancellationToken) =>
                    ExecuteWithRetryAsync(() => next.GetResponseAsync(messages, options, cancellationToken), cancellationToken),
                getStreamingResponseFunc: null)
            .Build();
    }

    private static async Task<ChatResponse> ExecuteWithRetryAsync(Func<Task<ChatResponse>> operation, CancellationToken ct)
    {
        int retryDelayMs = 1000;

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsRateLimitError(ex) && attempt < MaxRetries)
            {
                Console.WriteLine($"  ⏳ Rate limit hit, retrying in {retryDelayMs}ms (attempt {attempt + 1}/{MaxRetries})");
                await Task.Delay(retryDelayMs, ct);
                retryDelayMs *= 2;
            }
        }
    }

    private static bool IsRateLimitError(Exception ex)
    {
        return ex.Message.Contains("429") ||
               ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase);
    }
}
