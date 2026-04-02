using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.Logging;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Logging;

public class SkillsLoggerTests
{
    private readonly ILogger<SkillsLogger> _innerLogger = Substitute.For<ILogger<SkillsLogger>>();

    [Fact]
    public void LogParseResult_DoesNotThrow()
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "test-logs-parse");
        var logger = new SkillsLogger(_innerLogger, logDir);

        var act = () => logger.LogParseResult("azure-test", 3, 5, 8);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogBatchSummary_DoesNotThrow()
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "test-logs-batch");
        var logger = new SkillsLogger(_innerLogger, logDir);

        var act = () => logger.LogBatchSummary(10, 8, 2, 5000);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogValidation_DoesNotThrow()
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "test-logs-validate");
        var logger = new SkillsLogger(_innerLogger, logDir);

        var act = () => logger.LogValidation("azure-test", true, 0, 1);

        act.Should().NotThrow();
    }
}
