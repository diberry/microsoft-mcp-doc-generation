using PipelineRunner.Cli;

return await PipelineCli.InvokeAsync(
    args,
    async (request, cancellationToken) =>
    {
        var runner = global::PipelineRunner.PipelineRunner.CreateDefault();
        return await runner.RunAsync(request, cancellationToken);
    });
