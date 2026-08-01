namespace ExamplePromptGeneratorStandalone.Models;

public sealed class Option
{
    public string? Name { get; set; }
    public bool Required { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// The human-readable display name from the parameter manifest (e.g., "App name" for CLI "--app").
    /// Used by Step 4 validation. When null, coverage checks fall back to the canonicalized CLI name.
    /// </summary>
    public string? DisplayName { get; set; }
}
