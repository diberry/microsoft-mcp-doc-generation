namespace GenerativeAI;

public sealed class AiResponseTruncatedException : InvalidOperationException
{
    public AiResponseTruncatedException(
        string responseContent,
        long totalTokens,
        int maxOutputTokens)
        : base(
            $"LLM response was truncated due to token limit. " +
            $"Used tokens: {totalTokens}, Max output tokens: {maxOutputTokens}. " +
            "Consider increasing maxTokens parameter.")
    {
        ResponseContent = responseContent;
        TotalTokens = totalTokens;
        MaxOutputTokens = maxOutputTokens;
    }

    public string ResponseContent { get; }

    public long TotalTokens { get; }

    public int MaxOutputTokens { get; }
}
