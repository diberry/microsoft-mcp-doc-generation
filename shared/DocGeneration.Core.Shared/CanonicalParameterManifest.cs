// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// The v2 canonical parameter manifest with pre-built placeholder alias index.
/// </summary>
public sealed record CanonicalParameterManifest(
    string SchemaVersion,
    string ToolCommand,
    string Namespace,
    ManifestSourceIdentity SourceIdentity,
    IReadOnlyList<CanonicalParameterEntry> Parameters,
    IReadOnlyDictionary<string, string> PlaceholderAliasIndex);
