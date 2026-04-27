// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Centralizes all metadata constants for the doc generation pipeline.
/// Single source of truth for publishing metadata, branding, and content paths.
/// Phase 0: Extracted from FrontmatterEnricher and DeterministicFrontmatterGenerator.
/// </summary>
public static class MetadataConstants
{
    // Publishing metadata
    public const string Author = "diberry";
    public const string Reviewer = "mbaldwin";
    public const string AiUsage = "ai-generated";
    public const string ContentWellValue = "AI-contribution";
    public const string MsCustom = "build-2025";
    public const string MsService = "azure-mcp-server";
    public const string MsTopic = "concept-article";

    // Branding
    public const string ProductName = "Azure MCP Server";
    public const string TitleTemplate = "{0} tools for {1}"; // ProductName, brandName
    public const string DescriptionTemplate = "Use {0} tools to manage {1} resources with natural language prompts from your IDE."; // ProductName, brandName

    // Content paths
    public const string IncludeParameterConsideration = "[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]";
}
