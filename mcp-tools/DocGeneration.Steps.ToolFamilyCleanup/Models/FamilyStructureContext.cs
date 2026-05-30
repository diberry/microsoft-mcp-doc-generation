// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolFamilyCleanup.Models;

public sealed record FamilyStructureContext(
    string FamilyName,
    IReadOnlyList<FamilySection> Sections,
    string SchemaVersion = "1.0");

public sealed record FamilySection(
    string Heading,
    IReadOnlyList<string> ToolNames,
    string SourceContent);
