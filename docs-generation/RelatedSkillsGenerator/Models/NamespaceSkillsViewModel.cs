// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace RelatedSkillsGenerator.Models;

/// <summary>
/// View model passed to the Handlebars template for rendering related-skills pages.
/// </summary>
public record NamespaceSkillsViewModel(
    string McpNamespace,
    string BrandName,
    string GeneratedAt,
    string CliVersion,
    List<ResolvedSkill> Skills,
    List<ResolvedBundle> Bundles
);

/// <summary>
/// A fully resolved skill with parsed content ready for template rendering.
/// </summary>
public record ResolvedSkill(
    string Name,
    string Description,
    string Category,
    string SkillUrl
);

/// <summary>
/// A bundle that contains at least one skill related to the namespace.
/// </summary>
public record ResolvedBundle(
    string Name,
    string Description,
    string BundleUrl
);
