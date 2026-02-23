using Xunit;
using AzmcpCommandParser.Serialization;
using AzmcpCommandParser.Models;

namespace AzmcpCommandParser.Tests;

public class CommandDocumentSerializerTests
{
    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var doc = new CommandDocument
        {
            Title = "Test",
            GlobalOptions =
            [
                new GlobalOption { Name = "--subscription", IsRequired = false, Default = "-", Description = "Sub ID" }
            ],
            ServiceSections =
            [
                new ServiceSection
                {
                    Heading = "Azure Storage Operations",
                    AreaName = "storage",
                    Commands =
                    [
                        new Command
                        {
                            Description = "Get accounts",
                            CommandText = "azmcp storage account get",
                            Namespace = "storage",
                            SubCommands = ["account", "get"],
                            Metadata = new ToolMetadata { ReadOnly = true, Idempotent = true },
                            Parameters =
                            [
                                new CommandParameter { Name = "--subscription", IsRequired = true, ValuePlaceholder = "subscription" }
                            ]
                        }
                    ]
                }
            ]
        };

        var json = CommandDocumentSerializer.Serialize(doc);

        Assert.Contains("\"title\"", json);
        Assert.Contains("\"storage\"", json);
        Assert.Contains("\"globalOptions\"", json);
        Assert.Contains("\"readOnly\": true", json);
    }

    [Fact]
    public void Roundtrip_PreservesData()
    {
        var doc = new CommandDocument
        {
            Title = "Roundtrip Test",
            GlobalOptions =
            [
                new GlobalOption { Name = "--tenant-id", Description = "Tenant" }
            ],
            ServiceSections =
            [
                new ServiceSection
                {
                    Heading = "Azure Key Vault Operations",
                    AreaName = "keyvault",
                    Commands =
                    [
                        new Command
                        {
                            Description = "Get secret",
                            CommandText = "azmcp keyvault secret get",
                            Namespace = "keyvault",
                            SubCommands = ["secret", "get"],
                            Metadata = new ToolMetadata { Secret = true, ReadOnly = true },
                            Parameters =
                            [
                                new CommandParameter { Name = "--vault", IsRequired = true, ValuePlaceholder = "vault-name" },
                                new CommandParameter { Name = "--secret", IsRequired = false, ValuePlaceholder = "secret-name" }
                            ]
                        }
                    ]
                }
            ]
        };

        var json = CommandDocumentSerializer.Serialize(doc);
        var deserialized = CommandDocumentSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Roundtrip Test", deserialized.Title);
        Assert.Single(deserialized.GlobalOptions);
        Assert.Single(deserialized.ServiceSections);

        var cmd = deserialized.ServiceSections[0].Commands[0];
        Assert.Equal("keyvault", cmd.Namespace);
        Assert.True(cmd.Metadata!.Secret);
        Assert.Equal(2, cmd.Parameters.Count);
    }

    [Fact]
    public void Serialize_OmitsNullFields()
    {
        var doc = new CommandDocument
        {
            Title = "Null Test",
            ServiceSections =
            [
                new ServiceSection
                {
                    Heading = "Test",
                    Commands =
                    [
                        new Command
                        {
                            CommandText = "azmcp test run",
                            Namespace = "test",
                            // No metadata, no parameters with allowed values
                        }
                    ]
                }
            ]
        };

        var json = CommandDocumentSerializer.Serialize(doc);
        Assert.DoesNotContain("\"metadata\"", json);
        Assert.DoesNotContain("\"allowedValues\"", json);
    }

    [Fact]
    public void Roundtrip_PreservesParameterAlternativeGroups()
    {
        var doc = new CommandDocument
        {
            Title = "Group Test",
            ServiceSections =
            [
                new ServiceSection
                {
                    Heading = "Azure Data Explorer Operations",
                    AreaName = "kusto",
                    Commands =
                    [
                        new Command
                        {
                            Description = "List databases",
                            CommandText = "azmcp kusto database list",
                            Namespace = "kusto",
                            SubCommands = ["database", "list"],
                            Parameters = [new CommandParameter { Name = "--database", IsRequired = true, ValuePlaceholder = "database" }],
                            ParameterAlternativeGroups =
                            [
                                new ParameterAlternativeGroup
                                {
                                    Alternatives =
                                    [
                                        [new CommandParameter { Name = "--cluster-uri", ValuePlaceholder = "cluster-uri" }],
                                        [
                                            new CommandParameter { Name = "--subscription", ValuePlaceholder = "subscription" },
                                            new CommandParameter { Name = "--cluster", ValuePlaceholder = "cluster" }
                                        ]
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var json = CommandDocumentSerializer.Serialize(doc);
        var deserialized = CommandDocumentSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        var cmd = deserialized.ServiceSections[0].Commands[0];
        Assert.Single(cmd.ParameterAlternativeGroups);

        var group = cmd.ParameterAlternativeGroups[0];
        Assert.Equal(2, group.Alternatives.Count);
        Assert.Single(group.Alternatives[0]);
        Assert.Equal("--cluster-uri", group.Alternatives[0][0].Name);
        Assert.Equal(2, group.Alternatives[1].Count);
        Assert.Equal("--subscription", group.Alternatives[1][0].Name);
        Assert.Equal("--cluster", group.Alternatives[1][1].Name);
    }
}
