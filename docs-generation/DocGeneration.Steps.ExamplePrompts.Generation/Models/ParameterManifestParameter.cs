using System.Text.Json.Serialization;

namespace ExamplePromptGeneratorStandalone.Models;

public sealed class ParameterManifestParameter
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("requiredText")]
    public string? RequiredText { get; set; }

    [JsonPropertyName("isConditionalRequired")]
    public bool IsConditionalRequired { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
