namespace CSharpGenerator.Models;

public class Tool
{
    public string? Name { get; set; }
    public string? Command { get; set; }
    public string? Description { get; set; }
    public string? SourceFile { get; set; }
    public List<Option>? Option { get; set; }
    public string? Area { get; set; }
    public ToolMetadata? Metadata { get; set; }
    public string? AnnotationContent { get; set; } // Content from annotation file
    public string? AnnotationFileName { get; set; } // Filename of the annotation file
    public bool HasAnnotation { get; set; } // Whether annotation file was generated
    public bool HasParameters { get; set; } // Whether parameter file was generated
    public bool HasExamplePrompts { get; set; } // Whether example prompts file was generated
}
