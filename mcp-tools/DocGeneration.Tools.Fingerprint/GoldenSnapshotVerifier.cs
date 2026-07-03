namespace DocGeneration.Tools.Fingerprint;

/// <summary>
/// Verifies generated output against a committed golden manifest.
/// </summary>
internal sealed class GoldenSnapshotVerifier
{
    private readonly GoldenSnapshotCapture _capture;

    /// <summary>
    /// Initializes a new verifier rooted at the repository root.
    /// </summary>
    /// <param name="repoRoot">Repository root used to resolve generated directories.</param>
    public GoldenSnapshotVerifier(string repoRoot)
    {
        _capture = new GoldenSnapshotCapture(repoRoot);
    }

    /// <summary>
    /// Verifies generated output against the supplied manifest.
    /// </summary>
    /// <param name="manifest">Committed golden manifest.</param>
    /// <param name="outputDirectory">Generated output directory to validate.</param>
    /// <returns>A verification result describing pass/fail state and violations.</returns>
    public GoldenVerificationResult Verify(GoldenManifest manifest, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var actual = _capture.CaptureFromOutputDirectory(outputDirectory, manifest.Namespace);
        var violations = new List<string>();
        var notes = new List<string>();

        CompareDeterministicFiles(manifest.DeterministicFiles, actual.DeterministicFiles, violations);

        if (actual.AiFiles.Count == 0)
        {
            notes.Add("AI output directories were not present in the candidate output. Skipping AI structural verification.");
        }
        else
        {
            CompareAiFiles(manifest.AiFiles, actual.AiFiles, violations);
        }

        return new GoldenVerificationResult(
            violations.Count == 0,
            violations,
            notes);
    }

    private static void CompareDeterministicFiles(
        IReadOnlyDictionary<string, DeterministicFileEntry> expected,
        IReadOnlyDictionary<string, DeterministicFileEntry> actual,
        ICollection<string> violations)
    {
        foreach (var missing in expected.Keys.Except(actual.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add($"Deterministic file missing: {missing}");
        }

        foreach (var unexpected in actual.Keys.Except(expected.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add($"Unexpected deterministic file: {unexpected}");
        }

        foreach (var path in expected.Keys.Intersect(actual.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            var expectedEntry = expected[path];
            var actualEntry = actual[path];
            if (!string.Equals(expectedEntry.Sha256, actualEntry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(
                    $"Deterministic hash mismatch: {path} (expected {expectedEntry.Sha256}, actual {actualEntry.Sha256})");
            }
        }
    }

    private static void CompareAiFiles(
        IReadOnlyDictionary<string, AiFileEntry> expected,
        IReadOnlyDictionary<string, AiFileEntry> actual,
        ICollection<string> violations)
    {
        foreach (var missing in expected.Keys.Except(actual.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add($"AI file missing: {missing}");
        }

        foreach (var unexpected in actual.Keys.Except(expected.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add($"Unexpected AI file: {unexpected}");
        }

        foreach (var path in expected.Keys.Intersect(actual.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            var expectedEntry = expected[path];
            var actualEntry = actual[path];

            var missingKeys = expectedEntry.RequiredKeys
                .Except(actualEntry.RequiredKeys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missingKeys.Length > 0)
            {
                violations.Add($"AI required keys missing: {path} ({string.Join(", ", missingKeys)})");
            }

            if (Math.Abs(actualEntry.SectionCount - expectedEntry.SectionCount) > 1)
            {
                violations.Add(
                    $"AI section count mismatch: {path} (expected {expectedEntry.SectionCount}, actual {actualEntry.SectionCount})");
            }
        }
    }
}

/// <summary>
/// Immutable result for golden verification runs.
/// </summary>
/// <param name="Succeeded">True when no violations were detected.</param>
/// <param name="Violations">Human-readable verification failures.</param>
/// <param name="Notes">Informational notes about skipped checks.</param>
internal sealed record GoldenVerificationResult(
    bool Succeeded,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> Notes);
