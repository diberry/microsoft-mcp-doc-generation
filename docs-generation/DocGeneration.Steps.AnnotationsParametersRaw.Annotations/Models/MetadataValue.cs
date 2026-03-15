using System.Text.Json.Serialization;

namespace CSharpGenerator.Models;

public class MetadataValue
{
    [JsonPropertyName("value")]
    public bool Value { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
