// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Xunit;
using Shared;

namespace Shared.Tests;

public class AiResponseArchiveEntryTests
{
    [Fact]
    public void Defaults_AreEmptyAndZero()
    {
        var entry = new AiResponseArchiveEntry();

        Assert.Equal("", entry.Step);
        Assert.Equal("", entry.ToolName);
        Assert.Equal("", entry.PromptHash);
        Assert.Equal("", entry.RawResponse);
        Assert.Equal("", entry.Model);
        Assert.Equal(default, entry.Timestamp);
        Assert.Equal(0, entry.PromptTokens);
        Assert.Equal(0, entry.CompletionTokens);
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesAllFields()
    {
        var timestamp = new DateTimeOffset(2025, 7, 15, 10, 30, 0, TimeSpan.Zero);
        var entry = new AiResponseArchiveEntry
        {
            Step = "Step 2 - Example Prompts",
            ToolName = "storage_blob_list",
            PromptHash = "abc123def456",
            RawResponse = "{\"prompts\":[\"List all blobs\"]}",
            Model = "gpt-4o-mini",
            Timestamp = timestamp,
            PromptTokens = 150,
            CompletionTokens = 300
        };

        var json = JsonSerializer.Serialize(entry);
        var deserialized = JsonSerializer.Deserialize<AiResponseArchiveEntry>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Step 2 - Example Prompts", deserialized!.Step);
        Assert.Equal("storage_blob_list", deserialized.ToolName);
        Assert.Equal("abc123def456", deserialized.PromptHash);
        Assert.Equal("{\"prompts\":[\"List all blobs\"]}", deserialized.RawResponse);
        Assert.Equal("gpt-4o-mini", deserialized.Model);
        Assert.Equal(timestamp, deserialized.Timestamp);
        Assert.Equal(150, deserialized.PromptTokens);
        Assert.Equal(300, deserialized.CompletionTokens);
    }

    [Fact]
    public void Serialization_UsesJsonPropertyNames()
    {
        var entry = new AiResponseArchiveEntry
        {
            Step = "Step 3",
            ToolName = "keyvault_secret_get",
            PromptHash = "hash123",
            RawResponse = "raw",
            Model = "gpt-4o",
            Timestamp = DateTimeOffset.UtcNow,
            PromptTokens = 100,
            CompletionTokens = 200
        };

        var json = JsonSerializer.Serialize(entry);

        Assert.Contains("\"step\"", json);
        Assert.Contains("\"toolName\"", json);
        Assert.Contains("\"promptHash\"", json);
        Assert.Contains("\"rawResponse\"", json);
        Assert.Contains("\"model\"", json);
        Assert.Contains("\"timestamp\"", json);
        Assert.Contains("\"promptTokens\"", json);
        Assert.Contains("\"completionTokens\"", json);
    }

    [Fact]
    public void Deserialization_FromJsonString_Works()
    {
        var json = """
        {
            "step": "Step 6",
            "toolName": "cosmos_query",
            "promptHash": "deadbeef",
            "rawResponse": "some response",
            "model": "gpt-4o-mini",
            "timestamp": "2025-07-15T12:00:00+00:00",
            "promptTokens": 50,
            "completionTokens": 75
        }
        """;

        var entry = JsonSerializer.Deserialize<AiResponseArchiveEntry>(json);

        Assert.NotNull(entry);
        Assert.Equal("Step 6", entry!.Step);
        Assert.Equal("cosmos_query", entry.ToolName);
        Assert.Equal("deadbeef", entry.PromptHash);
        Assert.Equal("some response", entry.RawResponse);
        Assert.Equal("gpt-4o-mini", entry.Model);
        Assert.Equal(50, entry.PromptTokens);
        Assert.Equal(75, entry.CompletionTokens);
    }
}
