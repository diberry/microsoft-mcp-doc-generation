// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using PipelineRunner.Cli;
using DocGeneration.TestInfrastructure;

namespace DocGeneration.E2E.Tests.Fixtures;

/// <summary>
/// Shared fixture that runs the pipeline once for the "advisor" namespace (Step 1 only).
/// All E2E test classes in the same collection share this single pipeline run.
/// Requires RUN_E2E_TESTS=true environment variable to actually execute the pipeline.
/// </summary>
public sealed class PipelineOutputFixture : IAsyncLifetime
{
    public const string TestNamespace = "advisor";

    public string OutputPath { get; private set; } = string.Empty;
    public int ExitCode { get; private set; } = -1;
    public bool PipelineRan { get; private set; }
    public string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        if (!IsE2EEnabled())
        {
            SkipReason = "E2E tests are opt-in. Set RUN_E2E_TESTS=true to run.";
            return;
        }

        var repoRoot = ProjectRootFinder.FindSolutionRoot();
        OutputPath = Path.Combine(repoRoot, $"generated-e2e-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(OutputPath);

        var request = new PipelineRequest(
            Namespace: TestNamespace,
            Steps: [1],
            OutputPath: OutputPath,
            SkipBuild: false,
            SkipValidation: false,
            DryRun: false,
            SkipEnvValidation: true,
            SkipDependencyValidation: true);

        var runner = PipelineRunner.PipelineRunner.CreateDefault(repoRoot);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        ExitCode = await runner.RunAsync(request, cts.Token);
        PipelineRan = true;
    }

    public Task DisposeAsync()
    {
        if (PipelineRan && Directory.Exists(OutputPath))
        {
            try { Directory.Delete(OutputPath, recursive: true); }
            catch { /* best effort cleanup */ }
        }
        return Task.CompletedTask;
    }

    private static bool IsE2EEnabled()
    {
        var value = Environment.GetEnvironmentVariable("RUN_E2E_TESTS");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }
}
