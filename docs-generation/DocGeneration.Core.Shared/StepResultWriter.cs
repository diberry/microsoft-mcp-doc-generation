using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Shared;

/// <summary>
/// Writes a <see cref="StepResultFile"/> as step-result.json to a directory.
/// Called by generator processes to report structured results back to PipelineRunner.
/// </summary>
public static class StepResultWriter
{
    /// <summary>
    /// Canonical filename for step results.
    /// </summary>
    public const string FileName = "step-result.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Writes the step result to <paramref name="directory"/>/step-result.json.
    /// Creates the directory if it does not exist. Overwrites any existing file.
    /// </summary>
    public static void Write(string directory, StepResultFile result)
    {
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, FileName);
        var json = JsonSerializer.Serialize(result, SerializerOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Converts <see cref="PromptSnapshot"/> records to <see cref="StepResultFile.PromptSnapshotRecord"/>
    /// and attaches them to the result. Sets version to 2.
    /// </summary>
    public static void AddPromptSnapshots(StepResultFile result, IEnumerable<PromptSnapshot> snapshots)
    {
        result.Version = 2;
        result.PromptSnapshots = snapshots.Select(s => new StepResultFile.PromptSnapshotRecord
        {
            FileName = s.FileName,
            ContentHash = s.ContentHash,
            SizeBytes = s.SizeBytes
        }).ToList();
    }
}