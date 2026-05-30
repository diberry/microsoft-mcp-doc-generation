// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace HorizontalArticleGenerator.Models;

public sealed record ArticleOutlineContext(
    string ArticleTitle,
    IReadOnlyList<ArticleOutlineSection> Sections,
    string ServiceIdentifier,
    string SchemaVersion = "1.0");

public sealed record ArticleOutlineSection(
    string Heading,
    string ContentType,
    IReadOnlyList<string> EvidenceItems);
