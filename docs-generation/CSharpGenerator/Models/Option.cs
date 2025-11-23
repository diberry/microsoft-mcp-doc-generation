namespace CSharpGenerator.Models;

public class Option
{
    public string? Name { get; set; }
    public string? NL_Name { get; set; }
    public string? Type { get; set; }
    public bool Required { get; set; }
    public string RequiredText { get; set; } = "";
    public string? Description { get; set; }
}
