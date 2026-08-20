namespace PipelineRunner.Services;

/// <summary>
/// Real console-based implementation of <see cref="IPipelineUserPrompt"/>. Detects non-interactive
/// runs via <see cref="Console.IsInputRedirected"/> so CI/scripted/redirected-stdin runs never
/// block on a prompt — they simply report "cannot prompt interactively" and the caller fails fast.
/// </summary>
public sealed class ConsolePipelineUserPrompt : IPipelineUserPrompt
{
    public bool CanPromptInteractively() => !Console.IsInputRedirected;

    public bool Confirm(string message)
    {
        if (!CanPromptInteractively())
        {
            return false;
        }

        Console.Error.Write(message);
        var line = Console.ReadLine();
        return line is not null
            && (line.Trim().Equals("y", StringComparison.OrdinalIgnoreCase)
                || line.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
