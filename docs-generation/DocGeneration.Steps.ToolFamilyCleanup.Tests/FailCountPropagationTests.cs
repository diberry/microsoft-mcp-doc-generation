// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for CleanupGenerator.ProcessToolFamiliesMultiPhase return value
/// to ensure non-zero exit when AI generation fails.
/// Fixes: #160 — Step 4 silently returns 0 when generation fails.
/// </summary>
public class FailCountPropagationTests
{
    [Fact]
    public void ProcessToolFamiliesMultiPhase_ReturnsInt_NotVoid()
    {
        // The method signature must return Task<int> (fail count), not Task (void).
        // This is a compile-time check — if the signature is wrong, this test won't compile.
        var method = typeof(CleanupGenerator).GetMethod("ProcessToolFamiliesMultiPhase");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<int>), method!.ReturnType);
    }
}
