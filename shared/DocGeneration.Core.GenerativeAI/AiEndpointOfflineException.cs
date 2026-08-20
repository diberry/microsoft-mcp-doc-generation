// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace GenerativeAI;

/// <summary>
/// Thrown by <see cref="GenerativeAIClient.GetChatCompletionAsync"/> when the pipeline is running
/// in the "partial_explicit" offline-continuation mode (see
/// <see cref="GenerativeAIClient.OfflineEnvironmentVariable"/>).
/// </summary>
/// <remarks>
/// This mode is entered only after PipelineRunner's very-early bootstrap live probe against the
/// configured Azure OpenAI endpoint fails, and an interactive operator explicitly chooses to
/// continue with deterministic/verbatim-only output rather than abort the run. Once active, this
/// exception is thrown for every further AI call attempt — in-process and in any child process
/// that inherits the environment variable — <b>before</b> any network I/O is attempted, so a known
/// -down endpoint is never retried. Callers that already treat AI failures as a per-artifact/
/// per-tool failure (Steps 2, 3, 4, 6) handle this exception exactly like any other AI failure:
/// deterministic/verbatim fallbacks proceed, and AI-required output is marked incomplete rather
/// than reported as fully successful.
/// </remarks>
public sealed class AiEndpointOfflineException : InvalidOperationException
{
    public AiEndpointOfflineException(string message)
        : base(message)
    {
    }
}
