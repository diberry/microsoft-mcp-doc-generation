using System.Text.Json;

namespace Azure.Mcp.TextTransformation.Models;

/// <summary>
/// Loads and manages transformation configuration.
/// </summary>
public class ConfigLoader
{
    private readonly string _configPath;
    private TransformationConfig? _config;

    /// <summary>
    /// Initializes a new instance of ConfigLoader.
    /// </summary>
    /// <param name="configPath">Path to the transformation-config.json file.</param>
    public ConfigLoader(string configPath)
    {
        _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
    }

    /// <summary>
    /// Loads the transformation configuration from the JSON file.
    /// </summary>
    /// <returns>The loaded configuration.</returns>
    public async Task<TransformationConfig> LoadAsync()
    {
        if (_config != null)
        {
            return _config;
        }

        if (!File.Exists(_configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {_configPath}");
        }

        var json = await File.ReadAllTextAsync(_configPath);
        _config = JsonSerializer.Deserialize<TransformationConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (_config == null)
        {
            throw new InvalidOperationException("Failed to deserialize configuration.");
        }

        // Resolve references
        ResolveReferences(_config);

        return _config;
    }

    /// <summary>
    /// Resolves $ref-style references in the configuration.
    /// </summary>
    private void ResolveReferences(TransformationConfig config)
    {
        // Resolve service mappings
        foreach (var mapping in config.Services.Mappings)
        {
            if (mapping.ShortName?.StartsWith("$lexicon.") == true)
            {
                mapping.ShortName = ResolveLexiconReference(config, mapping.ShortName);
            }
            if (mapping.BrandName?.StartsWith("$lexicon.") == true)
            {
                mapping.BrandName = ResolveLexiconReference(config, mapping.BrandName);
            }
        }
    }

    /// <summary>
    /// Resolves a lexicon reference (e.g., "$lexicon.acronyms.aks" -> "AKS").
    /// </summary>
    private string ResolveLexiconReference(TransformationConfig config, string reference)
    {
        if (!reference.StartsWith("$lexicon."))
        {
            return reference;
        }

        var parts = reference.Split('.');
        if (parts.Length < 3)
        {
            return reference;
        }

        var category = parts[1];
        var key = parts[2];
        var property = parts.Length > 3 ? parts[3] : "canonical";

        return category switch
        {
            "acronyms" => ResolveAcronym(config.Lexicon.Acronyms, key, property),
            "compoundWords" => ResolveCompoundWord(config.Lexicon.CompoundWords, key, property),
            "abbreviations" => ResolveAbbreviation(config.Lexicon.Abbreviations, key, property),
            "azureTerms" => ResolveAzureTerm(config.Lexicon.AzureTerms, key, property),
            _ => reference
        };
    }

    private string ResolveAcronym(Dictionary<string, AcronymEntry> acronyms, string key, string property)
    {
        if (!acronyms.TryGetValue(key, out var entry))
        {
            return key;
        }

        return property switch
        {
            "canonical" => entry.Canonical,
            "plural" => entry.Plural ?? entry.Canonical,
            "expansion" => entry.Expansion ?? entry.Canonical,
            _ => entry.Canonical
        };
    }

    private string ResolveCompoundWord(Dictionary<string, CompoundWordEntry> compoundWords, string key, string property)
    {
        if (!compoundWords.TryGetValue(key, out var entry))
        {
            return key;
        }

        return property switch
        {
            "displayForm" => entry.DisplayForm ?? string.Join("-", entry.Components),
            _ => string.Join("-", entry.Components)
        };
    }

    private string ResolveAbbreviation(Dictionary<string, AbbreviationEntry> abbreviations, string key, string property)
    {
        if (!abbreviations.TryGetValue(key, out var entry))
        {
            return key;
        }

        return property switch
        {
            "canonical" => entry.Canonical,
            "expansion" => entry.Expansion ?? entry.Canonical,
            _ => entry.Canonical
        };
    }

    private string ResolveAzureTerm(Dictionary<string, AzureTermEntry> azureTerms, string key, string property)
    {
        if (!azureTerms.TryGetValue(key, out var entry))
        {
            return key;
        }

        return property switch
        {
            "display" => entry.Display,
            "description" => entry.Description ?? entry.Display,
            _ => entry.Display
        };
    }
}
