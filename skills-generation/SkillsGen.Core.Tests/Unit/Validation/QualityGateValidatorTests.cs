using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SkillsGen.Core.Validation;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Validation;

/// <summary>
/// Tests for GitHub Issue #12 (R7): Quality Gate.
/// Verifies that ValidateWhenToUse correctly flags thin or empty useFor lists
/// and passes adequate ones.
/// </summary>
public class QualityGateValidatorTests
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger =
        NullLoggerFactory.Instance.CreateLogger("QualityGate");

    [Fact]
    public void ValidateWhenToUse_ThinList_FlaggedAsInvalid()
    {
        var items = new List<string>
        {
            "Use it for storage operations"
        };

        var result = QualityGateValidator.ValidateWhenToUse(items, "azure-storage", _logger);

        result.IsValid.Should().BeFalse("a list with fewer than 3 items is considered thin");
        result.Warning.Should().NotBeNullOrEmpty("a warning message should explain why it failed");
    }

    [Fact]
    public void ValidateWhenToUse_AdequateList_PassesValidation()
    {
        var items = new List<string>
        {
            "Managing Azure Key Vault secrets and certificates",
            "Rotating expiring certificates before they expire",
            "Setting access policies for service principals",
            "Auditing secret access logs for compliance"
        };

        var result = QualityGateValidator.ValidateWhenToUse(items, "azure-keyvault", _logger);

        result.IsValid.Should().BeTrue("a list with 3 or more items passes the quality gate");
    }

    [Fact]
    public void ValidateWhenToUse_EmptyList_FlaggedAsInvalid()
    {
        var items = new List<string>();

        var result = QualityGateValidator.ValidateWhenToUse(items, "azure-monitor", _logger);

        result.IsValid.Should().BeFalse("an empty useFor list should fail the quality gate");
        result.Warning.Should().NotBeNullOrEmpty("a warning message should explain why it failed");
        result.ItemCount.Should().Be(0);
    }

    [Fact]
    public void ValidateWhenToUse_ExactlyThreeItems_PassesValidation()
    {
        var items = new List<string>
        {
            "Checking Azure quota usage before provisioning resources",
            "Verifying remaining vCPU capacity by region",
            "Identifying subscriptions nearing their quota limits"
        };

        var result = QualityGateValidator.ValidateWhenToUse(items, "azure-quotas", _logger);

        result.IsValid.Should().BeTrue("exactly 3 items is the minimum acceptable count");
        result.ItemCount.Should().Be(3);
    }
}
