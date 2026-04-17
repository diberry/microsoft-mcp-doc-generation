// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Mcp.TextTransformation;
using Azure.Mcp.TextTransformation.Services;

namespace CSharpGenerator.Tests;

/// <summary>
/// Shared fixture that initializes TransformationEngine once for all test classes that need it.
/// Replaces TextCleanupFixture for Phase 2 migration.
/// Also initializes Config.TextTransformationEngine so callers (e.g. ParameterSorting)
/// that read Config.TextNormalizer work correctly in test context.
/// </summary>
public class TransformationEngineFixture
{
    public TransformationEngine Engine { get; }
    public TextNormalizer Normalizer { get; }

    public TransformationEngineFixture()
    {
        var nlPath = TestHelpers.TestDataPath("nl-parameters.json");
        var staticPath = TestHelpers.TestDataPath("static-text-replacement.json");
        var config = TransformationConfigFactory.CreateFromLegacyFiles(
            new List<string> { nlPath, staticPath });
        Engine = new TransformationEngine(config);
        Normalizer = Engine.TextNormalizer;

        // Also wire up Config.TextTransformationEngine so production code
        // that reads Config.TextNormalizer works in tests.
        Config.Load(TestHelpers.TestDataPath("config.json"));
    }
}
