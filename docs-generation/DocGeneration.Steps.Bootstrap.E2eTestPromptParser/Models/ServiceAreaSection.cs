namespace E2eTestPromptParser.Models;

/// <summary>
/// An Azure service area section containing its heading and associated test prompt entries.
/// </summary>
public sealed record ServiceAreaSection(string Heading, IReadOnlyList<TestPromptEntry> Entries);
