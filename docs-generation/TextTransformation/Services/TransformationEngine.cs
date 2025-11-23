using Azure.Mcp.TextTransformation.Models;

namespace Azure.Mcp.TextTransformation.Services;

/// <summary>
/// Core transformation engine that orchestrates all text transformations.
/// </summary>
public class TransformationEngine
{
    private readonly TransformationConfig _config;
    private readonly TextNormalizer _textNormalizer;
    private readonly FilenameGenerator _filenameGenerator;

    /// <summary>
    /// Initializes a new instance of the TransformationEngine class.
    /// </summary>
    /// <param name="config">The transformation configuration.</param>
    public TransformationEngine(TransformationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _textNormalizer = new TextNormalizer(config);
        _filenameGenerator = new FilenameGenerator(config);
    }

    /// <summary>
    /// Gets the TextNormalizer for text transformations.
    /// </summary>
    public TextNormalizer TextNormalizer => _textNormalizer;

    /// <summary>
    /// Gets the FilenameGenerator for filename operations.
    /// </summary>
    public FilenameGenerator FilenameGenerator => _filenameGenerator;

    /// <summary>
    /// Gets a service display name from an MCP name.
    /// </summary>
    public string GetServiceDisplayName(string mcpName)
    {
        if (string.IsNullOrWhiteSpace(mcpName))
        {
            return string.Empty;
        }

        var mapping = _config.Services.Mappings.FirstOrDefault(m => 
            m.McpName.Equals(mcpName, StringComparison.OrdinalIgnoreCase));
        
        return mapping?.BrandName ?? _textNormalizer.ToTitleCase(mcpName, "display");
    }

    /// <summary>
    /// Gets a service short name from an MCP name.
    /// </summary>
    public string GetServiceShortName(string mcpName)
    {
        if (string.IsNullOrWhiteSpace(mcpName))
        {
            return string.Empty;
        }

        var mapping = _config.Services.Mappings.FirstOrDefault(m => 
            m.McpName.Equals(mcpName, StringComparison.OrdinalIgnoreCase));
        
        return mapping?.ShortName ?? mcpName;
    }

    /// <summary>
    /// Transforms a description text (applies replacements and ensures period).
    /// </summary>
    public string TransformDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var transformed = _textNormalizer.ReplaceStaticText(description);
        return _textNormalizer.EnsureEndsPeriod(transformed);
    }

    /// <summary>
    /// Gets the transformation configuration.
    /// </summary>
    public TransformationConfig Config => _config;
}
