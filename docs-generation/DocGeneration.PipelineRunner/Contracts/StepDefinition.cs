using PipelineRunner.Context;

namespace PipelineRunner.Contracts;

public abstract class StepDefinition : IPipelineStep
{
    protected StepDefinition(
        int id,
        string name,
        StepScope scope,
        FailurePolicy failurePolicy,
        IReadOnlyList<int>? dependsOn = null,
        IReadOnlyList<IPostValidator>? postValidators = null,
        bool requiresCliOutput = true,
        bool requiresCliVersion = true,
        bool requiresAiConfiguration = false,
        bool createsFilteredCliView = false,
        bool usesIsolatedWorkspace = false,
        IReadOnlyList<string>? expectedOutputs = null,
        string implementation = "Typed",
        int maxRetries = 0)
    {
        Id = id;
        Name = name;
        Scope = scope;
        FailurePolicy = failurePolicy;
        DependsOn = dependsOn ?? Array.Empty<int>();
        PostValidators = postValidators ?? Array.Empty<IPostValidator>();
        RequiresCliOutput = requiresCliOutput;
        RequiresCliVersion = requiresCliVersion;
        RequiresAiConfiguration = requiresAiConfiguration;
        CreatesFilteredCliView = createsFilteredCliView;
        UsesIsolatedWorkspace = usesIsolatedWorkspace;
        ExpectedOutputs = expectedOutputs ?? Array.Empty<string>();
        Implementation = implementation;
        MaxRetries = maxRetries;
    }

    public int Id { get; }

    public string Name { get; }

    public StepScope Scope { get; }

    public FailurePolicy FailurePolicy { get; }

    public IReadOnlyList<int> DependsOn { get; }

    public IReadOnlyList<IPostValidator> PostValidators { get; }

    public bool RequiresCliOutput { get; }

    public bool RequiresCliVersion { get; }

    public bool RequiresAiConfiguration { get; }

    public bool CreatesFilteredCliView { get; }

    public bool UsesIsolatedWorkspace { get; }

    public IReadOnlyList<string> ExpectedOutputs { get; }

    public string Implementation { get; }

    public int MaxRetries { get; }

    public abstract ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken);
}
