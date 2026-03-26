using System;
using System.IO;
using System.Text.Json;

namespace Shared;

/// <summary>
/// Reads a <see cref="StepResultFile"/> from step-result.json in a directory.
/// Used by PipelineRunner to detect structured step results.
/// Falls back gracefully when no file exists (backward compatibility).
/// </summary>
public static class StepResultReader
{
    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Checks whether a step-result.json file exists in the given directory.
    /// </summary>
    public static bool Exists(string directory)
    {
        var filePath = Path.Combine(directory, StepResultWriter.FileName);
        return File.Exists(filePath);
    }

    /// <summary>
    /// Attempts to read and deserialize step-result.json from <paramref name="directory"/>.
    /// Returns false (with null result) when the file is missing, empty, or invalid JSON.
    /// This ensures backward compatibility — callers fall back to existing detection logic.
    /// </summary>
    public static bool TryRead(string directory, out StepResultFile? result)
    {
        result = null;

        var filePath = Path.Combine(directory, StepResultWriter.FileName);
        if (!File.Exists(filePath))
            return false;

        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            result = JsonSerializer.Deserialize<StepResultFile>(json, DeserializerOptions);
            return result is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
