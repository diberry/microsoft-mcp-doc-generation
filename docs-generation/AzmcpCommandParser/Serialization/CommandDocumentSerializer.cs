using System.Text.Json;
using System.Text.Json.Serialization;
using AzmcpCommandParser.Models;

namespace AzmcpCommandParser.Serialization;

/// <summary>
/// Serializes <see cref="CommandDocument"/> to JSON with configurable options.
/// </summary>
public static class CommandDocumentSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Serializes a <see cref="CommandDocument"/> to a JSON string.
    /// </summary>
    public static string Serialize(CommandDocument document, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(document, options ?? DefaultOptions);
    }

    /// <summary>
    /// Serializes a <see cref="CommandDocument"/> to a JSON file.
    /// </summary>
    public static void SerializeToFile(CommandDocument document, string filePath, JsonSerializerOptions? options = null)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = Serialize(document, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Deserializes a <see cref="CommandDocument"/> from a JSON string.
    /// </summary>
    public static CommandDocument? Deserialize(string json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<CommandDocument>(json, options ?? DefaultOptions);
    }

    /// <summary>
    /// Deserializes a <see cref="CommandDocument"/> from a JSON file.
    /// </summary>
    public static CommandDocument? DeserializeFromFile(string filePath, JsonSerializerOptions? options = null)
    {
        var json = File.ReadAllText(filePath);
        return Deserialize(json, options);
    }
}
