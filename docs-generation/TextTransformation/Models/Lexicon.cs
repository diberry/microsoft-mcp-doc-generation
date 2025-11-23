using System.Text.Json.Serialization;

namespace Azure.Mcp.TextTransformation.Models;

/// <summary>
/// Central lexicon containing all canonical terms and their variations.
/// </summary>
public class Lexicon
{
    /// <summary>
    /// Dictionary of acronyms with their canonical forms and plurals.
    /// </summary>
    [JsonPropertyName("acronyms")]
    public Dictionary<string, AcronymEntry> Acronyms { get; set; } = new();

    /// <summary>
    /// Dictionary of compound words that should be separated.
    /// </summary>
    [JsonPropertyName("compoundWords")]
    public Dictionary<string, CompoundWordEntry> CompoundWords { get; set; } = new();

    /// <summary>
    /// List of words to remove in certain contexts.
    /// </summary>
    [JsonPropertyName("stopWords")]
    public List<string> StopWords { get; set; } = new();

    /// <summary>
    /// Dictionary of abbreviations with their canonical forms and expansions.
    /// </summary>
    [JsonPropertyName("abbreviations")]
    public Dictionary<string, AbbreviationEntry> Abbreviations { get; set; } = new();

    /// <summary>
    /// Dictionary of Azure-specific terms with display names and descriptions.
    /// </summary>
    [JsonPropertyName("azureTerms")]
    public Dictionary<string, AzureTermEntry> AzureTerms { get; set; } = new();
}

/// <summary>
/// Represents an acronym with its canonical form and plural.
/// </summary>
public class AcronymEntry
{
    /// <summary>
    /// The canonical form of the acronym (e.g., "ID", "VM").
    /// </summary>
    [JsonPropertyName("canonical")]
    public string Canonical { get; set; } = string.Empty;

    /// <summary>
    /// The plural form of the acronym (e.g., "IDs", "VMs").
    /// </summary>
    [JsonPropertyName("plural")]
    public string? Plural { get; set; }

    /// <summary>
    /// Optional expansion of the acronym.
    /// </summary>
    [JsonPropertyName("expansion")]
    public string? Expansion { get; set; }

    /// <summary>
    /// Whether the acronym should be preserved in title case.
    /// </summary>
    [JsonPropertyName("preserveInTitleCase")]
    public bool PreserveInTitleCase { get; set; } = true;
}

/// <summary>
/// Represents a compound word with its components.
/// </summary>
public class CompoundWordEntry
{
    /// <summary>
    /// The components that make up the compound word (e.g., ["node", "pool"]).
    /// </summary>
    [JsonPropertyName("components")]
    public List<string> Components { get; set; } = new();

    /// <summary>
    /// How to join the components in different contexts.
    /// </summary>
    [JsonPropertyName("joinStrategy")]
    public string JoinStrategy { get; set; } = "hyphenate";

    /// <summary>
    /// Optional preferred display form.
    /// </summary>
    [JsonPropertyName("displayForm")]
    public string? DisplayForm { get; set; }
}

/// <summary>
/// Represents an abbreviation with its canonical form and expansion.
/// </summary>
public class AbbreviationEntry
{
    /// <summary>
    /// The canonical form of the abbreviation (e.g., "e.g.", "i.e.").
    /// </summary>
    [JsonPropertyName("canonical")]
    public string Canonical { get; set; } = string.Empty;

    /// <summary>
    /// The full expansion of the abbreviation (e.g., "for example").
    /// </summary>
    [JsonPropertyName("expansion")]
    public string? Expansion { get; set; }
}

/// <summary>
/// Represents an Azure-specific term with display information.
/// </summary>
public class AzureTermEntry
{
    /// <summary>
    /// The display name for the term.
    /// </summary>
    [JsonPropertyName("display")]
    public string Display { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the term.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Optional reference to another lexicon entry (e.g., "$lexicon.acronyms.aks").
    /// </summary>
    [JsonPropertyName("ref")]
    public string? Ref { get; set; }
}
