// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace RelatedSkillsGenerator.Models;

/// <summary>
/// Maps an MCP namespace to one or more Agent Skills.
/// </summary>
public record SkillMapping(
    string McpNamespace,
    string BrandName,
    List<SkillReference> Skills
);

/// <summary>
/// A reference to a single skill and its relationship to the MCP namespace.
/// </summary>
public record SkillReference(
    string Name,
    string Relationship
);
