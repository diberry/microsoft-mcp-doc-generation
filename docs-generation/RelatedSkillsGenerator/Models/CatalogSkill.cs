// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace RelatedSkillsGenerator.Models;

/// <summary>
/// A skill entry parsed from CATALOG.md with its category context.
/// </summary>
public record CatalogSkill(
    string Name,
    string Description,
    string Category,
    string SkillUrl
);
