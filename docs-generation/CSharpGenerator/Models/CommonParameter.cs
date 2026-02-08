namespace CSharpGenerator.Models;

public class CommonParameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsRequired { get; set; }
    public string Description { get; set; } = "";
    public double UsagePercent { get; set; }
    public bool IsHidden { get; set; }
    public string Source { get; set; } = "";
    public string RequiredText { get; set; } = "";
    public string NL_Name { get; set; } = "";
}

/// <summary>
/// Model for deserializing common parameters from JSON configuration file
/// </summary>
public class CommonParameterDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsRequired { get; set; }
    public string Description { get; set; } = "";
}

