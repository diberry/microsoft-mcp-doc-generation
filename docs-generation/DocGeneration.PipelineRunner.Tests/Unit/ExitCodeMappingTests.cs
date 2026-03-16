using PipelineRunner.Contracts;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class ExitCodeMappingTests
{
    [Fact]
    public void MapBootstrapExitCode_PreservesHumanReviewCode()
        => Assert.Equal(2, global::PipelineRunner.PipelineRunner.MapBootstrapExitCode(2));

    [Fact]
    public void MapBootstrapExitCode_MapsUnexpectedFailuresToFatal()
        => Assert.Equal(1, global::PipelineRunner.PipelineRunner.MapBootstrapExitCode(7));

    [Fact]
    public void MapStepFailureExitCode_FatalFailure_ReturnsFatal()
        => Assert.Equal(1, global::PipelineRunner.PipelineRunner.MapStepFailureExitCode(FailurePolicy.Fatal, stepSucceeded: false));

    [Fact]
    public void MapStepFailureExitCode_WarnFailure_ReturnsSuccess()
        => Assert.Equal(0, global::PipelineRunner.PipelineRunner.MapStepFailureExitCode(FailurePolicy.Warn, stepSucceeded: false));

    [Fact]
    public void MapStepFailureExitCode_HumanReviewOverride_ReturnsHumanReview()
        => Assert.Equal(2, global::PipelineRunner.PipelineRunner.MapStepFailureExitCode(FailurePolicy.Fatal, stepSucceeded: false, exitCodeOverride: 2));
}
