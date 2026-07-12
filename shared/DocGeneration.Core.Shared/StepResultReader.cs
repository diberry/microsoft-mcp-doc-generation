using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Shared;

/// <summary>
/// Reads a <see cref="StepResultFile"/> from step-result.json in a directory.
/// Used by PipelineRunner to detect structured step results.
/// Falls back gracefully when no file exists (backward compatibility).
/// </summary>
public static class StepResultReader
{
    /// <summary>
    /// The only recognized semantic schema version. Files with any other non-empty
    /// <c>schemaVersion</c> value trigger a <see cref="StepResultSchemaException"/>.
    /// Null/absent or empty/whitespace values are treated as legacy v0 and never throw.
    /// </summary>
    private const string RecognizedSchemaVersion = "1.0";

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
    /// <exception cref="StepResultSchemaException">
    /// Thrown when the file contains a <c>schemaVersion</c> field with an unrecognized value.
    /// Files without <c>schemaVersion</c> (or with an empty/whitespace value) are treated as
    /// legacy v0 and never throw.
    /// </exception>
    public static bool TryRead(string directory, out StepResultFile? result)
    {
        result = null;

        var filePath = Path.Combine(directory, StepResultWriter.FileName);
        if (!File.Exists(filePath))
            return false;

        StepResultFile? deserialized;
        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            deserialized = JsonSerializer.Deserialize<StepResultFile>(json, DeserializerOptions);
            if (deserialized is null)
                return false;
        }
        catch (Exception)
        {
            return false;
        }

        // Schema version check runs outside the catch so StepResultSchemaException propagates.
        ValidateSchemaVersion(deserialized.SchemaVersion);
        result = deserialized;
        return true;
    }

    /// <summary>
    /// Reads and validates step-result.json for the given <paramref name="stepName"/> within
    /// <paramref name="workspaceDirectory"/>. The file is expected at
    /// <c>{workspaceDirectory}/{stepName}/step-result.json</c>.
    /// </summary>
    /// <exception cref="FileNotFoundException">The step result file does not exist.</exception>
    /// <exception cref="InvalidOperationException">The file is empty or cannot be deserialized.</exception>
    /// <exception cref="StepResultSchemaException">
    /// The file declares an unrecognized <c>schemaVersion</c>.
    /// </exception>
    public static async Task<StepResultFile> ReadAsync(string stepName, string workspaceDirectory)
    {
        var directory = Path.Combine(workspaceDirectory, stepName);
        var filePath = Path.Combine(directory, StepResultWriter.FileName);

        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                $"Step result not found for step '{stepName}' in '{workspaceDirectory}'.", filePath);

        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException(
                $"Step result file is empty for step '{stepName}'.");

        var result = JsonSerializer.Deserialize<StepResultFile>(json, DeserializerOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize step result for step '{stepName}'.");

        ValidateSchemaVersion(result.SchemaVersion);
        return result;
    }

    /// <summary>
    /// Validates the schema version string from a deserialized file.
    /// Null/absent OR empty/whitespace is treated as "not declared" (legacy v0) and never
    /// throws. Only a non-empty, unrecognized value throws.
    /// </summary>
    private static void ValidateSchemaVersion(string? schemaVersion)
    {
        if (string.IsNullOrWhiteSpace(schemaVersion))
            return; // Not declared (null/absent or empty) — legacy v0, backward compatible.

        if (schemaVersion == RecognizedSchemaVersion)
            return; // Known version — all good.

        throw new StepResultSchemaException(
            $"Unrecognized step-result schemaVersion '{schemaVersion}'. " +
            $"This reader supports null (legacy v0) and '{RecognizedSchemaVersion}'.",
            actualVersion: schemaVersion);
    }
}
