using Azure.Mcp.TextTransformation.Models;

namespace Azure.Mcp.TextTransformation.Services;

/// <summary>
/// Generates filenames using three-tier resolution strategy.
/// </summary>
public class FilenameGenerator
{
    private readonly TransformationConfig _config;
    private readonly TextNormalizer _textNormalizer;

    /// <summary>
    /// Initializes a new instance of the FilenameGenerator class.
    /// </summary>
    /// <param name="config">The transformation configuration.</param>
    public FilenameGenerator(TransformationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _textNormalizer = new TextNormalizer(config);
    }

    /// <summary>
    /// Generates a filename using three-tier resolution:
    /// 1. Brand name from service mappings (highest priority)
    /// 2. Compound word transformations (medium priority)
    /// 3. Original area name (fallback)
    /// </summary>
    public string GenerateFilename(string areaName, string operationPart = "", string type = "")
    {
        if (string.IsNullOrWhiteSpace(areaName))
        {
            return string.Empty;
        }

        // Tier 1: Check brand mapping
        var mapping = _config.Services.Mappings.FirstOrDefault(m => 
            m.McpName.Equals(areaName, StringComparison.OrdinalIgnoreCase));
        
        string baseFilename;
        if (mapping?.Filename != null)
        {
            baseFilename = mapping.Filename;
        }
        else if (mapping?.ShortName != null)
        {
            baseFilename = mapping.ShortName;
        }
        else
        {
            // Tier 2: Check compound words
            var compoundWord = _config.Lexicon.CompoundWords
                .FirstOrDefault(c => c.Key.Equals(areaName, StringComparison.OrdinalIgnoreCase));
            
            if (compoundWord.Key != null)
            {
                baseFilename = string.Join("-", compoundWord.Value.Components);
            }
            else
            {
                // Tier 3: Use original name
                baseFilename = areaName;
            }
        }

        // Clean the filename
        baseFilename = CleanFilename(baseFilename);

        // Build the full filename
        var parts = new List<string> { baseFilename };
        
        if (!string.IsNullOrWhiteSpace(operationPart))
        {
            parts.Add(CleanFilename(operationPart));
        }
        
        if (!string.IsNullOrWhiteSpace(type))
        {
            parts.Add(type);
        }

        return string.Join("-", parts) + ".md";
    }

    /// <summary>
    /// Cleans a filename by removing stop words and applying transformations.
    /// </summary>
    public string CleanFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return string.Empty;
        }

        // Get filename context rules
        var context = _config.Contexts.TryGetValue("filename", out var rules) 
            ? rules 
            : new ContextRules();

        // Split into words
        var words = filename.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        // Determine if we should remove stop words
        var shouldRemoveStopWords = context.Rules.TryGetValue("stopWords", out var stopWordRule) && 
                                     stopWordRule == "remove";

        foreach (var word in words)
        {
            var cleaned = word.Trim().ToLowerInvariant();
            
            // Skip stop words if rule applies (but keep first and last words)
            if (shouldRemoveStopWords && 
                _config.Lexicon.StopWords.Contains(cleaned) &&
                result.Count > 0)  // Don't remove if it's the first word
            {
                continue;
            }

            // Check if it's an acronym that needs lowercase transformation
            var acronym = _config.Lexicon.Acronyms
                .FirstOrDefault(a => a.Key.Equals(cleaned, StringComparison.OrdinalIgnoreCase));
            
            if (acronym.Key != null)
            {
                var categoryDefault = _config.CategoryDefaults.TryGetValue("acronym", out var defaults)
                    ? defaults
                    : null;
                
                if (categoryDefault?.FilenameTransform == "to-lowercase")
                {
                    result.Add(acronym.Key.ToLowerInvariant());
                }
                else
                {
                    result.Add(acronym.Key);
                }
            }
            else
            {
                result.Add(cleaned);
            }
        }

        return string.Join("-", result);
    }

    /// <summary>
    /// Generates a main service filename (single word, not cleaned).
    /// </summary>
    public string GenerateMainServiceFilename(string areaName)
    {
        if (string.IsNullOrWhiteSpace(areaName))
        {
            return string.Empty;
        }

        // Check for brand mapping
        var mapping = _config.Services.Mappings.FirstOrDefault(m => 
            m.McpName.Equals(areaName, StringComparison.OrdinalIgnoreCase));
        
        if (mapping?.Filename != null)
        {
            return mapping.Filename + ".md";
        }
        
        if (mapping?.ShortName != null)
        {
            return mapping.ShortName.ToLowerInvariant() + ".md";
        }

        // Use original name
        return areaName.ToLowerInvariant() + ".md";
    }
}
