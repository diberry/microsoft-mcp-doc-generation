// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// TDD tests for Issue #416, Item 5: supported-azure-services.md link updater.
/// When a tool family file is renamed, the corresponding link in
/// supported-azure-services.md must be updated. Typos in service names
/// should be detected/prevented. Resolved URLs should be valid.
/// </summary>
public class SupportedServicesLinkUpdaterTests
{
    // ── Link update on rename ───────────────────────────────────────

    [Fact]
    public void UpdateLinks_RenamedFile_UpdatesCorrespondingLink()
    {
        var supportedServicesContent =
            "| Service | Link |\n" +
            "|---------|------|\n" +
            "| Storage | [Storage tools](azure-mcp-server-storage.md) |\n" +
            "| Compute | [Compute tools](azure-mcp-server-compute.md) |\n";

        var oldFileName = "azure-mcp-server-storage.md";
        var newFileName = "azure-mcp-server-azure-storage.md";

        var result = SupportedServicesLinkUpdater.UpdateLink(
            supportedServicesContent, oldFileName, newFileName);

        Assert.Contains("azure-mcp-server-azure-storage.md", result);
        Assert.DoesNotContain("azure-mcp-server-storage.md", result);
        // Other links unchanged
        Assert.Contains("azure-mcp-server-compute.md", result);
    }

    [Fact]
    public void UpdateLinks_FileNotInList_ReturnsUnchanged()
    {
        var supportedServicesContent =
            "| Service | Link |\n" +
            "|---------|------|\n" +
            "| Storage | [Storage tools](azure-mcp-server-storage.md) |\n";

        var oldFileName = "azure-mcp-server-nonexistent.md";
        var newFileName = "azure-mcp-server-new.md";

        var result = SupportedServicesLinkUpdater.UpdateLink(
            supportedServicesContent, oldFileName, newFileName);

        Assert.Equal(supportedServicesContent, result);
    }

    // ── Typo detection in service names ──────────────────────────────

    [Fact]
    public void ValidateServiceNames_DetectsTypo()
    {
        var serviceNames = new[] { "Storage", "Compue", "Key Vault" }; // "Compue" is a typo
        var knownServices = new[] { "Storage", "Compute", "Key Vault", "Cosmos DB" };

        var errors = SupportedServicesLinkUpdater.ValidateServiceNames(serviceNames, knownServices);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Compue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateServiceNames_AllValid_NoErrors()
    {
        var serviceNames = new[] { "Storage", "Compute", "Key Vault" };
        var knownServices = new[] { "Storage", "Compute", "Key Vault", "Cosmos DB" };

        var errors = SupportedServicesLinkUpdater.ValidateServiceNames(serviceNames, knownServices);

        Assert.Empty(errors);
    }

    // ── URL validation ──────────────────────────────────────────────

    [Fact]
    public void ValidateLearnUrl_WellFormedUrl_ReturnsValid()
    {
        var url = "/azure/developer/azure-mcp-server/azure-mcp-server-storage";

        var result = SupportedServicesLinkUpdater.IsWellFormedLearnUrl(url);

        Assert.True(result, "Site-root-relative URL should be considered well-formed");
    }

    [Fact]
    public void ValidateLearnUrl_EmptyUrl_ReturnsInvalid()
    {
        var result = SupportedServicesLinkUpdater.IsWellFormedLearnUrl("");

        Assert.False(result, "Empty URL should be invalid");
    }

    [Fact]
    public void ValidateLearnUrl_RelativeMdLink_ReturnsValid()
    {
        var url = "azure-mcp-server-storage.md";

        var result = SupportedServicesLinkUpdater.IsWellFormedLearnUrl(url);

        Assert.True(result, "Relative .md link should be valid");
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void UpdateLinks_EmptyContent_ReturnsEmpty()
    {
        var result = SupportedServicesLinkUpdater.UpdateLink("", "old.md", "new.md");

        Assert.Equal("", result);
    }

    [Fact]
    public void UpdateLinks_NullOldName_ReturnsUnchanged()
    {
        var content = "Some content";

        var result = SupportedServicesLinkUpdater.UpdateLink(content, null!, "new.md");

        Assert.Equal(content, result);
    }
}
