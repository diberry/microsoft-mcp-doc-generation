using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using Shared;
using ToolFamilyCleanup.Services;

namespace PipelineRunner.Validation;

public sealed class ToolFamilyPostAssemblyValidator : IPostValidator
{
    public const string FamilyNameContextKey = "ToolFamilyCleanup.FamilyName";
    public const string OutputFileNameContextKey = "ToolFamilyCleanup.OutputFileName";

    private static readonly Regex FrontmatterRegex = new(@"^---\s*\n(.*?)\n---\s*\n?", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HeadingRegex = new(@"(?m)^##\s+(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex McpCliRegex = new(@"(?m)^\s*<!--\s*@mcpcli\s+(.+?)\s*-->$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FencedCodeDelimiterRegex = new(@"^`{3,}(?:\s*[A-Za-z0-9_-]+)?\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly BrandingRule[] BrandingRules =
    [
        new(@"\bthis command\b", "Use \"this tool\" instead of \"this command\"."),
        new(@"\bCosmosDB\b", "Use \"Azure Cosmos DB\" on first mention instead of \"CosmosDB\"."),
        new(@"\bAzure VMs\b", "Use \"Azure Virtual Machines\" on first mention instead of \"Azure VMs\"."),
        new(@"\bMSSQL\b", "Use \"Azure SQL\" or \"SQL Server\" as appropriate instead of \"MSSQL\"."),
        new(@"\bFoundry\b", "Verify first mention uses the full product name (for example, \"Microsoft Foundry\")."),
    ];

    private static readonly Regex BacktickTermRegex = new(@"`([^`]{4,})`", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly string[] ToneMarkerPhrases = ["you can", "you will", "you use", "you should"];
    private static readonly Regex ToneMarkerSuperlativeRegex = new(@"\b(powerful|seamless|cutting-edge|game-changing)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public string Name => "ToolFamilyPostAssemblyValidator";

    public async ValueTask<ValidatorResult> ValidateAsync(PipelineContext context, IPipelineStep step, CancellationToken cancellationToken)
    {
        var familyName = ResolveFamilyName(context);
        var outputFileName = ResolveOutputFileName(context, familyName);
        var toolsDirectory = Path.Combine(context.OutputPath, "tools");
        var articlePath = Path.Combine(context.OutputPath, "tool-family", $"{outputFileName}.md");
        var reportDirectory = Path.Combine(context.OutputPath, "reports");
        var reportPath = Path.Combine(reportDirectory, $"tool-family-validation-{familyName}.txt");

        var blockingIssues = new List<string>();
        var warningIssues = new List<string>();
        var brandingIssues = new List<string>();
        var requiredParamIssues = new List<string>();
        var toolFiles = Array.Empty<NamespaceToolFile>();
        var sections = Array.Empty<ArticleSection>();
        var toolFileCount = 0;
        var articleSectionCount = 0;
        int? frontmatterToolCount = null;

        try
        {
            if (!Directory.Exists(toolsDirectory))
            {
                blockingIssues.Add($"Tools directory not found: {toolsDirectory}");
            }

            if (!File.Exists(articlePath))
            {
                blockingIssues.Add($"Tool-family article not found: {articlePath}");
            }

            if (blockingIssues.Count == 0)
            {
                var prefixes = await GetNamespaceFilePrefixesAsync(context.RepoRoot, familyName, cancellationToken);
                toolFiles = await GetNamespaceToolFilesAsync(toolsDirectory, familyName, prefixes, cancellationToken);
                toolFileCount = toolFiles.Length;
                if (toolFileCount == 0)
                {
                    blockingIssues.Add($"No tool files found for namespace '{familyName}' in {toolsDirectory}");
                }
            }

            if (blockingIssues.Count == 0)
            {
                var articleContent = await File.ReadAllTextAsync(articlePath, cancellationToken);
                var article = ParseArticleSections(articleContent, familyName);
                sections = article.Sections.ToArray();
                articleSectionCount = sections.Length;
                frontmatterToolCount = ParseToolCount(article.Frontmatter);

                var toolFileLookup = GroupByToolKey(toolFiles);
                var sectionLookup = GroupByToolKey(sections);

                var normalized = articleContent.Replace("\r\n", "\n", StringComparison.Ordinal);
                var mcpMarkerCount = McpCliRegex.Matches(normalized).Count;

                if (toolFileCount != mcpMarkerCount || frontmatterToolCount is null || frontmatterToolCount != toolFileCount)
                {
                    var details = new List<string>();
                    if (toolFileCount != mcpMarkerCount)
                    {
                        details.Add($"tool files: {toolFileCount}, @mcpcli markers: {mcpMarkerCount}");
                    }
                    if (frontmatterToolCount is null)
                    {
                        details.Add("frontmatter tool_count is missing");
                    }
                    else if (frontmatterToolCount != toolFileCount)
                    {
                        details.Add($"frontmatter tool_count: {frontmatterToolCount}, actual tool files: {toolFileCount}");
                    }
                    blockingIssues.Add($"Tool count integrity check failed ({string.Join("; ", details)}).");
                }

                foreach (var duplicate in toolFileLookup.Where(pair => pair.Value.Count > 1))
                {
                    var duplicateFiles = string.Join(", ", duplicate.Value.Select(file => file.Name));
                    blockingIssues.Add($"Duplicate tool file mapping for '{duplicate.Key}': {duplicateFiles}");
                }

                foreach (var duplicate in sectionLookup.Where(pair => pair.Value.Count > 1))
                {
                    var duplicateHeadings = string.Join(", ", duplicate.Value.Select(section => section.Heading));
                    blockingIssues.Add($"Duplicate article section mapping for '{duplicate.Key}': {duplicateHeadings}");
                }

                var missingFromArticle = toolFiles
                    .Where(file => !sectionLookup.ContainsKey(file.ToolKey))
                    .Select(file => file.Name)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var missingFromFiles = sections
                    .Where(section => !toolFileLookup.ContainsKey(section.ToolKey))
                    .Select(section => section.Heading)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (missingFromArticle.Length > 0 || missingFromFiles.Length > 0)
                {
                    var parts = new List<string>();
                    if (missingFromArticle.Length > 0)
                    {
                        var toolNames = string.Join(", ", missingFromArticle.Select(name => $"'{name}'"));
                        parts.Add($"{missingFromArticle.Length} tool(s) exist in /tools/ but have no H2 section in the article: {toolNames}. Regenerate the namespace to include them");
                    }
                    if (missingFromFiles.Length > 0)
                    {
                        var sectionNames = string.Join(", ", missingFromFiles.Select(name => $"'{name}'"));
                        parts.Add($"{missingFromFiles.Length} article section(s) have no matching tool file: {sectionNames}");
                    }
                    blockingIssues.Add($"Cross-reference check failed. {string.Join(". ", parts)}.");
                }

                foreach (var issue in GetRequiredParameterIssues(sections))
                {
                    requiredParamIssues.Add(issue);
                    blockingIssues.Add(issue);
                }

                foreach (var warning in GetExampleHeaderWarnings(sections))
                {
                    warningIssues.Add(warning);
                }

                foreach (var warning in GetMarkerWarnings(sections))
                {
                    warningIssues.Add(warning);
                }

                brandingIssues.AddRange(GetBrandingIssues(articleContent));

                var annotationFormatIssues = GetAnnotationFormatIssues(articleContent);
                foreach (var issue in annotationFormatIssues)
                {
                    blockingIssues.Add(issue);
                }

                var expectedAnnotationValues = BuildExpectedAnnotationValues(context);
                foreach (var issue in AnnotationValueValidator.GetValueMismatchIssues(articleContent, expectedAnnotationValues))
                {
                    blockingIssues.Add(issue);
                }

                var relatedToolsIssues = GetRelatedToolsCompletenessIssues(article.RelatedSectionText, sections, toolFiles);
                foreach (var issue in relatedToolsIssues)
                {
                    blockingIssues.Add(issue);
                }

                foreach (var issue in GetSourceVerificationIssues(context, familyName, article, frontmatterToolCount))
                {
                    blockingIssues.Add(issue);
                }

                var toneMarkerWarnings = GetToneMarkerWarnings(articleContent);
                warningIssues.AddRange(toneMarkerWarnings);

                var boilerplateWarnings = GetBoilerplateRedundancyWarnings(sections);
                warningIssues.AddRange(boilerplateWarnings);

                var relatedSectionWarnings = GetRelatedSectionHeaderWarnings(article.HasRelatedSection);
                warningIssues.AddRange(relatedSectionWarnings);

                var missingExampleIssues = GetMissingExampleIssues(sections);
                foreach (var issue in missingExampleIssues)
                {
                    blockingIssues.Add(issue);
                }

                var lowParamCountWarnings = GetLowParameterCountWarnings(sections);
                warningIssues.AddRange(lowParamCountWarnings);

                var postAssemblyChecks = new PostAssemblyCheckSummary(
                    relatedToolsIssues,
                    toneMarkerWarnings,
                    boilerplateWarnings,
                    article.HasRelatedSection,
                    missingExampleIssues,
                    lowParamCountWarnings);

                var reportLines = BuildReportLines(new ValidatorReportContext(
                    familyName,
                    toolFiles,
                    sections,
                    frontmatterToolCount,
                    missingFromArticle,
                    missingFromFiles,
                    blockingIssues,
                    warningIssues,
                    requiredParamIssues,
                    brandingIssues,
                    postAssemblyChecks));

                await WriteReportAsync(reportDirectory, reportPath, reportLines, cancellationToken);
            }
            else
            {
                var reportLines = BuildFailureReportLines(familyName, blockingIssues);
                await WriteReportAsync(reportDirectory, reportPath, reportLines, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            blockingIssues.Add($"Validator execution failed: {ex.Message}");
            var reportLines = BuildFailureReportLines(familyName, blockingIssues);
            await WriteReportAsync(reportDirectory, reportPath, reportLines, cancellationToken);
        }

        var surfacedWarnings = new List<string>();
        surfacedWarnings.AddRange(blockingIssues.Select(issue => $"Blocking: {issue}"));
        surfacedWarnings.AddRange(warningIssues);
        surfacedWarnings.AddRange(brandingIssues.Select(issue => $"Branding: {issue}"));

        return new ValidatorResult(Name, blockingIssues.Count == 0, surfacedWarnings);
    }

    private static string ResolveFamilyName(PipelineContext context)
    {
        if (context.Items.TryGetValue(FamilyNameContextKey, out var familyNameValue) && familyNameValue is string familyName && !string.IsNullOrWhiteSpace(familyName))
        {
            return familyName.Trim().ToLowerInvariant();
        }

        if (context.Items.TryGetValue("Namespace", out var namespaceValue) && namespaceValue is string currentNamespace && !string.IsNullOrWhiteSpace(currentNamespace))
        {
            var normalized = NormalizeToolCommand(currentNamespace);
            return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        }

        throw new InvalidOperationException("Tool-family validation requires a current namespace or family name in the pipeline context.");
    }

    private static string ResolveOutputFileName(PipelineContext context, string familyName)
    {
        if (context.Items.TryGetValue(OutputFileNameContextKey, out var outputFileNameValue)
            && outputFileNameValue is string outputFileName
            && !string.IsNullOrWhiteSpace(outputFileName))
        {
            return outputFileName.Trim().ToLowerInvariant();
        }

        return familyName;
    }

    private static async Task<IReadOnlyList<string>> GetNamespaceFilePrefixesAsync(string repoRoot, string namespaceName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prefixes = new List<string> { namespaceName.ToLowerInvariant() };

        // Load namespace→filename mappings from config/namespace-mapping.json (issue #582)
        var loader = new NamespaceMappingLoader();
        var namespaceMappings = await loader.LoadAsync(repoRoot, cancellationToken);
        if (namespaceMappings.TryGetValue(namespaceName, out var fileName) && !string.IsNullOrWhiteSpace(fileName))
        {
            // Strip .md extension to get file prefix (e.g., "azure-storage.md" → "azure-storage")
            var mappedPrefix = Path.GetFileNameWithoutExtension(fileName).Trim().ToLowerInvariant();
            prefixes.Add(mappedPrefix);
            if (!mappedPrefix.StartsWith("azure-", StringComparison.OrdinalIgnoreCase))
            {
                prefixes.Add($"azure-{mappedPrefix}");
            }
        }

        prefixes.Add($"ai-{namespaceName}");
        prefixes.Add($"azure-{namespaceName}");

        return prefixes
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(prefix => prefix.Length)
            .ToArray();
    }

    private static async Task<NamespaceToolFile[]> GetNamespaceToolFilesAsync(string toolsDirectory, string namespaceName, IReadOnlyList<string> prefixes, CancellationToken cancellationToken)
    {
        var files = new List<NamespaceToolFile>();
        foreach (var filePath in Directory.EnumerateFiles(toolsDirectory, "*.md", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var baseName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            string? commandText = null;
            foreach (var candidate in GetMcpCliCommands(content))
            {
                var normalized = NormalizeToolCommand(candidate);
                var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0 && string.Equals(tokens[0], namespaceName, StringComparison.OrdinalIgnoreCase))
                {
                    commandText = candidate;
                    break;
                }
            }

            string? matchedPrefix = null;
            foreach (var prefix in prefixes)
            {
                if (string.Equals(baseName, prefix, StringComparison.OrdinalIgnoreCase)
                    || baseName.StartsWith($"{prefix}-", StringComparison.OrdinalIgnoreCase))
                {
                    matchedPrefix = prefix;
                    break;
                }
            }

            if (commandText is null && matchedPrefix is null)
            {
                continue;
            }

            var toolKey = commandText is not null
                ? ConvertCommandToToolKey(commandText, namespaceName)
                : matchedPrefix is not null && baseName.StartsWith($"{matchedPrefix}-", StringComparison.OrdinalIgnoreCase)
                    ? baseName[(matchedPrefix.Length + 1)..]
                    : baseName;

            files.Add(new NamespaceToolFile(Path.GetFileName(filePath), filePath, toolKey, commandText));
        }

        return files.ToArray();
    }

    private static ParsedArticle ParseArticleSections(string articleContent, string namespaceName)
    {
        var normalized = articleContent.Replace("\r\n", "\n", StringComparison.Ordinal);
        var frontmatterMatch = FrontmatterRegex.Match(normalized);
        if (!frontmatterMatch.Success)
        {
            throw new InvalidOperationException("Tool-family article is missing YAML frontmatter.");
        }

        var frontmatter = frontmatterMatch.Groups[1].Value;
        var body = normalized[frontmatterMatch.Length..];
        var headingMatches = HeadingRegex.Matches(body);
        var sections = new List<ArticleSection>();
        var hasRelatedSection = false;
        var relatedSectionText = string.Empty;

        for (var index = 0; index < headingMatches.Count; index++)
        {
            var heading = headingMatches[index].Groups[1].Value.Trim();
            var startIndex = headingMatches[index].Index;
            var endIndex = index < headingMatches.Count - 1 ? headingMatches[index + 1].Index : body.Length;

            if (string.Equals(heading, "Related content", StringComparison.OrdinalIgnoreCase)
                || string.Equals(heading, "See also", StringComparison.OrdinalIgnoreCase)
                || string.Equals(heading, "Related tools", StringComparison.OrdinalIgnoreCase))
            {
                hasRelatedSection = true;
                relatedSectionText = body.Substring(startIndex, endIndex - startIndex).TrimEnd();
                continue;
            }

            var sectionText = body.Substring(startIndex, endIndex - startIndex).TrimEnd();
            var sectionLines = sectionText.Split('\n');
            var commands = GetMcpCliCommands(sectionText);
            var toolKey = commands.Count > 0
                ? ConvertCommandToToolKey(commands[0], namespaceName)
                : ParameterCoverageChecker.ConvertToSlug(heading);

            var markerLineIndices = new List<int>();
            var exampleHeaderIndex = -1;
            var tableStartIndex = -1;
            string? alternateExampleHeader = null;
            var alternateExampleHeaderIndex = -1;

            for (var lineIndex = 0; lineIndex < sectionLines.Length; lineIndex++)
            {
                var trimmed = sectionLines[lineIndex].Trim();
                if (trimmed.StartsWith("<!-- @mcpcli ", StringComparison.Ordinal))
                {
                    markerLineIndices.Add(lineIndex);
                }
                if (string.Equals(trimmed, "Example prompts include:", StringComparison.Ordinal))
                {
                    exampleHeaderIndex = lineIndex;
                }
                if (tableStartIndex < 0 && trimmed.StartsWith('|'))
                {
                    tableStartIndex = lineIndex;
                }
                if (alternateExampleHeader is null && Regex.IsMatch(trimmed, "^(?i)(example prompts|example commands|usage examples|examples|try this|to .* use commands like):"))
                {
                    alternateExampleHeader = trimmed;
                    alternateExampleHeaderIndex = lineIndex;
                }
            }

            var examplePromptStartIndex = exampleHeaderIndex >= 0
                ? exampleHeaderIndex
                : alternateExampleHeaderIndex;
            var examplePrompts = ExtractExamplePrompts(sectionLines, examplePromptStartIndex);
            var parameterRows = GetSectionParameterRows(sectionLines);
            var requiredParameters = parameterRows
                .Where(row => row.IsRequired)
                .Select(row => row.ParameterName)
                .ToArray();

            sections.Add(new ArticleSection(
                heading,
                toolKey,
                commands.ToArray(),
                markerLineIndices.Count,
                markerLineIndices.ToArray(),
                exampleHeaderIndex,
                tableStartIndex,
                alternateExampleHeader,
                examplePrompts,
                requiredParameters,
                parameterRows.Count,
                parameterRows,
                ExtractDescriptionParagraphs(sectionLines)));
        }

        return new ParsedArticle(frontmatter, sections, hasRelatedSection, relatedSectionText);
    }

    private static IReadOnlyList<string> ExtractExamplePrompts(IReadOnlyList<string> sectionLines, int exampleHeaderIndex)
    {
        if (exampleHeaderIndex < 0)
        {
            return Array.Empty<string>();
        }

        var examplePrompts = new List<string>();
        string? currentPrompt = null;
        for (var lineIndex = exampleHeaderIndex + 1; lineIndex < sectionLines.Count; lineIndex++)
        {
            var trimmed = sectionLines[lineIndex].Trim();
            if (trimmed.StartsWith('|')
                || trimmed.StartsWith("[Tool annotation hints]", StringComparison.Ordinal)
                || trimmed.StartsWith("Destructive:", StringComparison.Ordinal)
                || trimmed.StartsWith("<!-- @mcpcli ", StringComparison.Ordinal))
            {
                break;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(currentPrompt))
                {
                    examplePrompts.Add(currentPrompt);
                }
                currentPrompt = trimmed[2..].Trim();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(currentPrompt) && !string.IsNullOrWhiteSpace(trimmed))
            {
                currentPrompt = $"{currentPrompt} {trimmed}";
            }
        }

        if (!string.IsNullOrWhiteSpace(currentPrompt))
        {
            examplePrompts.Add(currentPrompt);
        }

        return examplePrompts;
    }

    private static IReadOnlyList<ParameterRow> GetSectionParameterRows(IReadOnlyList<string> lines)
    {
        var rows = new List<ParameterRow>();

        static bool IsTableLine(string line)
            => line.Trim().StartsWith('|');

        static bool IsSeparatorLine(string line)
            => Regex.IsMatch(line.Trim(), "^\\|\\s*[-: ]+\\|");

        static void AddParameterRowsFromTable(List<string> sourceTableLines, List<ParameterRow> targetRows)
        {
            if (sourceTableLines.Count < 2)
            {
                return;
            }

            var headerCells = ConvertTableLineToCells(sourceTableLines[0]);
            var parameterIndex = -1;
            var requiredIndex = -1;
            for (var index = 0; index < headerCells.Count; index++)
            {
                var header = ParameterCoverageChecker.RemoveMarkup(headerCells[index]);
                if (Regex.IsMatch(header, "(?i)^parameter$"))
                {
                    parameterIndex = index;
                }
                if (Regex.IsMatch(header, "(?i)required"))
                {
                    requiredIndex = index;
                }
            }

            if (parameterIndex < 0 || requiredIndex < 0)
            {
                return;
            }

            foreach (var line in sourceTableLines.Skip(1))
            {
                if (Regex.IsMatch(line, "^\\|\\s*[-: ]+\\|"))
                {
                    continue;
                }

                var cells = ConvertTableLineToCells(line);
                if (cells.Count <= Math.Max(parameterIndex, requiredIndex))
                {
                    continue;
                }

                var parameterName = ParameterCoverageChecker.RemoveMarkup(cells[parameterIndex]);
                var requiredValue = ParameterCoverageChecker.RemoveMarkup(cells[requiredIndex]);
                var isRequired = Regex.IsMatch(requiredValue, "(?i)^(yes|✅|required\\*?)$") || Regex.IsMatch(requiredValue, "(?i)^required");
                targetRows.Add(new ParameterRow(parameterName, requiredValue, isRequired));
            }
        }

        for (var index = 0; index < lines.Count - 1; index++)
        {
            if (!IsTableLine(lines[index]) || !IsSeparatorLine(lines[index + 1]))
            {
                continue;
            }

            var tableLines = new List<string>
            {
                lines[index].Trim(),
                lines[index + 1].Trim(),
            };

            var rowIndex = index + 2;
            while (rowIndex < lines.Count && IsTableLine(lines[rowIndex]))
            {
                if (rowIndex + 1 < lines.Count && IsTableLine(lines[rowIndex]) && IsSeparatorLine(lines[rowIndex + 1]))
                {
                    break;
                }

                tableLines.Add(lines[rowIndex].Trim());
                rowIndex++;
            }

            AddParameterRowsFromTable(tableLines, rows);
            index = rowIndex - 1;
        }

        return rows;
    }

    private static IReadOnlyList<string> ConvertTableLineToCells(string line)
        => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

    private static int? ParseToolCount(string frontmatter)
    {
        var toolCountValue = GetFrontmatterValue(frontmatter, "tool_count");
        return int.TryParse(toolCountValue, out var parsed) ? parsed : null;
    }

    private static string? GetFrontmatterValue(string frontmatter, string name)
    {
        var match = Regex.Match(frontmatter, $"(?mi)^{Regex.Escape(name)}\\s*:\\s*(.+?)\\s*$");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static Dictionary<string, List<NamespaceToolFile>> GroupByToolKey(IEnumerable<NamespaceToolFile> toolFiles)
        => toolFiles.GroupBy(file => file.ToolKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, List<ArticleSection>> GroupByToolKey(IEnumerable<ArticleSection> sections)
        => sections.GroupBy(section => section.ToolKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> GetSourceVerificationIssues(
        PipelineContext context,
        string familyName,
        ParsedArticle article,
        int? frontmatterToolCount)
    {
        var issues = new List<string>();
        if (context.CliOutput is null)
        {
            return issues;
        }

        var sourceNamespace = context.Items.TryGetValue("Namespace", out var namespaceValue) && namespaceValue is string namespaceName
            ? namespaceName
            : familyName;

        var sourceSnapshot = context.SourceCliOutput;
        if (sourceSnapshot is null)
        {
            var configuredVersion = SourceVersionVerificationGate.ResolveConfiguredVersion(context.RepoRoot);
            if (string.IsNullOrWhiteSpace(configuredVersion))
            {
                return issues;
            }

            var sourceResolution = SourceCliMetadataResolver.ResolveAsync(context.RepoRoot, configuredVersion, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            if (!sourceResolution.Success)
            {
                issues.Add($"Source CLI JSON check failed: {sourceResolution.Error}");
                return issues;
            }

            sourceSnapshot = sourceResolution.Snapshot!;
            context.SourceCliOutput = sourceSnapshot;
        }

        var articleVersion = GetFrontmatterValue(article.Frontmatter, MetadataConstants.McpCliVersionFrontmatterName);
        var normalizedArticleVersion = NormalizeVersionForComparison(articleVersion);
        var sourceVersion = SourceVersionVerificationGate.ExtractVersionFromSourcePath(sourceSnapshot.FilePath);
        if (!string.IsNullOrWhiteSpace(sourceVersion)
            && !string.Equals(sourceVersion, "unknown", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedArticleVersion, sourceVersion, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Version stamp check failed (frontmatter mcp-cli.version: {articleVersion ?? "missing"}, source version: {sourceVersion}).");
        }

        var sourceJsonVersion = SourceVersionVerificationGate.ExtractVersionFromCliOutput(sourceSnapshot.RawRoot);
        if (!string.IsNullOrWhiteSpace(sourceVersion)
            && !string.IsNullOrWhiteSpace(sourceJsonVersion)
            && !string.Equals(sourceVersion, sourceJsonVersion, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Source CLI JSON version check failed: source metadata folder version '{sourceVersion}' != source CLI JSON version '{sourceJsonVersion}'.");
        }

        IReadOnlyList<CliTool> sourceTools;
        try
        {
            sourceTools = ResolveSourceToolsForArticle(context, sourceNamespace, article, sourceSnapshot);
        }
        catch (InvalidOperationException ex)
        {
            issues.Add($"Source CLI JSON check failed: {ex.Message}");
            return issues;
        }

        if (frontmatterToolCount is not null && frontmatterToolCount != sourceTools.Count)
        {
            issues.Add($"Source tool count check failed (frontmatter tool_count: {frontmatterToolCount}, source CLI JSON tools: {sourceTools.Count}).");
        }

        var sourceCommands = sourceTools
            .Select(tool => NormalizeToolCommand(tool.Command))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var articleCommands = article.Sections
            .SelectMany(section => section.Commands)
            .Select(NormalizeToolCommand)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingArticleCommands = sourceCommands
            .Except(articleCommands, StringComparer.OrdinalIgnoreCase)
            .OrderBy(command => command, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var extraArticleCommands = articleCommands
            .Except(sourceCommands, StringComparer.OrdinalIgnoreCase)
            .OrderBy(command => command, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missingArticleCommands.Length > 0)
        {
            issues.Add($"Source CLI JSON check failed: source tool(s) missing from article markers: {string.Join(", ", missingArticleCommands.Select(command => $"'{command}'"))}.");
        }

        if (extraArticleCommands.Length > 0)
        {
            issues.Add($"Source CLI JSON check failed: article marker(s) are not present in source CLI JSON: {string.Join(", ", extraArticleCommands.Select(command => $"'{command}'"))}.");
        }

        var sectionsByCommand = article.Sections
            .SelectMany(section => section.Commands.Select(command => new { Command = NormalizeToolCommand(command), Section = section }))
            .GroupBy(entry => entry.Command, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Section, StringComparer.OrdinalIgnoreCase);

        foreach (var sourceTool in sourceTools)
        {
            var normalizedCommand = NormalizeToolCommand(sourceTool.Command);
            if (!sectionsByCommand.TryGetValue(normalizedCommand, out var section))
            {
                continue;
            }

            if (!sourceTool.Raw.TryGetProperty(MetadataConstants.OptionPropertyName, out var optionsProperty) || optionsProperty.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var sourceParameters = SourceVerificationHelpers.GetSourceParameters(optionsProperty);
            var sourceParameterNames = sourceParameters
                .Select(parameter => parameter.NormalizedName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var documentedParameters = section.ParameterRows
                .Select(row => SourceVerificationHelpers.NormalizeParameterName(row.ParameterName))
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var extraParameters = documentedParameters
                .Except(sourceParameterNames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(parameter => parameter, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (extraParameters.Length > 0)
            {
                issues.Add($"Source CLI JSON check failed for '{normalizedCommand}': parameter(s) documented but not present in source CLI JSON: {string.Join(", ", extraParameters.Select(parameter => $"'{parameter}'"))}.");
            }

            var missingRequiredParameters = sourceParameters
                .Where(parameter => parameter.Required)
                .Select(parameter => parameter.NormalizedName)
                .Except(documentedParameters, StringComparer.OrdinalIgnoreCase)
                .OrderBy(parameter => parameter, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missingRequiredParameters.Length > 0)
            {
                issues.Add($"Source CLI JSON check failed for '{normalizedCommand}': required source parameter(s) missing from article: {string.Join(", ", missingRequiredParameters.Select(parameter => $"'{parameter}'"))}.");
            }
        }

        return issues;
    }

    private static string? NormalizeVersionForComparison(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var trimmed = version.Trim();
        var buildMetadataIndex = trimmed.IndexOf('+', StringComparison.Ordinal);
        return buildMetadataIndex >= 0
            ? trimmed[..buildMetadataIndex]
            : trimmed;
    }

    private static IReadOnlyList<CliTool> ResolveSourceToolsForArticle(
        PipelineContext context,
        string sourceNamespace,
        ParsedArticle article,
        CliMetadataSnapshot sourceSnapshot)
    {
        var roots = article.Sections
                .SelectMany(section => section.Commands)
                .Select(NormalizeToolCommand)
                .Select(command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Append(sourceNamespace)
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var tools = new Dictionary<string, CliTool>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
                var matches = context.TargetMatcher.FindMatches(sourceSnapshot.Tools, root!);
                foreach (var match in matches)
                {
                    tools.TryAdd(NormalizeToolCommand(match.Command), match);
                }
        }

        return tools.Values
                .OrderBy(tool => NormalizeToolCommand(tool.Command), StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private static IReadOnlyList<string> GetRequiredParameterIssues(IEnumerable<ArticleSection> sections)
    {
        var issues = new List<string>();
        foreach (var section in sections)
        {
            if (section.RequiredParameters.Count == 0)
            {
                continue;
            }

            var missingParameters = new List<string>();
            foreach (var requiredParameter in section.RequiredParameters)
            {
                var coverage = ParameterCoverageChecker.GetConcretePromptCoverage(section.ExamplePrompts, requiredParameter, section.RequiredParameters.Count);
                if (!coverage.Covered && !coverage.PlaceholderDetected)
                {
                    missingParameters.Add(requiredParameter);
                }
            }

            if (missingParameters.Count > 0)
            {
                var suffix = missingParameters.Count > 1 ? "s" : string.Empty;
                var joined = string.Join(", ", missingParameters.Select(parameter => $"'{parameter}'"));
                issues.Add($"🛑 {section.ToolKey}: missing {joined} in example prompt{suffix}");
            }
        }

        return issues;
    }

    private static IReadOnlyList<string> GetExampleHeaderWarnings(IEnumerable<ArticleSection> sections)
    {
        var warnings = new List<string>();
        foreach (var section in sections)
        {
            var headerIsStandard = section.ExampleHeaderIndex >= 0;
            var headerIsPositionedCorrectly = true;
            if (headerIsStandard && section.MarkerLineIndices.Count > 0)
            {
                headerIsPositionedCorrectly = section.ExampleHeaderIndex > section.MarkerLineIndices[^1];
            }
            if (headerIsStandard && section.TableStartIndex >= 0)
            {
                headerIsPositionedCorrectly &= section.ExampleHeaderIndex < section.TableStartIndex;
            }

            if (!headerIsStandard || !headerIsPositionedCorrectly)
            {
                var usedHeader = section.AlternateExampleHeader ?? (section.ExampleHeaderIndex < 0 ? "missing" : "misplaced");
                warnings.Add($"⚠️ {section.ToolKey}: example prompt header is {usedHeader}");
            }
        }

        return warnings;
    }

    private static IReadOnlyList<string> GetMarkerWarnings(IEnumerable<ArticleSection> sections)
        => sections
            .Where(section => section.MarkerCount != 1)
            .Select(section => $"⚠️ {section.ToolKey}: expected 1 annotation marker, found {section.MarkerCount}")
            .ToArray();

    private static readonly Regex InlineAnnotationLineRegex = new(
        @"(?m)^\s*Destructive\s*:\s*(✅|❌)\s*\|",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AnnotationLinkRegex = new(
        @"\[Tool annotation hints\]\(index\.md#tool-annotations-for-azure-mcp-server\):",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AnnotationTableSeparatorRegex = new(
        @"\|:-----------:\|:----------:\|:----------:\|:---------:\|:------:\|:--------------:\|",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns blocking issues if any annotation block uses the old inline format
    /// instead of the required 3-row markdown table format.
    /// Fails if: any line immediately after an annotation-hints link matches the old inline format.
    /// Fails if: annotation links exist but the table separator "|:-----------:|" is absent.
    /// Guard 1 is scoped to annotation block context (lines following the annotation-hints link)
    /// to avoid false positives from prose examples of the old format embedded elsewhere in the
    /// article. Generated tool-family content does not embed old-format examples in prose, so this
    /// scoping is low-risk and defensive only.
    /// </summary>
    private static IReadOnlyList<string> GetAnnotationFormatIssues(string articleContent)
    {
        var issues = new List<string>();
        var normalized = articleContent.Replace("\r\n", "\n", StringComparison.Ordinal);

        // Guard 1: No inline annotation value lines allowed in annotation blocks.
        // Scoped to lines that follow an annotation-hints link line to avoid flagging
        // prose examples of the old format that may appear elsewhere in the document.
        var checkLines = normalized.Split('\n');
        bool foundInlineInAnnotationBlock = false;
        for (int lineIdx = 0; lineIdx < checkLines.Length && !foundInlineInAnnotationBlock; lineIdx++)
        {
            if (!AnnotationLinkRegex.IsMatch(checkLines[lineIdx]))
                continue;
            // Scan the next few lines (up to 4) — the inline/table content comes immediately after.
            for (int j = lineIdx + 1; j < checkLines.Length && j <= lineIdx + 4; j++)
            {
                if (string.IsNullOrWhiteSpace(checkLines[j]))
                    continue;
                if (InlineAnnotationLineRegex.IsMatch(checkLines[j]))
                    foundInlineInAnnotationBlock = true;
                break; // First non-blank line after the link is the format indicator
            }
        }
        if (foundInlineInAnnotationBlock)
        {
            issues.Add(
                "Annotation block uses old inline format (\"Destructive: ✅ | ...\"). " +
                "Annotations must use the 3-row markdown table format with header, " +
                "centered-alignment separator (|:-----------:|), and values rows.");
        }

        // Guard 2: Every annotation link must be followed by the table format.
        var annotationLinkCount = AnnotationLinkRegex.Matches(normalized).Count;
        var tableSeparatorCount = AnnotationTableSeparatorRegex.Matches(normalized).Count;
        if (annotationLinkCount > 0 && tableSeparatorCount < annotationLinkCount)
        {
            issues.Add(
                $"Annotation table format incomplete: found {annotationLinkCount} annotation block(s) " +
                $"but only {tableSeparatorCount} table separator row(s) " +
                "(|:-----------:|:----------:|...). Each annotation block must have a 3-row table.");
        }

        return issues;
    }

    /// <summary>
    /// Builds the expected annotation values (per command) from the CLI <c>tools list</c>
    /// metadata carried on <see cref="PipelineContext.CliOutput"/>. Each tool maps to six
    /// booleans in <see cref="AnnotationValueValidator.ColumnFields"/> order. A field that is
    /// absent from the metadata defaults to <see langword="false"/> (❌), matching the annotation
    /// template's rendering, so missing fields never produce false-positive mismatches.
    /// Returns an empty map when no CLI snapshot is available (value validation then no-ops).
    /// </summary>
    private static Dictionary<string, bool[]> BuildExpectedAnnotationValues(PipelineContext context)
    {
        var result = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase);
        var snapshot = context.CliOutput;
        if (snapshot is null)
        {
            return result;
        }

        foreach (var tool in snapshot.Tools)
        {
            if (string.IsNullOrWhiteSpace(tool.Command))
            {
                continue;
            }

            var values = new bool[AnnotationValueValidator.ColumnFields.Length];
            if (tool.Raw.ValueKind == JsonValueKind.Object
                && tool.Raw.TryGetProperty("metadata", out var metadata)
                && metadata.ValueKind == JsonValueKind.Object)
            {
                values[0] = ReadMetadataFlag(metadata, "destructive");
                values[1] = ReadMetadataFlag(metadata, "idempotent");
                values[2] = ReadMetadataFlag(metadata, "openWorld");
                values[3] = ReadMetadataFlag(metadata, "readOnly");
                values[4] = ReadMetadataFlag(metadata, "secret");
                values[5] = ReadMetadataFlag(metadata, "localRequired");
            }

            var key = Regex.Replace(tool.Command.Trim(), @"\s+", " ");
            result[key] = values;
        }

        return result;
    }

    private static bool ReadMetadataFlag(JsonElement metadata, string field)
        => metadata.TryGetProperty(field, out var flag)
            && flag.ValueKind == JsonValueKind.Object
            && flag.TryGetProperty("value", out var value)
            && value.ValueKind == JsonValueKind.True;

    private static IReadOnlyList<string> GetBrandingIssues(string articleContent)
    {
        var issues = new List<string>();
        var normalized = articleContent.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in normalized.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith('|'))
            {
                continue;
            }

            foreach (var check in BrandingRules)
            {
                if (check.Pattern.IsMatch(trimmed))
                {
                    if (ReferenceEquals(check, BrandingRules[^1]) && trimmed.Contains("Microsoft Foundry", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    issues.Add($"{check.Message} [{trimmed}]");
                    break;
                }
            }
        }

        return issues
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(issue => issue, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AppendPassFailSection(
        List<string> lines, string label, IReadOnlyList<string> issues)
    {
        lines.Add(issues.Count == 0
            ? $"{label}: ✅ PASS"
            : $"{label}: ❌ FAIL ({issues.Count} issue(s))");
        lines.AddRange(issues.Select(issue => $"  - {issue}"));
    }

    private static void AppendCountSection(
        List<string> lines, string label, IReadOnlyList<string> items, bool isBlocking)
    {
        lines.Add(items.Count == 0
            ? $"{label}: ✅ none detected"
            : $"{label}: {(isBlocking ? "❌" : "⚠️")} {items.Count} detected");
        lines.AddRange(items.Select(item => $"  {item}"));
    }

    private static void AppendRatioSection(
        List<string> lines, string label, IReadOnlyList<string> issues, int total,
        bool isBlocking, string itemDescription)
    {
        var passing = total - issues.Count;
        lines.Add(issues.Count == 0
            ? $"{label}: ✅ {passing}/{total} {itemDescription}"
            : $"{label}: {(isBlocking ? "❌" : "⚠️")} {passing}/{total} {itemDescription}");
        lines.AddRange(issues.Select(item => $"  {item}"));
    }

    private static IReadOnlyList<string> BuildReportLines(ValidatorReportContext ctx)
    {
        var reportLines = new List<string>
        {
            $"=== Tool Family Validation: {ctx.FamilyName} ===",
            $"Tool files found: {ctx.ToolFiles.Count}",
            $"Article H2 sections: {ctx.Sections.Count}",
            $"Frontmatter tool_count: {(ctx.FrontmatterToolCount is not null ? ctx.FrontmatterToolCount.Value : "missing")}",
            ctx.ToolFiles.Count == ctx.Sections.Count && ctx.FrontmatterToolCount == ctx.ToolFiles.Count
                ? "✅ Tool count integrity: PASS"
                : "❌ Tool count integrity: FAIL",
            string.Empty,
            "Cross-reference:",
            ctx.MissingFromArticle.Count == 0
                ? $"  ✅ All {ctx.ToolFiles.Count} tool files have matching article sections"
                : $"  ❌ Missing from article: {ctx.MissingFromArticle.Count}",
        };

        if (ctx.MissingFromArticle.Count > 0)
        {
            reportLines.AddRange(ctx.MissingFromArticle.Select(item => $"    - {item}"));
        }

        reportLines.Add(ctx.MissingFromFiles.Count == 0
            ? $"  ✅ All {ctx.Sections.Count} article sections have matching tool files"
            : $"  ❌ Missing from files: {ctx.MissingFromFiles.Count}");
        if (ctx.MissingFromFiles.Count > 0)
        {
            reportLines.AddRange(ctx.MissingFromFiles.Select(item => $"    - {item}"));
        }

        foreach (var duplicateIssue in ctx.BlockingIssues.Where(issue => issue.StartsWith("Duplicate ", StringComparison.Ordinal)))
        {
            reportLines.Add($"  ❌ {duplicateIssue}");
        }

        reportLines.Add(string.Empty);
        reportLines.Add("Required params in prompts:");
        var requiredParamsPassingTools = ctx.Sections.Count - ctx.RequiredParamIssues.Count;
        reportLines.Add(ctx.RequiredParamIssues.Count == 0
            ? $"  ✅ {requiredParamsPassingTools}/{ctx.Sections.Count} tools have all required params in examples"
            : $"  ❌ {requiredParamsPassingTools}/{ctx.Sections.Count} tools have all required params in examples");
        reportLines.AddRange(ctx.RequiredParamIssues.Select(issue => $"  {issue}"));

        reportLines.Add(string.Empty);
        var markerWarnings = ctx.WarningIssues.Where(issue => issue.Contains("annotation marker", StringComparison.OrdinalIgnoreCase)).ToArray();
        var totalMarkers = ctx.Sections.Sum(section => section.MarkerCount);
        reportLines.Add($"Annotation markers: {totalMarkers} found (expected {ctx.Sections.Count}) {(markerWarnings.Length == 0 && totalMarkers == ctx.Sections.Count ? "✅" : "⚠️")}");
        reportLines.AddRange(markerWarnings.Select(warning => $"  {warning}"));

        var headerWarnings = ctx.WarningIssues.Where(issue => issue.Contains("example prompt header", StringComparison.OrdinalIgnoreCase)).ToArray();
        var standardHeaderSections = ctx.Sections.Count - headerWarnings.Length;
        reportLines.Add($"Example headers: {standardHeaderSections}/{ctx.Sections.Count} use standard format {(headerWarnings.Length == 0 ? "✅" : "⚠️")}");
        reportLines.AddRange(headerWarnings.Select(warning => $"  {warning}"));

        reportLines.Add($"Branding: {ctx.BrandingIssues.Count} issue{(ctx.BrandingIssues.Count == 1 ? string.Empty : "s")} found {(ctx.BrandingIssues.Count == 0 ? "✅" : "ℹ️")}");
        reportLines.AddRange(ctx.BrandingIssues.Select(issue => $"  - {issue}"));
        reportLines.Add(string.Empty);

        // 6 new post-assembly checks (PRD-QUALITY Item C)
        AppendPassFailSection(reportLines, "Related tools completeness", ctx.PostAssemblyChecks.RelatedToolsIssues);
        AppendCountSection(reportLines, "Tone markers", ctx.PostAssemblyChecks.ToneMarkerWarnings, isBlocking: false);
        AppendCountSection(reportLines, "Boilerplate redundancy", ctx.PostAssemblyChecks.BoilerplateWarnings, isBlocking: false);
        reportLines.Add(ctx.PostAssemblyChecks.HasRelatedSection
            ? "Related section header: ✅ present"
            : "Related section header: ⚠️ absent");
        if (!ctx.PostAssemblyChecks.HasRelatedSection)
        {
            reportLines.Add("  ⚠️ Related section header absent: article is missing a '## See also' or '## Related content' section");
        }
        AppendRatioSection(reportLines, "Tool examples", ctx.PostAssemblyChecks.MissingExampleIssues, ctx.Sections.Count, isBlocking: true, itemDescription: "tools have examples");
        AppendRatioSection(reportLines, "Parameter count", ctx.PostAssemblyChecks.LowParamCountWarnings, ctx.Sections.Count, isBlocking: false, itemDescription: "tools have ≥2 parameters");
        reportLines.Add(string.Empty);

        if (ctx.BlockingIssues.Count == 0)
        {
            reportLines.Add($"RESULT: PASS {(ctx.WarningIssues.Count > 0 || ctx.BrandingIssues.Count > 0 ? $"({ctx.WarningIssues.Count + ctx.BrandingIssues.Count} warning{(ctx.WarningIssues.Count + ctx.BrandingIssues.Count == 1 ? string.Empty : "s")})" : "(clean)")}");
        }
        else
        {
            reportLines.Add($"RESULT: FAIL ({ctx.BlockingIssues.Count} blocking issue{(ctx.BlockingIssues.Count == 1 ? string.Empty : "s")}, {ctx.WarningIssues.Count + ctx.BrandingIssues.Count} warning{(ctx.WarningIssues.Count + ctx.BrandingIssues.Count == 1 ? string.Empty : "s")})");
        }

        return reportLines;
    }

    private static IReadOnlyList<string> BuildFailureReportLines(string familyName, IReadOnlyList<string> blockingIssues)
    {
        var reportLines = new List<string>
        {
            $"=== Tool Family Validation: {familyName} ===",
            "Validation could not complete.",
            string.Empty,
            "Blocking issues:",
        };

        reportLines.AddRange(blockingIssues.Select(issue => $"  - {issue}"));
        reportLines.Add(string.Empty);
        reportLines.Add($"RESULT: FAIL ({blockingIssues.Count} blocking issue{(blockingIssues.Count == 1 ? string.Empty : "s")}, 0 warnings)");
        return reportLines;
    }

    private static IReadOnlyList<string> GetRelatedToolsCompletenessIssues(
        string relatedSectionText,
        IReadOnlyList<ArticleSection> sections,
        IReadOnlyList<NamespaceToolFile> toolFiles)
    {
        // Checks backtick terms in the related section that match THIS family's tool keys.
        // External references (other namespaces, Azure CLI commands, etc.) are typically skipped
        // because they don't match this family's keys, but short or coincidentally matching
        // terms could produce false positives.
        if (string.IsNullOrWhiteSpace(relatedSectionText))
        {
            return Array.Empty<string>();
        }

        var familyToolKeys = toolFiles
            .Select(file => file.ToolKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (familyToolKeys.Count == 0)
        {
            return Array.Empty<string>();
        }

        var backtickTerms = BacktickTermRegex.Matches(relatedSectionText)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(t => t.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (backtickTerms.Length == 0)
        {
            return Array.Empty<string>();
        }

        var issues = new List<string>();
        foreach (var term in backtickTerms)
        {
            var normalizedTerm = ParameterCoverageChecker.ConvertToSlug(term);
            var isInternalTool = familyToolKeys.Any(key => MatchesRelatedToolKey(key, normalizedTerm));
            if (!isInternalTool)
            {
                continue;
            }

            var found = sections.Any(s => MatchesRelatedToolKey(s.ToolKey, normalizedTerm));
            if (!found)
            {
                issues.Add($"🛑 '{term}' is referenced in the related section but has no matching H2 section in this article");
            }
        }

        return issues;
    }

    private static IReadOnlyList<string> GetToneMarkerWarnings(string articleContent)
    {
        var warnings = new List<string>();
        var normalized = articleContent.Replace("\r\n", "\n", StringComparison.Ordinal);
        var inFencedBlock = false;
        var inHtmlCommentBlock = false;
        foreach (var line in normalized.Split('\n'))
        {
            var trimmed = line.Trim();

            // Standard Markdown fenced blocks are supported here.
            // Nested 3-backtick fences inside 4+ backtick fences are not supported and are not expected in generated articles.
            if (FencedCodeDelimiterRegex.IsMatch(trimmed))
            {
                inFencedBlock = !inFencedBlock;
                continue;
            }

            if (inFencedBlock)
            {
                continue;
            }

            if (inHtmlCommentBlock)
            {
                if (trimmed.Contains("-->", StringComparison.Ordinal))
                {
                    inHtmlCommentBlock = false;
                }

                continue;
            }

            if (trimmed.StartsWith("<!--", StringComparison.Ordinal))
            {
                if (!trimmed.Contains("-->", StringComparison.Ordinal))
                {
                    inHtmlCommentBlock = true;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed)
                || trimmed.StartsWith('|')
                || trimmed.StartsWith('#'))
            {
                continue;
            }

            foreach (var phrase in ToneMarkerPhrases)
            {
                if (trimmed.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"⚠️ Tone marker: second-person phrase '{phrase}' found: [{trimmed}]");
                    break;
                }
            }

            var match = ToneMarkerSuperlativeRegex.Match(trimmed);
            if (match.Success)
            {
                warnings.Add($"⚠️ Tone marker: prohibited superlative '{match.Value}' found: [{trimmed}]");
            }
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> GetBoilerplateRedundancyWarnings(IReadOnlyList<ArticleSection> sections)
        => DetectBoilerplateRedundancyWarnings(
            sections.Select(section => (section.Heading, section.DescriptionParagraphs)).ToArray());

    // Minimum word count for a prose paragraph to be considered "boilerplate" — short
    // shared phrases (single clauses, generic labels) are excluded to avoid false positives.
    private const int BoilerplateMinWordCount = 8;

    private static readonly Regex BoilerplateWhitespaceRegex =
        new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ExampleHeaderLineRegex =
        new("^(?i)(example prompts|example commands|usage examples|examples|try this|to .* use commands like)[^:]*:",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OrderedListItemRegex =
        new(@"^\d+\.\s", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Detects copy-pasted boilerplate description prose repeated verbatim across two or
    // more tool sections in the same assembled article (#662). Warn-only and
    // service-agnostic: it compares normalized prose paragraphs by content only, with no
    // knowledge of any specific Azure service, tool, or product name.
    internal static IReadOnlyList<string> DetectBoilerplateRedundancyWarnings(
        IReadOnlyList<(string Heading, IReadOnlyList<string> Paragraphs)> sectionParagraphs)
    {
        var headingsByParagraph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var sampleByParagraph = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (heading, paragraphs) in sectionParagraphs)
        {
            var countedInThisSection = new HashSet<string>(StringComparer.Ordinal);
            foreach (var paragraph in paragraphs)
            {
                var normalized = NormalizeProse(paragraph);
                if (normalized.Length == 0 || CountWords(normalized) < BoilerplateMinWordCount)
                {
                    continue;
                }

                // Count each tool section at most once per distinct paragraph so a section
                // that repeats a line internally isn't mistaken for cross-section redundancy.
                if (!countedInThisSection.Add(normalized))
                {
                    continue;
                }

                if (!headingsByParagraph.TryGetValue(normalized, out var headings))
                {
                    headings = new List<string>();
                    headingsByParagraph[normalized] = headings;
                    sampleByParagraph[normalized] = paragraph.Trim();
                }

                if (!headings.Contains(heading))
                {
                    headings.Add(heading);
                }
            }
        }

        var warnings = new List<string>();
        foreach (var pair in headingsByParagraph)
        {
            var headings = pair.Value;
            if (headings.Count < 2)
            {
                continue;
            }

            var snippet = sampleByParagraph[pair.Key];
            if (snippet.Length > 80)
            {
                snippet = string.Concat(snippet.AsSpan(0, 80), "…");
            }

            warnings.Add(
                $"⚠️ Boilerplate redundancy: identical description repeated across {headings.Count} tool sections ({string.Join(", ", headings)}): [{snippet}]");
        }

        return warnings;
    }

    // Extracts the natural-language description paragraphs from a tool section's lines,
    // excluding structural elements (headings, tab markers, HTML/marker comments, tables,
    // list items, code fences, includes, blockquotes, horizontal rules, example headers).
    // Consecutive prose lines separated by blank/structural lines form distinct paragraphs.
    internal static IReadOnlyList<string> ExtractDescriptionParagraphs(IReadOnlyList<string> sectionLines)
    {
        var paragraphs = new List<string>();
        var current = new List<string>();

        void Flush()
        {
            if (current.Count > 0)
            {
                paragraphs.Add(string.Join(" ", current));
                current.Clear();
            }
        }

        // Start at index 1 to skip the section's own "## Heading" line.
        for (var i = 1; i < sectionLines.Count; i++)
        {
            var trimmed = sectionLines[i].Trim();
            if (trimmed.Length == 0 || !IsProseLine(trimmed))
            {
                Flush();
                continue;
            }

            current.Add(trimmed);
        }

        Flush();
        return paragraphs;
    }

    private static bool IsProseLine(string trimmed)
    {
        if (trimmed.StartsWith("<!--", StringComparison.Ordinal)
            || trimmed.StartsWith('|')
            || trimmed.StartsWith('#')
            || trimmed.StartsWith('>')
            || trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith("---", StringComparison.Ordinal)
            || trimmed.StartsWith("[!INCLUDE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("- ", StringComparison.Ordinal)
            || trimmed.StartsWith("* ", StringComparison.Ordinal)
            || trimmed.StartsWith("+ ", StringComparison.Ordinal)
            || OrderedListItemRegex.IsMatch(trimmed)
            || ExampleHeaderLineRegex.IsMatch(trimmed))
        {
            return false;
        }

        return true;
    }

    private static string NormalizeProse(string paragraph)
        => BoilerplateWhitespaceRegex.Replace(paragraph, " ").Trim().ToLowerInvariant();

    private static int CountWords(string normalized)
        => normalized.Length == 0
            ? 0
            : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private static IReadOnlyList<string> GetRelatedSectionHeaderWarnings(bool hasRelatedSection)
        => hasRelatedSection
            ? Array.Empty<string>()
            : new[] { "⚠️ Related section header absent: article is missing a '## See also' or '## Related content' section" };

    private static IReadOnlyList<string> GetMissingExampleIssues(IReadOnlyList<ArticleSection> sections)
    {
        var issues = new List<string>();
        foreach (var section in sections)
        {
            if (section.ExampleHeaderIndex < 0 && section.AlternateExampleHeader is null)
            {
                issues.Add($"🛑 {section.ToolKey}: no example prompt header found (section requires 'Example prompts include:' or recognized alternate)");
            }
        }

        return issues;
    }

    private static IReadOnlyList<string> GetLowParameterCountWarnings(IReadOnlyList<ArticleSection> sections)
    {
        var warnings = new List<string>();
        foreach (var section in sections)
        {
            if (section.TotalParameterCount < 2)
            {
                warnings.Add($"⚠️ {section.ToolKey}: only {section.TotalParameterCount} documented parameter(s) (expected ≥2)");
            }
        }

        return warnings;
    }

    private static bool MatchesRelatedToolKey(string candidateKey, string normalizedTerm)
        => string.Equals(candidateKey, normalizedTerm, StringComparison.OrdinalIgnoreCase)
            || normalizedTerm.EndsWith("-" + candidateKey, StringComparison.OrdinalIgnoreCase)
            || candidateKey.EndsWith("-" + normalizedTerm, StringComparison.OrdinalIgnoreCase);


    private static async Task WriteReportAsync(string reportDirectory, string reportPath, IReadOnlyList<string> reportLines, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllLinesAsync(reportPath, reportLines, cancellationToken);
    }

    private static string NormalizeToolCommand(string command)
        => SourceVerificationHelpers.NormalizeToolCommand(command);

    private static IReadOnlyList<string> GetMcpCliCommands(string text)
        => McpCliRegex.Matches(text.Replace("\r\n", "\n", StringComparison.Ordinal))
            .Select(match => match.Groups[1].Value.Trim())
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .ToArray();

    private static string ConvertCommandToToolKey(string commandText, string namespaceName)
    {
        var normalized = NormalizeToolCommand(commandText).Trim().ToLowerInvariant();
        var namespacePrefix = $"{namespaceName.ToLowerInvariant()} ";
        if (normalized.StartsWith(namespacePrefix, StringComparison.Ordinal))
        {
            normalized = normalized[namespacePrefix.Length..];
        }

        normalized = Regex.Replace(normalized, "[\\s_]+", "-");
        normalized = Regex.Replace(normalized, "[^a-z0-9\\-]+", "-");
        return normalized.Trim('-');
    }

    private sealed record NamespaceToolFile(string Name, string FullName, string ToolKey, string? CommandText);



    private sealed record ParameterRow(string ParameterName, string RequiredValue, bool IsRequired);

    private sealed record ArticleSection(
        string Heading,
        string ToolKey,
        IReadOnlyList<string> Commands,
        int MarkerCount,
        IReadOnlyList<int> MarkerLineIndices,
        int ExampleHeaderIndex,
        int TableStartIndex,
        string? AlternateExampleHeader,
        IReadOnlyList<string> ExamplePrompts,
        IReadOnlyList<string> RequiredParameters,
        int TotalParameterCount,
        IReadOnlyList<ParameterRow> ParameterRows,
        IReadOnlyList<string> DescriptionParagraphs);

    private sealed record ParsedArticle(
        string Frontmatter,
        IReadOnlyList<ArticleSection> Sections,
        bool HasRelatedSection,
        string RelatedSectionText);

    private sealed record PostAssemblyCheckSummary(
        IReadOnlyList<string> RelatedToolsIssues,
        IReadOnlyList<string> ToneMarkerWarnings,
        IReadOnlyList<string> BoilerplateWarnings,
        bool HasRelatedSection,
        IReadOnlyList<string> MissingExampleIssues,
        IReadOnlyList<string> LowParamCountWarnings);

    private sealed record ValidatorReportContext(
        string FamilyName,
        IReadOnlyList<NamespaceToolFile> ToolFiles,
        IReadOnlyList<ArticleSection> Sections,
        int? FrontmatterToolCount,
        IReadOnlyList<string> MissingFromArticle,
        IReadOnlyList<string> MissingFromFiles,
        IReadOnlyList<string> BlockingIssues,
        IReadOnlyList<string> WarningIssues,
        IReadOnlyList<string> RequiredParamIssues,
        IReadOnlyList<string> BrandingIssues,
        PostAssemblyCheckSummary PostAssemblyChecks);

    private sealed record BrandingRule(string PatternText, string Message)
    {
        public Regex Pattern { get; } = new(PatternText, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
