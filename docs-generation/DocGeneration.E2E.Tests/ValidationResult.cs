// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DocGeneration.E2E.Tests;

/// <summary>
/// Result of a validation check containing pass/fail status and any issues found.
/// </summary>
public sealed class ValidationResult
{
    public bool Success => Issues.Count == 0;
    public List<string> Issues { get; } = new();

    public void AddIssue(string issue) => Issues.Add(issue);

    public void Merge(ValidationResult other)
    {
        Issues.AddRange(other.Issues);
    }
}
