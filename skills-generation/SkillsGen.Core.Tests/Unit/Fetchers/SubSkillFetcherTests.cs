using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.Fetchers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Fetchers;

public class SubSkillFetcherTests
{
    [Fact]
    public async Task FetchSubSkillsAsync_WithSubdirectories_ReturnsSubSkills()
    {
        // Arrange: mock HTTP responses for directory listing and sub-skill files
        var handler = new MockHttpMessageHandler();

        // Directory listing response
        var dirListing = new[]
        {
            new { name = "foundry-agents", type = "dir" },
            new { name = "foundry-models", type = "dir" },
            new { name = "SKILL.md", type = "file" },
            new { name = ".hidden", type = "dir" }
        };
        handler.AddResponse(
            "https://api.github.com/repos/microsoft/azure-skills/contents/skills/microsoft-foundry?ref=main",
            JsonSerializer.Serialize(dirListing),
            "application/json");

        // Sub-skill SKILL.md responses
        handler.AddResponse(
            "https://api.github.com/repos/microsoft/azure-skills/contents/skills/microsoft-foundry/foundry-agents/SKILL.md?ref=main",
            "---\nname: foundry-agents\ndescription: Manage Foundry agents\n---\n\n# Foundry Agents\n\nCreate and manage AI agents.",
            "text/plain");

        handler.AddResponse(
            "https://api.github.com/repos/microsoft/azure-skills/contents/skills/microsoft-foundry/foundry-models/SKILL.md?ref=main",
            "---\nname: foundry-models\ndescription: Manage Foundry models\n---\n\n# Foundry Models\n\nDeploy AI models.",
            "text/plain");

        var httpClient = new HttpClient(handler);
        var logger = Substitute.For<ILogger<GitHubSkillFetcher>>();
        var fetcher = new GitHubSkillFetcher(httpClient, logger);

        // Act
        var subSkills = await fetcher.FetchSubSkillsAsync("microsoft-foundry");

        // Assert
        subSkills.Should().HaveCount(2);
        subSkills.Should().Contain(s => s.Name == "foundry-agents");
        subSkills.Should().Contain(s => s.Name == "foundry-models");
        // Hidden directories should be excluded
        subSkills.Should().NotContain(s => s.Name == ".hidden");
    }

    [Fact]
    public async Task FetchSubSkillsAsync_NoSubdirectories_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler();

        // Directory listing with only files, no subdirectories
        var dirListing = new[]
        {
            new { name = "SKILL.md", type = "file" },
            new { name = "README.md", type = "file" }
        };
        handler.AddResponse(
            "https://api.github.com/repos/microsoft/azure-skills/contents/skills/azure-storage?ref=main",
            JsonSerializer.Serialize(dirListing),
            "application/json");

        var httpClient = new HttpClient(handler);
        var logger = Substitute.For<ILogger<GitHubSkillFetcher>>();
        var fetcher = new GitHubSkillFetcher(httpClient, logger);

        var subSkills = await fetcher.FetchSubSkillsAsync("azure-storage");

        subSkills.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchSubSkillsAsync_SubdirWithoutSkillMd_Skipped()
    {
        var handler = new MockHttpMessageHandler();

        var dirListing = new[]
        {
            new { name = "docs", type = "dir" },
            new { name = "scripts", type = "dir" }
        };
        handler.AddResponse(
            "https://api.github.com/repos/microsoft/azure-skills/contents/skills/azure-test?ref=main",
            JsonSerializer.Serialize(dirListing),
            "application/json");

        // Neither subdirectory has SKILL.md — return 404
        handler.AddNotFoundResponse(
            "https://api.github.com/repos/microsoft/azure-skills/contents/skills/azure-test/docs/SKILL.md?ref=main");
        handler.AddNotFoundResponse(
            "https://api.github.com/repos/microsoft/azure-skills/contents/skills/azure-test/scripts/SKILL.md?ref=main");

        var httpClient = new HttpClient(handler);
        var logger = Substitute.For<ILogger<GitHubSkillFetcher>>();
        var fetcher = new GitHubSkillFetcher(httpClient, logger);

        var subSkills = await fetcher.FetchSubSkillsAsync("azure-test");

        subSkills.Should().BeEmpty();
    }

    [Fact]
    public async Task ListSubdirectoriesAsync_ApiError_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddNotFoundResponse(
            "https://api.github.com/repos/microsoft/azure-skills/contents/skills/nonexistent?ref=main");

        var httpClient = new HttpClient(handler);
        var logger = Substitute.For<ILogger<GitHubSkillFetcher>>();
        var fetcher = new GitHubSkillFetcher(httpClient, logger);

        var subdirs = await fetcher.ListSubdirectoriesAsync("skills/nonexistent", CancellationToken.None);

        subdirs.Should().BeEmpty();
    }

    /// <summary>
    /// Simple mock HTTP handler for testing GitHub API calls.
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (string Content, string ContentType, HttpStatusCode StatusCode)> _responses = new();

        public void AddResponse(string url, string content, string contentType, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responses[url] = (content, contentType, statusCode);
        }

        public void AddNotFoundResponse(string url)
        {
            _responses[url] = ("", "text/plain", HttpStatusCode.NotFound);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri?.ToString() ?? "";
            if (_responses.TryGetValue(url, out var response))
            {
                return Task.FromResult(new HttpResponseMessage(response.StatusCode)
                {
                    Content = new StringContent(response.Content, Encoding.UTF8, response.ContentType)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
