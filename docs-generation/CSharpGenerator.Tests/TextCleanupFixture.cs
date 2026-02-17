// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using NaturalLanguageGenerator;

namespace CSharpGenerator.Tests;

/// <summary>
/// Shared fixture that initializes TextCleanup once for all test classes that need it.
/// Used via IClassFixture&lt;TextCleanupFixture&gt; on ParameterSortingTests and DocumentationGeneratorTests.
/// </summary>
public class TextCleanupFixture
{
    public TextCleanupFixture()
    {
        var nlPath = TestHelpers.TestDataPath("nl-parameters.json");
        var staticPath = TestHelpers.TestDataPath("static-text-replacement.json");
        TextCleanup.LoadFiles(new List<string> { nlPath, staticPath });
    }
}
