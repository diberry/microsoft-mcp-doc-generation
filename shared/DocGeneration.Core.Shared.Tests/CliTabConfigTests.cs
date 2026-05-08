// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

public class CliTabConfigTests
{
    [Fact]
    public void IsEnabled_EmptyAllowlist_ReturnsFalse()
    {
        var config = new CliTabConfig();
        Assert.False(config.IsEnabled);
    }

    [Fact]
    public void IsEnabled_NonEmptyAllowlist_ReturnsTrue()
    {
        var config = CliTabConfig.ForNamespaces("azure-storage");
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void IsNamespaceAllowed_InList_ReturnsTrue()
    {
        var config = CliTabConfig.ForNamespaces("azure-storage", "azure-compute");
        Assert.True(config.IsNamespaceAllowed("azure-storage"));
    }

    [Fact]
    public void IsNamespaceAllowed_NotInList_ReturnsFalse()
    {
        var config = CliTabConfig.ForNamespaces("azure-storage");
        Assert.False(config.IsNamespaceAllowed("azure-network"));
    }

    [Fact]
    public void IsNamespaceAllowed_CaseInsensitive()
    {
        var config = CliTabConfig.ForNamespaces("Azure-Storage");
        Assert.True(config.IsNamespaceAllowed("azure-storage"));
        Assert.True(config.IsNamespaceAllowed("AZURE-STORAGE"));
    }

    [Fact]
    public void IsNamespaceAllowed_DisabledConfig_ReturnsFalse()
    {
        var config = new CliTabConfig();
        Assert.False(config.IsNamespaceAllowed("azure-storage"));
    }

    [Fact]
    public void LoadFromFile_MissingFile_ReturnsDisabledConfig()
    {
        var config = CliTabConfig.LoadFromFile("nonexistent-file-12345.json");
        Assert.False(config.IsEnabled);
        Assert.Empty(config.AllowedNamespaces);
    }

    [Fact]
    public void LoadFromFile_ValidJson_LoadsNamespaces()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cli-tab-config-test-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, """
            {
                "AllowedNamespaces": ["azure-storage", "azure-compute"]
            }
            """);

            var config = CliTabConfig.LoadFromFile(path);
            Assert.True(config.IsEnabled);
            Assert.Contains("azure-storage", config.AllowedNamespaces);
            Assert.Contains("azure-compute", config.AllowedNamespaces);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ForNamespaces_CreatesCorrectConfig()
    {
        var config = CliTabConfig.ForNamespaces("ns1", "ns2", "ns3");
        Assert.Equal(3, config.AllowedNamespaces.Count);
        Assert.True(config.IsEnabled);
        Assert.True(config.IsNamespaceAllowed("ns1"));
        Assert.True(config.IsNamespaceAllowed("ns2"));
        Assert.True(config.IsNamespaceAllowed("ns3"));
    }
}
