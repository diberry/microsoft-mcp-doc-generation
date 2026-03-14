namespace PipelineRunner.Services;

public interface IWorkspaceManager
{
    string CreateTemporaryDirectory(string prefix);

    void Delete(string path);

    void DeleteAll();
}
