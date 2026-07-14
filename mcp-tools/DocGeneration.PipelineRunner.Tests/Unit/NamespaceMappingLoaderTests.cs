using PipelineRunner.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public sealed class NamespaceMappingLoaderTests
{
    [Fact]
    public async Task LoadAsync_ValidFile_ReturnsAllMappings()
    {
        // Arrange
        var tempDir = CreateTempRepoWithNamespaceMapping();
        var loader = new NamespaceMappingLoader();

        try
        {
            // Act
            var mapping = await loader.LoadAsync(tempDir, CancellationToken.None);

            // Assert
            Assert.NotEmpty(mapping);
            Assert.Equal(3, mapping.Count);
            Assert.Equal("azure-storage.md", mapping["storage"]);
            Assert.Equal("azure-key-vault.md", mapping["keyvault"]);
            Assert.Equal("azure-cosmos-db.md", mapping["cosmos"]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_FileNotFound_ReturnsEmptyMapping()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var loader = new NamespaceMappingLoader();

        try
        {
            // Act
            var mapping = await loader.LoadAsync(tempDir, CancellationToken.None);

            // Assert
            Assert.Empty(mapping);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ReturnsEmptyMapping()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var configDir = Path.Combine(tempDir, "config");
        Directory.CreateDirectory(configDir);
        await File.WriteAllTextAsync(Path.Combine(configDir, "namespace-mapping.json"), "{ invalid json }");
        var loader = new NamespaceMappingLoader();

        try
        {
            // Act
            var mapping = await loader.LoadAsync(tempDir, CancellationToken.None);

            // Assert
            Assert.Empty(mapping);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_EmptyJsonObject_ReturnsEmptyMapping()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var configDir = Path.Combine(tempDir, "config");
        Directory.CreateDirectory(configDir);
        await File.WriteAllTextAsync(Path.Combine(configDir, "namespace-mapping.json"), "{}");
        var loader = new NamespaceMappingLoader();

        try
        {
            // Act
            var mapping = await loader.LoadAsync(tempDir, CancellationToken.None);

            // Assert
            Assert.Empty(mapping);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_CaseInsensitiveKeys_PreservesOriginalKeys()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var configDir = Path.Combine(tempDir, "config");
        Directory.CreateDirectory(configDir);
        var json = """
        {
          "Storage": "azure-storage.md",
          "KEYVAULT": "azure-key-vault.md"
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(configDir, "namespace-mapping.json"), json);
        var loader = new NamespaceMappingLoader();

        try
        {
            // Act
            var mapping = await loader.LoadAsync(tempDir, CancellationToken.None);

            // Assert - keys should be preserved as-is from JSON
            Assert.Equal(2, mapping.Count);
            Assert.True(mapping.ContainsKey("Storage"));
            Assert.True(mapping.ContainsKey("KEYVAULT"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_RespectsRealWorldStructure()
    {
        // Arrange - test against actual extracted structure
        var tempDir = CreateTempRepoWithRealWorldMapping();
        var loader = new NamespaceMappingLoader();

        try
        {
            // Act
            var mapping = await loader.LoadAsync(tempDir, CancellationToken.None);

            // Assert - spot-check key entries from actual PowerShell hashtable
            Assert.True(mapping.Count > 50, "Should have 57 entries from real PowerShell hashtable");
            Assert.Equal("app-configuration.md", mapping["appconfig"]);
            Assert.Equal("application-insights.md", mapping["applicationinsights"]);
            Assert.Equal("resource-group.md", mapping["group"]);
            Assert.Equal("azure-kubernetes.md", mapping["aks"]);
            Assert.Equal("azure-mcp-tool.md", mapping["extension"]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_RepositoryConfig_IncludesResilienceMapping()
    {
        // Arrange - config/namespace-mapping.json is the post-assembly validation source of truth.
        var repoRoot = FindRepositoryRoot();
        var loader = new NamespaceMappingLoader();

        // Act
        var mapping = await loader.LoadAsync(repoRoot, CancellationToken.None);

        // Assert
        Assert.Equal("azure-resilience.md", mapping["resilience"]);
    }

    private static string CreateTempRepoWithNamespaceMapping()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var configDir = Path.Combine(tempDir, "config");
        Directory.CreateDirectory(configDir);

        var json = """
        {
          "storage": "azure-storage.md",
          "keyvault": "azure-key-vault.md",
          "cosmos": "azure-cosmos-db.md"
        }
        """;

        File.WriteAllText(Path.Combine(configDir, "namespace-mapping.json"), json);
        return tempDir;
    }

    private static string CreateTempRepoWithRealWorldMapping()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var configDir = Path.Combine(tempDir, "config");
        Directory.CreateDirectory(configDir);

        // Subset of real namespace-mapping.json for testing
        var json = """
        {
          "appconfig": "app-configuration.md",
          "applicationinsights": "application-insights.md",
          "group": "resource-group.md",
          "speech": "ai-services-speech.md",
          "subscription": "subscription.md",
          "kusto": "azure-data-explorer.md",
          "acr": "azure-container-registry.md",
          "advisor": "azure-advisor.md",
          "aks": "azure-kubernetes.md",
          "extension": "azure-mcp-tool.md",
          "storage": "azure-storage.md",
          "keyvault": "azure-key-vault.md",
          "cosmos": "azure-cosmos-db.md",
          "monitor": "azure-monitor.md",
          "functions": "azure-functions.md",
          "compute": "azure-compute.md",
          "sql": "azure-sql.md",
          "postgres": "azure-database-postgresql.md",
          "mysql": "azure-mysql.md",
          "redis": "azure-redis.md",
          "search": "azure-ai-search.md",
          "servicebus": "azure-service-bus.md",
          "eventgrid": "azure-event-grid.md",
          "eventhubs": "azure-event-hubs.md",
          "containerapps": "azure-container-apps.md",
          "policy": "azure-policy.md",
          "role": "azure-rbac.md",
          "pricing": "azure-pricing.md",
          "quota": "azure-quotas.md",
          "deploy": "azure-deploy.md",
          "get": "azure-best-practices.md",
          "applens": "azure-app-lens.md",
          "appservice": "azure-app-service.md",
          "azurebackup": "azure-backup.md",
          "azuremigrate": "azure-migrate.md",
          "azureterraform": "azure-terraform.md",
          "azureterraformbestpractices": "azure-terraform-best-practices.md",
          "bicepschema": "azure-bicep-schema.md",
          "cloudarchitect": "azure-cloud-architect.md",
          "communication": "azure-communication.md",
          "confidentialledger": "azure-confidential-ledger.md",
          "datadog": "azure-native-isv.md",
          "deviceregistry": "azure-device-registry.md",
          "fileshares": "azure-file-shares.md",
          "foundryextensions": "azure-foundry.md",
          "functionapp": "azure-functions.md",
          "grafana": "azure-grafana.md",
          "loadtesting": "azure-load-testing.md",
          "managedlustre": "azure-managed-lustre.md",
          "marketplace": "azure-marketplace.md",
          "resourcehealth": "azure-resource-health.md",
          "servicefabric": "azure-service-fabric.md",
          "signalr": "azure-signalr.md",
          "storagesync": "azure-file-sync.md",
          "virtualdesktop": "azure-virtual-desktop.md",
          "wellarchitectedframework": "azure-well-architected-framework.md",
          "workbooks": "azure-workbooks.md"
        }
        """;

        File.WriteAllText(Path.Combine(configDir, "namespace-mapping.json"), json);
        return tempDir;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var configPath = Path.Combine(current.FullName, "config", "namespace-mapping.json");
            if (File.Exists(configPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing config/namespace-mapping.json.");
    }
}
