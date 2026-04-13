// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Defines a test collection for tests that mutate shared static state
/// (Config.Load, TransformationEngine). Tests in this collection run
/// sequentially to avoid race conditions from parallel xUnit execution.
/// </summary>
[CollectionDefinition("StaticState")]
public class StaticStateCollection : ICollectionFixture<TransformationEngineFixture>
{
    // No body needed — the attribute + ICollectionFixture wire up the fixture.
}
