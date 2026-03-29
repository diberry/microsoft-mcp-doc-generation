// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Shared;

/// <summary>
/// Snapshot of a prompt file at a point in time, including its content hash.
/// Used to track prompt versions across pipeline runs.
/// </summary>
public sealed record PromptSnapshot(
    string FileName,
    string ContentHash,
    long SizeBytes,
    DateTimeOffset LastModified);

/// <summary>
/// Computes SHA256 hashes for prompt content and files.
/// Enables prompt versioning by tracking content changes via deterministic hashes.
/// Hash is computed on post-token-resolution content so that changes to shared
/// includes (e.g., Acrolinx rules) are reflected in the hash.
/// </summary>
public static class PromptHasher
{
    /// <summary>
    /// Computes the SHA256 hash of the given content string.
    /// Returns the hash as a lowercase hex string (64 characters).
    /// </summary>
    public static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Reads a prompt file, optionally resolves tokens, and returns a snapshot
    /// with the content hash and file metadata.
    /// </summary>
    /// <param name="filePath">Absolute path to the prompt file.</param>
    /// <param name="dataDir">
    /// Optional path to the data directory for token resolution.
    /// When provided, <see cref="PromptTokenResolver.Resolve"/> expands tokens
    /// (e.g., {{ACROLINX_RULES}}) before hashing.
    /// When null, the raw file content is hashed.
    /// </param>
    public static async Task<PromptSnapshot> HashFileAsync(string filePath, string? dataDir = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Prompt file not found: {filePath}", filePath);

        // Capture metadata BEFORE reading content to avoid TOCTOU race (#332).
        var fileInfo = new FileInfo(filePath);
        var initialTimestamp = fileInfo.LastWriteTimeUtc;
        var initialSize = fileInfo.Length;

        var content = await File.ReadAllTextAsync(filePath);

        // Resolve tokens if a data directory is provided
        var contentToHash = dataDir is not null
            ? PromptTokenResolver.Resolve(content, dataDir)
            : content;

        // Verify file wasn't modified during read
        fileInfo.Refresh();
        if (fileInfo.LastWriteTimeUtc != initialTimestamp)
            throw new IOException($"Prompt file '{filePath}' was modified during read. Retry the operation.");

        var hash = ComputeHash(contentToHash);

        return new PromptSnapshot(
            FileName: Path.GetFileName(filePath),
            ContentHash: hash,
            SizeBytes: initialSize,
            LastModified: initialTimestamp);
    }
}
