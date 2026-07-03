namespace Shared;

/// <summary>
/// Thrown by <see cref="StepResultReader"/> when a step-result.json file declares a
/// <c>schemaVersion</c> that is present but not recognized by this version of the reader.
/// Files without a <c>schemaVersion</c> field are treated as legacy v0 and never throw.
/// </summary>
public class StepResultSchemaException : Exception
{
    /// <summary>The unrecognized schema version string found in the file, or null if unavailable.</summary>
    public string? ActualVersion { get; }

    /// <inheritdoc cref="Exception(string)"/>
    public StepResultSchemaException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance with the unrecognized schema version attached for diagnostics.
    /// </summary>
    public StepResultSchemaException(string message, string? actualVersion)
        : base(message)
    {
        ActualVersion = actualVersion;
    }
}
