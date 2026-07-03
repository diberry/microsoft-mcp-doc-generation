using Xunit;
using Shared;

namespace Shared.Tests;

public class StepResultSchemaExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var ex = new StepResultSchemaException("Unknown schema version.");

        Assert.Equal("Unknown schema version.", ex.Message);
        Assert.Null(ex.ActualVersion);
    }

    [Fact]
    public void Constructor_WithMessageAndVersion_SetsBoth()
    {
        var ex = new StepResultSchemaException("Unrecognized schemaVersion '3.5'.", "3.5");

        Assert.Equal("Unrecognized schemaVersion '3.5'.", ex.Message);
        Assert.Equal("3.5", ex.ActualVersion);
    }

    [Fact]
    public void ActualVersion_IsNull_WhenOnlyMessageProvided()
    {
        var ex = new StepResultSchemaException("msg");
        Assert.Null(ex.ActualVersion);
    }

    [Fact]
    public void ActualVersion_IsNull_WhenExplicitlyPassedNull()
    {
        var ex = new StepResultSchemaException("msg", null);
        Assert.Null(ex.ActualVersion);
    }

    [Fact]
    public void IsExceptionSubtype()
    {
        var ex = new StepResultSchemaException("msg");
        Assert.IsAssignableFrom<Exception>(ex);
    }
}
