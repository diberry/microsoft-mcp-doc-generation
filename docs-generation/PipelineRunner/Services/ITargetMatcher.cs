namespace PipelineRunner.Services;

public interface ITargetMatcher
{
    string Normalize(string target);

    IReadOnlyList<CliTool> FindMatches(IReadOnlyList<CliTool> allTools, string target);
}
