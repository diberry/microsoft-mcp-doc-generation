// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace RelatedSkillsGenerator.Models;

/// <summary>
/// A curated skill bundle parsed from BUNDLES.md.
/// </summary>
public record BundleData(
    string Name,
    string Description,
    string AnchorId,
    List<string> SkillNames
);
