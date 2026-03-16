// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using CSharpGenerator.Models;
using GenerativeAI;
using HorizontalArticleGenerator.Models;
using NUnit.Framework;
using ArticleGenerator = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator;

namespace HorizontalArticleGenerator.Tests;

[TestFixture]
public class HorizontalArticleGeneratorTests
{
    private string _outputBasePath = null!;

    [SetUp]
    public void SetUp()
    {
        _outputBasePath = Path.Combine(Path.GetTempPath(), "horizontal-article-generator-tests", Guid.NewGuid().ToString("N"));
        var cliDirectory = Path.Combine(_outputBasePath, "cli");
        Directory.CreateDirectory(cliDirectory);
        File.WriteAllText(Path.Combine(cliDirectory, "cli-version.json"), "{\"version\":\"test-version\"}");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_outputBasePath))
        {
            Directory.Delete(_outputBasePath, recursive: true);
        }
    }

    [Test]
    public async Task ExtractStaticData_UsesToolFamilyReferenceLink()
    {
        await WriteCliOutputAsync(new CliOutput
        {
            Results =
            [
                new Tool
                {
                    Name = "list",
                    Command = "compute vm list",
                    Description = "List virtual machines.",
                    Area = "compute",
                    Option = []
                }
            ]
        });

        var generator = CreateGenerator();

        var staticData = await InvokeExtractStaticDataAsync(generator);

        Assert.That(staticData, Has.Count.EqualTo(1));
        Assert.That(staticData[0].ToolsReferenceLink, Is.EqualTo("../tool-family/compute.md"));
    }

    private ArticleGenerator CreateGenerator()
    {
        return new ArticleGenerator(
            new GenerativeAIOptions
            {
                ApiKey = "test-key",
                Endpoint = "https://example.test",
                Deployment = "test-deployment",
                ApiVersion = "2024-01-01"
            },
            outputBasePath: _outputBasePath);
    }

    private async Task WriteCliOutputAsync(CliOutput cliOutput)
    {
        var cliOutputPath = Path.Combine(_outputBasePath, "cli", "cli-output.json");
        var json = JsonSerializer.Serialize(cliOutput);
        await File.WriteAllTextAsync(cliOutputPath, json);
    }

    private static async Task<List<StaticArticleData>> InvokeExtractStaticDataAsync(ArticleGenerator generator)
    {
        var method = typeof(ArticleGenerator).GetMethod("ExtractStaticData", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        var task = method!.Invoke(generator, null) as Task<List<StaticArticleData>>;
        Assert.That(task, Is.Not.Null);

        return await task!;
    }
}
