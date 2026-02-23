using Xunit;
using AzmcpCommandParser.Parsing;
using AzmcpCommandParser.Models;

namespace AzmcpCommandParser.Tests;

public class MarkdownCommandParserTests
{
    private readonly MarkdownCommandParser _parser = new();

    // ── H1 / Title ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsTitle()
    {
        var md = "# Azure MCP CLI Command Reference\n\nSome intro text.\n\n## Global Options\n\n| Option | Required | Default | Description |\n|--------|----------|---------|-------------|\n| `--subscription` | No | - | Azure subscription ID |";
        var doc = _parser.Parse(md);
        Assert.Equal("Azure MCP CLI Command Reference", doc.Title);
    }

    [Fact]
    public void Parse_ExtractsIntroduction()
    {
        var md = "# Title\n\n> [!IMPORTANT]\n> Some important note.\n\n## Global Options\n\n| Option | Required | Default | Description |\n|--------|----------|---------|-------------|\n| `--sub` | No | - | desc |";
        var doc = _parser.Parse(md);
        Assert.Contains("IMPORTANT", doc.Introduction);
    }

    // ── Global Options ───────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsGlobalOptions()
    {
        var md = """
            # Title

            ## Global Options

            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--subscription` | No | Environment variable | Azure subscription ID |
            | `--tenant-id` | No | - | Azure tenant ID |
            | `--retry-max-retries` | No | 3 | Max retry attempts |

            ## Available Commands
            """;
        var doc = _parser.Parse(md);
        Assert.Equal(3, doc.GlobalOptions.Count);
        Assert.Equal("--subscription", doc.GlobalOptions[0].Name);
        Assert.False(doc.GlobalOptions[0].IsRequired);
        Assert.Equal("--tenant-id", doc.GlobalOptions[1].Name);
    }

    // ── Service Sections ─────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsServiceSections()
    {
        var md = """
            # Title

            ## Global Options

            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |

            ## Available Commands

            ### Azure Storage Operations

            ```bash
            # List storage accounts
            # ❌ Destructive | ✅ Idempotent | ❌ OpenWorld | ✅ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp storage account get --subscription <subscription>
            ```

            ### Azure Key Vault Operations

            ```bash
            # Get secrets
            # ❌ Destructive | ✅ Idempotent | ❌ OpenWorld | ✅ ReadOnly | ✅ Secret | ❌ LocalRequired
            azmcp keyvault secret get --subscription <subscription> --vault <vault>
            ```
            """;

        var doc = _parser.Parse(md);
        Assert.Equal(2, doc.ServiceSections.Count);
        Assert.Equal("Azure Storage Operations", doc.ServiceSections[0].Heading);
        Assert.Equal("storage", doc.ServiceSections[0].AreaName);
        Assert.Equal("Azure Key Vault Operations", doc.ServiceSections[1].Heading);
        Assert.Equal("keyvault", doc.ServiceSections[1].AreaName);
    }

    // ── Command Parsing ──────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsCommandWithMetadata()
    {
        var md = """
            # Title
            ## Global Options
            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |
            ## Available Commands
            ### Azure Cosmos DB Operations
            ```bash
            # List Cosmos DB accounts in a subscription
            # ❌ Destructive | ✅ Idempotent | ❌ OpenWorld | ✅ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp cosmos account list --subscription <subscription>
            ```
            """;

        var doc = _parser.Parse(md);
        var cmd = doc.ServiceSections[0].Commands[0];

        Assert.Equal("List Cosmos DB accounts in a subscription", cmd.Description);
        Assert.Equal("cosmos", cmd.Namespace);
        Assert.Equal(["account", "list"], cmd.SubCommands);
        Assert.NotNull(cmd.Metadata);
        Assert.False(cmd.Metadata.Destructive);
        Assert.True(cmd.Metadata.Idempotent);
        Assert.True(cmd.Metadata.ReadOnly);
        Assert.False(cmd.Metadata.Secret);
    }

    [Fact]
    public void Parse_ExtractsCommandWithoutMetadata()
    {
        var md = """
            # Title
            ## Global Options
            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |
            ## Available Commands
            ### Azure Advisor Operations
            ```bash
            # List Advisor recommendations in a subscription
            azmcp advisor recommendations list --subscription <subscription>
            ```
            """;

        var doc = _parser.Parse(md);
        var cmd = doc.ServiceSections[0].Commands[0];

        Assert.Equal("List Advisor recommendations in a subscription", cmd.Description);
        Assert.Null(cmd.Metadata);
        Assert.Equal("advisor", cmd.Namespace);
    }

    // ── Multi-line commands (continuation) ───────────────────────────────

    [Fact]
    public void Parse_HandlesMultiLineCommand()
    {
        var md = """
            # Title
            ## Global Options
            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |
            ## Available Commands
            ### Azure Storage Operations
            ```bash
            # Get blob properties
            # ❌ Destructive | ✅ Idempotent | ❌ OpenWorld | ✅ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp storage blob get --subscription <subscription> \
                                   --account <account> \
                                   --container <container> \
                                   [--blob <blob>]
            ```
            """;

        var doc = _parser.Parse(md);
        var cmd = doc.ServiceSections[0].Commands[0];

        Assert.Equal("storage", cmd.Namespace);
        Assert.Contains(cmd.SubCommands, s => s == "blob");
        Assert.Contains(cmd.SubCommands, s => s == "get");
        Assert.True(cmd.Parameters.Count >= 3);

        var subParam = cmd.Parameters.First(p => p.Name == "--subscription");
        Assert.True(subParam.IsRequired);

        var blobParam = cmd.Parameters.First(p => p.Name == "--blob");
        Assert.False(blobParam.IsRequired);
    }

    // ── Parameters ───────────────────────────────────────────────────────

    [Fact]
    public void ParseParameters_ExtractsRequiredAndOptional()
    {
        var line = "azmcp sql db create --subscription <subscription> --server <server> [--sku-name <sku-name>] [--zone-redundant <true/false>]";
        var parameters = _parser.ParseParameters(line);

        Assert.Equal(4, parameters.Count);

        var sub = parameters.First(p => p.Name == "--subscription");
        Assert.True(sub.IsRequired);
        Assert.Equal("subscription", sub.ValuePlaceholder);

        var sku = parameters.First(p => p.Name == "--sku-name");
        Assert.False(sku.IsRequired);

        var zr = parameters.First(p => p.Name == "--zone-redundant");
        Assert.False(zr.IsRequired);
    }

    [Fact]
    public void ParseParameters_ExtractsFlagParameters()
    {
        var line = "azmcp server start --mode namespace --read-only";
        var parameters = _parser.ParseParameters(line);

        var readOnly = parameters.FirstOrDefault(p => p.Name == "--read-only");
        Assert.NotNull(readOnly);
        Assert.True(readOnly.IsFlag);
    }

    [Fact]
    public void ParseParameters_ExtractsAllowedValues()
    {
        var line = "azmcp storage blob get [--format <simple|detailed>]";
        var parameters = _parser.ParseParameters(line);

        var format = parameters.First(p => p.Name == "--format");
        Assert.NotNull(format.AllowedValues);
        Assert.Equal(2, format.AllowedValues.Count);
        Assert.Contains("simple", format.AllowedValues);
        Assert.Contains("detailed", format.AllowedValues);
    }

    // ── Parameter Alternative Groups ─────────────────────────────────────

    [Fact]
    public void ParseParametersAndGroups_ExtractsOneVsTwoParamAlternative()
    {
        // [--cluster-uri <cluster-uri> | --subscription <subscription> --cluster <cluster>]
        var line = "azmcp kusto database list [--cluster-uri <cluster-uri> | --subscription <subscription> --cluster <cluster>]";
        var (parameters, groups) = _parser.ParseParametersAndGroups(line);

        // Grouped params should NOT appear in the flat list
        Assert.Empty(parameters);

        // One group with two alternatives
        Assert.Single(groups);
        var group = groups[0];
        Assert.Equal(2, group.Alternatives.Count);

        // Alternative 1: --cluster-uri alone
        Assert.Single(group.Alternatives[0]);
        Assert.Equal("--cluster-uri", group.Alternatives[0][0].Name);
        Assert.Equal("cluster-uri", group.Alternatives[0][0].ValuePlaceholder);

        // Alternative 2: --subscription + --cluster together
        Assert.Equal(2, group.Alternatives[1].Count);
        Assert.Equal("--subscription", group.Alternatives[1][0].Name);
        Assert.Equal("--cluster", group.Alternatives[1][1].Name);
    }

    [Fact]
    public void ParseParametersAndGroups_ExtractsOneVsOneParamAlternative()
    {
        var line = "azmcp avd session list --subscription <subscription> --resource-group <rg> [--hostpool <hostpool-name> | --hostpool-resource-id <hostpool-resource-id>]";
        var (parameters, groups) = _parser.ParseParametersAndGroups(line);

        // Flat params: --subscription, --resource-group
        Assert.Equal(2, parameters.Count);
        Assert.Contains(parameters, p => p.Name == "--subscription");
        Assert.Contains(parameters, p => p.Name == "--resource-group");

        // One group with two single-param alternatives
        Assert.Single(groups);
        var group = groups[0];
        Assert.Equal(2, group.Alternatives.Count);
        Assert.Single(group.Alternatives[0]);
        Assert.Equal("--hostpool", group.Alternatives[0][0].Name);
        Assert.Single(group.Alternatives[1]);
        Assert.Equal("--hostpool-resource-id", group.Alternatives[1][0].Name);
    }

    [Fact]
    public void ParseParametersAndGroups_MixedGroupAndFlatParams()
    {
        var line = "azmcp kusto query [--cluster-uri <cluster-uri> | --subscription <subscription> --cluster <cluster>] --database <database> --query <kql-query>";
        var (parameters, groups) = _parser.ParseParametersAndGroups(line);

        // Flat params: --database, --query
        Assert.Equal(2, parameters.Count);
        Assert.Contains(parameters, p => p.Name == "--database");
        Assert.Contains(parameters, p => p.Name == "--query");

        // One group
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Alternatives.Count);
    }

    [Fact]
    public void Parse_CommandHasParameterAlternativeGroups()
    {
        var md = """
            # Title
            ## Global Options
            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |
            ## Available Commands
            ### Azure Data Explorer Operations
            ```bash
            # List databases in a cluster
            # ❌ Destructive | ✅ Idempotent | ❌ OpenWorld | ✅ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp kusto database list [--cluster-uri <cluster-uri> | --subscription <subscription> --cluster <cluster>]
            ```
            """;

        var doc = _parser.Parse(md);
        var cmd = doc.ServiceSections[0].Commands[0];

        Assert.Empty(cmd.Parameters); // All in groups
        Assert.Single(cmd.ParameterAlternativeGroups);

        var group = cmd.ParameterAlternativeGroups[0];
        Assert.Equal(2, group.Alternatives.Count);
        Assert.Equal("--cluster-uri", group.Alternatives[0][0].Name);
        Assert.Equal(2, group.Alternatives[1].Count);
    }

    [Fact]
    public void ParseParametersAndGroups_SimpleOptionalBracketIsNotGroup()
    {
        // Single optional params [--foo <bar>] should NOT become groups
        var line = "azmcp storage blob put --account <account> [--container <container>]";
        var (parameters, groups) = _parser.ParseParametersAndGroups(line);

        Assert.Empty(groups);
        Assert.Equal(2, parameters.Count);
    }

    // ── Sub-sections ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsSubSections()
    {
        var md = """
            # Title
            ## Global Options
            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |
            ## Available Commands
            ### Azure SQL Operations
            #### Database
            ```bash
            # List databases
            # ❌ Destructive | ✅ Idempotent | ❌ OpenWorld | ✅ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp sql db list --subscription <subscription> --resource-group <resource-group> --server <server>
            ```
            #### Server
            ```bash
            # List servers
            # ❌ Destructive | ✅ Idempotent | ❌ OpenWorld | ✅ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp sql server list --subscription <subscription> --resource-group <resource-group>
            ```
            """;

        var doc = _parser.Parse(md);
        var sql = doc.ServiceSections[0];
        Assert.Equal(2, sql.SubSections.Count);
        Assert.Equal("Database", sql.SubSections[0].Heading);
        Assert.Equal("Server", sql.SubSections[1].Heading);
        Assert.Single(sql.SubSections[0].Commands);
        Assert.Single(sql.SubSections[1].Commands);
    }

    // ── Example vs Definition ────────────────────────────────────────────

    [Fact]
    public void Parse_DistinguishesExamplesFromDefinitions()
    {
        var md = """
            # Title
            ## Global Options
            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |
            ## Available Commands
            ### Azure App Service Operations
            ```bash
            # Add a database connection
            # ❌ Destructive | ❌ Idempotent | ✅ OpenWorld | ❌ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp appservice database add --subscription <subscription> \
                                          --resource-group <resource-group> \
                                          --app <app>

            # Add a SQL Server database connection
            # ❌ Destructive | ❌ Idempotent | ✅ OpenWorld | ❌ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp appservice database add --subscription "my-subscription" \
                                          --resource-group "my-rg" \
                                          --app "my-webapp"
            ```
            """;

        var doc = _parser.Parse(md);
        var commands = doc.ServiceSections[0].Commands;
        Assert.Equal(2, commands.Count);

        var definition = commands[0];
        Assert.False(definition.IsExample);

        var example = commands[1];
        Assert.True(example.IsExample);
    }

    // ── Multiple commands in single code block ───────────────────────────

    [Fact]
    public void Parse_HandlesMultipleCommandsInOneCodeBlock()
    {
        var md = """
            # Title
            ## Global Options
            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |
            ## Available Commands
            ### Azure Cosmos DB Operations
            ```bash
            # List accounts
            # ❌ Destructive | ✅ Idempotent | ❌ OpenWorld | ✅ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp cosmos account list --subscription <subscription>

            # Query items
            # ❌ Destructive | ✅ Idempotent | ❌ OpenWorld | ✅ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp cosmos database container item query --subscription <subscription> \
                                                       --account <account> \
                                                       --database <database> \
                                                       --container <container>
            ```
            """;

        var doc = _parser.Parse(md);
        Assert.Equal(2, doc.ServiceSections[0].Commands.Count);
    }

    // ── DeriveAreaName ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Azure Storage Operations", "storage")]
    [InlineData("Azure Key Vault Operations", "keyvault")]
    [InlineData("Azure Container Registry (ACR) Operations", "acr")]
    [InlineData("Azure Cosmos DB Operations", "cosmos")]
    [InlineData("Azure Data Explorer Operations", "kusto")]
    [InlineData("Azure Database for MySQL Operations", "mysql")]
    [InlineData("Azure Database for PostgreSQL Operations", "postgres")]
    [InlineData("Bicep", "bicepschema")]
    [InlineData("Cloud Architect", "cloudarchitect")]
    [InlineData("Microsoft Foundry Operations", "foundry")]
    [InlineData("Azure Kubernetes Service (AKS) Operations", "aks")]
    public void DeriveAreaName_MapsCorrectly(string heading, string expected)
    {
        Assert.Equal(expected, MarkdownCommandParser.DeriveAreaName(heading));
    }

    // ── Parameter tables at section level ─────────────────────────────────

    [Fact]
    public void Parse_ExtractsParameterTables()
    {
        var md = """
            # Title
            ## Global Options
            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |
            ## Available Commands
            ### Azure Compute Operations
            #### Virtual Machines
            ```bash
            # Get VMs
            # ❌ Destructive | ✅ Idempotent | ❌ OpenWorld | ✅ ReadOnly | ❌ Secret | ❌ LocalRequired
            azmcp compute vm get --subscription <subscription>
            ```
            | Parameter | Required | Description |
            |-----------|----------|-------------|
            | `--subscription` | Yes | Azure subscription ID |
            | `--resource-group` | No | Resource group name |
            """;

        var doc = _parser.Parse(md);
        var sub = doc.ServiceSections[0].SubSections[0];
        Assert.Single(sub.ParameterTables);
        Assert.Equal(2, sub.ParameterTables[0].Entries.Count);
        Assert.True(sub.ParameterTables[0].Entries[0].IsRequired);
        Assert.False(sub.ParameterTables[0].Entries[1].IsRequired);
    }

    // ── Metadata flags (all true) ────────────────────────────────────────

    [Fact]
    public void Parse_MetadataAllTrue()
    {
        var md = """
            # Title
            ## Global Options
            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |
            ## Available Commands
            ### Azure Test Operations
            ```bash
            # Test command
            # ✅ Destructive | ✅ Idempotent | ✅ OpenWorld | ✅ ReadOnly | ✅ Secret | ✅ LocalRequired
            azmcp test run --subscription <subscription>
            ```
            """;

        var doc = _parser.Parse(md);
        var meta = doc.ServiceSections[0].Commands[0].Metadata!;
        Assert.True(meta.Destructive);
        Assert.True(meta.Idempotent);
        Assert.True(meta.OpenWorld);
        Assert.True(meta.ReadOnly);
        Assert.True(meta.Secret);
        Assert.True(meta.LocalRequired);
    }

    // ── Response Format ──────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsResponseFormat()
    {
        var md = """
            # Title
            ## Global Options
            | Option | Required | Default | Description |
            |--------|----------|---------|-------------|
            | `--sub` | No | - | desc |
            ## Response Format
            All responses follow a consistent JSON format:
            ```json
            { "status": "200" }
            ```
            ## Error Handling
            The CLI returns errors.
            """;

        var doc = _parser.Parse(md);
        Assert.NotNull(doc.ResponseFormat);
        Assert.Contains("status", doc.ResponseFormat.JsonSchema);
        Assert.Contains("errors", doc.ErrorHandling);
    }
}
