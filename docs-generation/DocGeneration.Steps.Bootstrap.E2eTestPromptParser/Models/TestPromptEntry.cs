namespace E2eTestPromptParser.Models;

/// <summary>
/// A single test prompt entry mapping a tool name to a natural language prompt.
/// </summary>
public sealed record TestPromptEntry(string ToolName, string TestPrompt);
