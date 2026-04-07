// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DocGeneration.E2E.Tests.Fixtures;

/// <summary>
/// xUnit collection definition that ensures all E2E test classes
/// share a single pipeline run via PipelineOutputFixture.
/// </summary>
[CollectionDefinition(Name)]
public sealed class E2ETestCollection : ICollectionFixture<PipelineOutputFixture>
{
    public const string Name = "E2E Pipeline Tests";
}
