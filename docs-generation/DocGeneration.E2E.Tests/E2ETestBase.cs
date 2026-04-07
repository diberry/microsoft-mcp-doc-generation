// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.E2E.Tests.Fixtures;

namespace DocGeneration.E2E.Tests;

/// <summary>
/// Base class for E2E tests that depend on the shared pipeline output fixture.
/// Provides guard logic and common assertions.
/// </summary>
public abstract class E2ETestBase
{
    protected readonly PipelineOutputFixture Fixture;

    protected E2ETestBase(PipelineOutputFixture fixture)
    {
        Fixture = fixture;
    }

    /// <summary>
    /// Ensures the pipeline ran successfully. Returns false if E2E tests are disabled
    /// (caller should return early). Asserts pipeline success when enabled.
    /// </summary>
    protected bool EnsurePipelineRan()
    {
        if (!Fixture.PipelineRan)
            return false;

        Assert.Equal(0, Fixture.ExitCode);
        return true;
    }
}
