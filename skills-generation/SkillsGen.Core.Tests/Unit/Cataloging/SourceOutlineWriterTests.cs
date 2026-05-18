using System.Text.Json;
using FluentAssertions;
using SkillsGen.Core.Cataloging;
using SkillsGen.Core.Models;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Cataloging;

public class SourceOutlineWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SourceOutlineWriter _writer;

    public SourceOutlineWriterTests()
    {
        _tempDir = Path.Combine(AppContext.BaseDirectory, "test-outline-output", Guid.NewGuid().ToString("N"));
        _writer = new SourceOutlineWriter(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Write_SingleSkill_ProducesValidJson()
    {
        var catalog = new Dictionary<string, SkillOutline>
        {
            ["azure-compute"] = new SkillOutline
            {
                Headings =
                [
                    new HeadingEntry { Level = 2, Text = "Prerequisites", MappedTo = "Prerequisites" },
                    new HeadingEntry { Level = 2, Text = "New Section", MappedTo = null }
                ],
                UnmappedCount = 1,
                CatalogedAt = new DateTime(2026, 5, 17, 15, 55, 0, DateTimeKind.Utc)
            }
        };

        _writer.Write(catalog);

        var path = Path.Combine(_tempDir, "source-outlines.json");
        File.Exists(path).Should().BeTrue();

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        root.TryGetProperty("azure-compute", out var skill).Should().BeTrue();

        skill.TryGetProperty("headings", out var headings).Should().BeTrue();
        headings.GetArrayLength().Should().Be(2);

        var first = headings[0];
        first.GetProperty("level").GetInt32().Should().Be(2);
        first.GetProperty("text").GetString().Should().Be("Prerequisites");
        first.GetProperty("mappedTo").GetString().Should().Be("Prerequisites");

        var second = headings[1];
        second.GetProperty("mappedTo").ValueKind.Should().Be(JsonValueKind.Null);

        skill.GetProperty("unmappedCount").GetInt32().Should().Be(1);
        skill.TryGetProperty("catalogedAt", out _).Should().BeTrue();
    }

    [Fact]
    public void Write_EmptyCatalog_ProducesEmptyJsonObject()
    {
        _writer.Write([]);

        var path = Path.Combine(_tempDir, "source-outlines.json");
        File.Exists(path).Should().BeTrue();

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public void Write_MultipleSkills_AllPresentInOutput()
    {
        var catalog = new Dictionary<string, SkillOutline>
        {
            ["azure-storage"] = new SkillOutline
            {
                Headings = [new HeadingEntry { Level = 2, Text = "Prerequisites", MappedTo = "Prerequisites" }],
                UnmappedCount = 0,
                CatalogedAt = DateTime.UtcNow
            },
            ["azure-monitor"] = new SkillOutline
            {
                Headings = [new HeadingEntry { Level = 2, Text = "Custom", MappedTo = null }],
                UnmappedCount = 1,
                CatalogedAt = DateTime.UtcNow
            }
        };

        _writer.Write(catalog);

        var json = File.ReadAllText(Path.Combine(_tempDir, "source-outlines.json"));
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("azure-storage", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("azure-monitor", out _).Should().BeTrue();
    }

    [Fact]
    public void Write_CreatesDataDirectoryIfMissing()
    {
        // _tempDir does not exist yet
        var newDir = Path.Combine(_tempDir, "subdir");
        var writer = new SourceOutlineWriter(newDir);

        writer.Write([]);

        Directory.Exists(newDir).Should().BeTrue();
        File.Exists(Path.Combine(newDir, "source-outlines.json")).Should().BeTrue();
    }
}
