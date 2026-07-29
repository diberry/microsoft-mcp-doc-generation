// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ExamplePromptGeneratorStandalone.Models;

namespace ExamplePromptGeneratorStandalone.Generators;

/// <summary>
/// Builds an <see cref="ExamplePromptsResponse"/> directly from the source
/// end-to-end test prompts (e2eTestPrompts.md → parsed.json), publishing them
/// VERBATIM with no Azure OpenAI call.
///
/// Preserves the exact text, count, and order of the source prompts. The source
/// counts vary (1–19 per tool) and MUST NOT be capped or truncated — the AI-only
/// "maximum of 10" cap rule does not apply here.
///
/// Fixes: issue #748.
/// </summary>
public static class VerbatimExamplePromptBuilder
{
    /// <summary>
    /// Builds a verbatim response from the tool's source reference prompts.
    /// A defensive copy of the list is taken so the source is never mutated.
    /// </summary>
    public static ExamplePromptsResponse Build(Tool tool, List<string> referencePrompts)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(referencePrompts);

        return new ExamplePromptsResponse
        {
            ToolName = tool.Command,
            Prompts = new List<string>(referencePrompts)
        };
    }

    /// <summary>
    /// Builds the provenance note written to the <c>example-prompts-prompts/</c>
    /// debug input file for verbatim-sourced tools (in place of an AI user prompt).
    /// </summary>
    public static string BuildProvenanceNote(List<string> referencePrompts)
    {
        ArgumentNullException.ThrowIfNull(referencePrompts);
        var count = referencePrompts.Count;
        var plural = count == 1 ? "prompt" : "prompts";
        return $"verbatim — {count} source {plural} from e2eTestPrompts.md (no AI call)";
    }
}
