using System.Text.Json.Serialization;

namespace Azure.Mcp.TextTransformation.Models;

/// <summary>
/// Context-specific transformation rules.
/// </summary>
public class ContextRules
{
    /// <summary>
    /// General rules for this context.
    /// </summary>
    [JsonPropertyName("rules")]
    public Dictionary<string, string> Rules { get; set; } = new();

    /// <summary>
    /// Specific exclusions for this context.
    /// </summary>
    [JsonPropertyName("exclusions")]
    public List<string>? Exclusions { get; set; }

    /// <summary>
    /// Specific inclusions for this context.
    /// </summary>
    [JsonPropertyName("inclusions")]
    public List<string>? Inclusions { get; set; }

    /// <summary>
    /// Whether to apply category defaults in this context.
    /// </summary>
    [JsonPropertyName("applyCategoryDefaults")]
    public bool ApplyCategoryDefaults { get; set; } = true;
}
