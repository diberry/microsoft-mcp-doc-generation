using System.Text.Json.Serialization;

namespace Azure.Mcp.TextTransformation.Models;

/// <summary>
/// Root configuration object for all text transformations.
/// </summary>
public class TransformationConfig
{
    /// <summary>
    /// Central lexicon containing all canonical terms and their variations.
    /// </summary>
    [JsonPropertyName("lexicon")]
    public Lexicon Lexicon { get; set; } = new();

    /// <summary>
    /// Service-specific mappings and configurations.
    /// </summary>
    [JsonPropertyName("services")]
    public ServiceConfig Services { get; set; } = new();

    /// <summary>
    /// Context-specific transformation rules.
    /// </summary>
    [JsonPropertyName("contexts")]
    public Dictionary<string, ContextRules> Contexts { get; set; } = new();

    /// <summary>
    /// Default transformation rules by category.
    /// </summary>
    [JsonPropertyName("categoryDefaults")]
    public Dictionary<string, CategoryDefaults> CategoryDefaults { get; set; } = new();

    /// <summary>
    /// Parameter-specific mappings and configurations.
    /// </summary>
    [JsonPropertyName("parameters")]
    public ParameterConfig Parameters { get; set; } = new();
}

/// <summary>
/// Service configuration containing mappings.
/// </summary>
public class ServiceConfig
{
    /// <summary>
    /// List of service mappings from MCP names to display names.
    /// </summary>
    [JsonPropertyName("mappings")]
    public List<ServiceMapping> Mappings { get; set; } = new();
}

/// <summary>
/// Parameter configuration containing mappings.
/// </summary>
public class ParameterConfig
{
    /// <summary>
    /// List of parameter name mappings.
    /// </summary>
    [JsonPropertyName("mappings")]
    public List<ParameterMapping> Mappings { get; set; } = new();
}

/// <summary>
/// Default transformation rules for a category of terms.
/// </summary>
public class CategoryDefaults
{
    /// <summary>
    /// How to transform the term in filenames.
    /// </summary>
    [JsonPropertyName("filenameTransform")]
    public string? FilenameTransform { get; set; }

    /// <summary>
    /// How to transform the term in display text.
    /// </summary>
    [JsonPropertyName("displayTransform")]
    public string? DisplayTransform { get; set; }

    /// <summary>
    /// Whether to preserve the term in title case.
    /// </summary>
    [JsonPropertyName("preserveInTitleCase")]
    public bool? PreserveInTitleCase { get; set; }
}
