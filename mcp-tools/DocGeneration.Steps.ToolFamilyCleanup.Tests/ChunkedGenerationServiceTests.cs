// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class ChunkedGenerationServiceTests
{
    // ════════════════════════════════════════════════════════════════════════
    //  Threshold Detection
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ShouldUseChunkedGeneration_FiveTools_ReturnsFalse()
    {
        var tools = CreateTools(5, contentSize: 1000);

        var result = ChunkedGenerationService.ShouldUseChunkedGeneration(tools);

        Assert.False(result);
    }

    [Fact]
    public void ShouldUseChunkedGeneration_ElevenTools_ReturnsTrue()
    {
        var tools = CreateTools(11, contentSize: 1000);

        var result = ChunkedGenerationService.ShouldUseChunkedGeneration(tools);

        Assert.True(result);
    }

    [Fact]
    public void ShouldUseChunkedGeneration_TenToolsExactly_ReturnsFalse()
    {
        var tools = CreateTools(10, contentSize: 1000);

        var result = ChunkedGenerationService.ShouldUseChunkedGeneration(tools);

        Assert.False(result);
    }

    [Fact]
    public void ShouldUseChunkedGeneration_LargeMetadata35KB_ReturnsTrue()
    {
        // 5 tools each with 7KB content = 35KB total > 30KB threshold
        var tools = CreateTools(5, contentSize: 7168);

        var result = ChunkedGenerationService.ShouldUseChunkedGeneration(tools);

        Assert.True(result);
    }

    [Fact]
    public void ShouldUseChunkedGeneration_SmallMetadata_ReturnsFalse()
    {
        // 5 tools with 4KB each = 20KB total < 30KB threshold
        var tools = CreateTools(5, contentSize: 4096);

        var result = ChunkedGenerationService.ShouldUseChunkedGeneration(tools);

        Assert.False(result);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Batching
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateBatches_TwentyTools_CreatesFourBatches()
    {
        var tools = CreateTools(20);

        var batches = ChunkedGenerationService.CreateBatches(tools);

        Assert.Equal(4, batches.Count);
        Assert.All(batches, b => Assert.Equal(5, b.Count));
    }

    [Fact]
    public void CreateBatches_SevenTools_CreatesTwoBatches()
    {
        var tools = CreateTools(7);

        var batches = ChunkedGenerationService.CreateBatches(tools);

        Assert.Equal(2, batches.Count);
        Assert.Equal(5, batches[0].Count);
        Assert.Equal(2, batches[1].Count);
    }

    [Fact]
    public void CreateBatches_EmptyList_ReturnsEmpty()
    {
        var batches = ChunkedGenerationService.CreateBatches(Array.Empty<ToolContent>());

        Assert.Empty(batches);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Batch Merge with H2 Deduplication
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MergeBatchResults_ThreeBatchesOverlappingH2_MergesCorrectly()
    {
        var batch1 = "## Vault\n\n### List vaults\n\nLists all vaults.\n\n### Get vault\n\nGets a vault.\n";
        var batch2 = "## Vault\n\n### Create vault\n\nCreates a vault.\n\n## Policy\n\n### List policies\n\nLists policies.\n";
        var batch3 = "## Policy\n\n### Get policy\n\nGets a policy.\n\n### Delete policy\n\nDeletes a policy.\n";

        var merged = ChunkedGenerationService.MergeBatchResults(new[] { batch1, batch2, batch3 });

        // Vault H2 should appear once with all 3 vault tools
        var vaultH2Count = CountOccurrences(merged, "## Vault");
        Assert.Equal(1, vaultH2Count);

        // Policy H2 should appear once with all 3 policy tools
        var policyH2Count = CountOccurrences(merged, "## Policy");
        Assert.Equal(1, policyH2Count);

        // All H3 tools should be present
        Assert.Contains("### List vaults", merged);
        Assert.Contains("### Get vault", merged);
        Assert.Contains("### Create vault", merged);
        Assert.Contains("### List policies", merged);
        Assert.Contains("### Get policy", merged);
        Assert.Contains("### Delete policy", merged);
    }

    [Fact]
    public void MergeBatchResults_SingleBatch_ReturnsSameContent()
    {
        var content = "## Resources\n\n### List resources\n\nLists resources.\n";

        var merged = ChunkedGenerationService.MergeBatchResults(new[] { content });

        Assert.Equal(content, merged);
    }

    [Fact]
    public void MergeBatchResults_EmptyList_ReturnsEmpty()
    {
        var merged = ChunkedGenerationService.MergeBatchResults(Array.Empty<string>());

        Assert.Equal(string.Empty, merged);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Retry Logic
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GenerateChunkedAsync_BatchFailsThenSucceeds_Retries()
    {
        int callCount = 0;
        var tools = CreateTools(5);
        var expectedToolNames = tools.Select(t => t.ToolName).ToList();

        var service = new ChunkedGenerationService(async (batch) =>
        {
            callCount++;
            await Task.CompletedTask;

            if (callCount == 1)
            {
                // First attempt returns incomplete content (missing tools)
                return "### tool-1\n\nContent for tool-1.\n";
            }

            // Second attempt succeeds
            return GenerateValidBatchContent(batch);
        });

        var result = await service.GenerateChunkedAsync(tools);

        Assert.True(callCount >= 2);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GenerateChunkedAsync_BatchFailsThreeTimes_ThrowsWithToolNames()
    {
        var tools = CreateTools(5);

        var service = new ChunkedGenerationService(async (batch) =>
        {
            await Task.CompletedTask;
            // Always return empty/incomplete
            return "";
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GenerateChunkedAsync(tools));

        Assert.Contains("failed after 3 retries", ex.Message);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Integration: 20-tool namespace
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GenerateChunkedAsync_TwentyToolNamespace_GeneratesAllTools()
    {
        var tools = CreateTools(20);

        var service = new ChunkedGenerationService(async (batch) =>
        {
            await Task.CompletedTask;
            return GenerateValidBatchContent(batch);
        });

        var result = await service.GenerateChunkedAsync(tools);

        // All 20 tools should appear in merged output
        for (int i = 1; i <= 20; i++)
        {
            Assert.Contains($"### tool-{i}", result);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════════════════

    private static List<ToolContent> CreateTools(int count, int contentSize = 100)
    {
        var content = new string('x', contentSize);
        return Enumerable.Range(1, count)
            .Select(i => new ToolContent
            {
                ToolName = $"tool-{i}",
                FileName = $"azure-test-tool-{i}.complete.md",
                FamilyName = "test",
                Content = $"## tool-{i}\n\n{content}\n",
                ResourceType = i <= count / 2 ? "resource-a" : "resource-b"
            })
            .ToList();
    }

    private static string GenerateValidBatchContent(List<ToolContent> batch)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Resources");
        sb.AppendLine();
        foreach (var tool in batch)
        {
            sb.AppendLine($"### {tool.ToolName}");
            sb.AppendLine();
            sb.AppendLine($"Content for {tool.ToolName}.");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
