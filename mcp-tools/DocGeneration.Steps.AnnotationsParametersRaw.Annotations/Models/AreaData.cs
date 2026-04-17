namespace CSharpGenerator.Models;

public class AreaData
{
    public string Description { get; set; } = "";
    public int ToolCount { get; set; }
    public List<Tool> Tools { get; set; } = new();
}
