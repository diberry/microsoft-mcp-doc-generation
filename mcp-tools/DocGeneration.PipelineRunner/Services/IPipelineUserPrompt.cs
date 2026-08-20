namespace PipelineRunner.Services;

/// <summary>
/// Seam for asking an interactive operator whether to continue after a fatal, early Azure OpenAI
/// live-probe failure. Non-interactive/redirected-input runs must never block waiting on input —
/// implementations report that up front via <see cref="CanPromptInteractively"/> so the caller can
/// fail fast with a nonzero exit code instead of hanging indefinitely.
/// </summary>
public interface IPipelineUserPrompt
{
    /// <summary>
    /// True when a human can plausibly answer a prompt right now (an interactive terminal is
    /// attached to standard input). False for redirected/piped/CI/non-interactive runs.
    /// </summary>
    bool CanPromptInteractively();

    /// <summary>
    /// Asks the operator a yes/no question. Only meaningful when
    /// <see cref="CanPromptInteractively"/> is true; implementations must return
    /// <see langword="false"/> without blocking when it is not.
    /// </summary>
    bool Confirm(string message);
}
