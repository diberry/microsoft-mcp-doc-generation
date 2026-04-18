using FluentAssertions;
using SkillsGen.Core.Parsers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Parsers;

public class RbacExtractionTests
{
    [Fact]
    public void ExtractRbacRoles_TableFormat_ParsesAllRows()
    {
        var body = """
            ## Required Roles

            | Role | Scope | Reason |
            |------|-------|--------|
            | Cost Management Reader | Subscription | Read cost data |
            | Monitoring Reader | Resource group | Read monitoring metrics |
            """;

        var roles = SkillMarkdownParser.ExtractRbacRoles(body);

        roles.Should().HaveCount(2);
        roles[0].RoleName.Should().Be("Cost Management Reader");
        roles[0].Scope.Should().Be("Subscription");
        roles[0].Reason.Should().Be("Read cost data");
        roles[1].RoleName.Should().Be("Monitoring Reader");
        roles[1].Scope.Should().Be("Resource group");
    }

    [Fact]
    public void ExtractRbacRoles_RbacHeading_ParsesTable()
    {
        // Use \n-delimited string to ensure multiline regex works correctly
        var body = "### RBAC\n\n| Role | Scope |\n|------|-------|\n| Storage Blob Data Reader | Storage account |";

        var roles = SkillMarkdownParser.ExtractRbacRoles(body);

        roles.Should().NotBeEmpty("table under ### RBAC heading should be parsed");
        roles.Should().Contain(r => r.RoleName == "Storage Blob Data Reader");
        // Scope may come from table or default to Subscription
        var role = roles.First(r => r.RoleName == "Storage Blob Data Reader");
        role.Scope.Should().Be("Storage account");
    }

    [Fact]
    public void ExtractRbacRoles_BulletFormat_ParsesRoles()
    {
        var body = """
            ## Required Roles

            - **Key Vault Secrets Officer** — Manage secrets in Key Vault
            - **Key Vault Certificates Officer** — Manage certificates
            """;

        var roles = SkillMarkdownParser.ExtractRbacRoles(body);

        roles.Should().HaveCount(2);
        roles[0].RoleName.Should().Be("Key Vault Secrets Officer");
    }

    [Fact]
    public void ExtractRbacRoles_InlineMentions_ExtractsRoleNames()
    {
        var body = """
            This skill requires Cost Management Reader + Monitoring Reader
            to analyze Azure spending data. The user may also need
            Billing Account Reader for cross-subscription views.
            """;

        var roles = SkillMarkdownParser.ExtractRbacRoles(body);

        roles.Should().Contain(r => r.RoleName == "Cost Management Reader");
        roles.Should().Contain(r => r.RoleName == "Monitoring Reader");
        roles.Should().Contain(r => r.RoleName == "Billing Account Reader");
    }

    [Fact]
    public void ExtractRbacRoles_EmptyBody_ReturnsEmpty()
    {
        var roles = SkillMarkdownParser.ExtractRbacRoles("");
        roles.Should().BeEmpty();
    }

    [Fact]
    public void ExtractRbacRoles_NullBody_ReturnsEmpty()
    {
        var roles = SkillMarkdownParser.ExtractRbacRoles(null!);
        roles.Should().BeEmpty();
    }

    [Fact]
    public void ExtractRbacRoles_NoRbacSection_ReturnsEmpty()
    {
        var body = """
            ## Services

            | Service | When to use |
            |---------|------------|
            | Azure Monitor | View metrics |
            """;

        var roles = SkillMarkdownParser.ExtractRbacRoles(body);
        roles.Should().BeEmpty();
    }

    [Fact]
    public void ExtractRbacRoles_DuplicatesDeduped_ByCaseInsensitiveName()
    {
        var body = """
            ## Required Roles

            | Role | Scope |
            |------|-------|
            | Storage Blob Data Reader | Subscription |

            The user needs Storage Blob Data Reader to read blobs.
            """;

        var roles = SkillMarkdownParser.ExtractRbacRoles(body);

        // Table role found first; inline duplicate should be deduped
        roles.Should().ContainSingle();
        roles[0].RoleName.Should().Be("Storage Blob Data Reader");
    }

    [Fact]
    public void ExtractRbacRoles_TableWithSeparatorRow_SkipsIt()
    {
        var body = """
            ## Required Roles

            | Role | Scope | Reason |
            |------|-------|--------|
            | Network Contributor | Resource group | Manage network resources |
            """;

        var roles = SkillMarkdownParser.ExtractRbacRoles(body);

        roles.Should().ContainSingle();
        roles[0].RoleName.Should().NotStartWith("---");
    }
}
