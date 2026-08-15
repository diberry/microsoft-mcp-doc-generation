// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Source identity metadata for a parameter manifest.
/// </summary>
public sealed record ManifestSourceIdentity(string AzureMcpBuild, string GeneratedAtUtc);
