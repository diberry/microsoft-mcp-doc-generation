namespace CSharpGenerator.Models;

public class TransformedData
{
    public string Version { get; set; } = "";
    public List<Tool> Tools { get; set; } = new();
    public Dictionary<string, AreaData> Areas { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public List<CommonParameter> SourceDiscoveredCommonParams { get; set; } = new();
}
