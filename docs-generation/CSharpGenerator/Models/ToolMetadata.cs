using System.Text.Json.Serialization;

namespace CSharpGenerator.Models;

public class ToolMetadata
{
    [JsonPropertyName("destructive")]
    public MetadataValue? Destructive { get; set; }
    
    [JsonPropertyName("idempotent")]
    public MetadataValue? Idempotent { get; set; }
    
    [JsonPropertyName("openWorld")]
    public MetadataValue? OpenWorld { get; set; }
    
    [JsonPropertyName("readOnly")]
    public MetadataValue? ReadOnly { get; set; }
    
    [JsonPropertyName("secret")]
    public MetadataValue? Secret { get; set; }
    
    [JsonPropertyName("localRequired")]
    public MetadataValue? LocalRequired { get; set; }
}
