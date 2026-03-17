using System.Text;
using System.Text.RegularExpressions;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using Shared;

namespace PipelineRunner.Validation;

public sealed class ToolFamilyPostAssemblyValidator : IPostValidator
{
    public const string FamilyNameContextKey = "ToolFamilyCleanup.FamilyName";

    private static readonly Regex FrontmatterRegex = new(@"^---\s*\n(.*?)\n---\s*\n?", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HeadingRegex = new(@"(?m)^##\s+(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex McpCliRegex = new(@"(?m)^<!--\s*@mcpcli\s+(.+?)\s*-->$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly BrandingRule[] BrandingRules =
    [
        new(@"\bthis command\b", "Use \"this tool\" instead of \"this command\"."),
        new(@"\bCosmosDB\b", "Use \"Azure Cosmos DB\" on first mention instead of \"CosmosDB\"."),
        new(@"\bAzure VMs\b", "Use \"Azure Virtual Machines\" on first mention instead of \"Azure VMs\"."),
        new(@"\bMSSQL\b", "Use \"Azure SQL\" or \"SQL Server\" as appropriate instead of \"MSSQL\"."),
        new(@"\bFoundry\b", "Verify first mention uses the full product name (for example, \"Microsoft Foundry\")."),
    ];

    public string Name => "ToolFamilyPostAssemblyValidator";

    public async ValueTask<ValidatorResult> ValidateAsync(PipelineContext context, IPipelineStep step, CancellationToken cancellationToken)
    {
        var familyName = ResolveFamilyName(context);
        var toolsDirectory = Path.Combine(context.OutputPath, "tools");
        var articlePath = Path.Combine(context.OutputPath, "tool-family", $"{familyName}.md");
        var reportDirectory = Path.Combine(context.OutputPath, "reports");
        var reportPath = Path.Combine(reportDirectory, $"tool-family-validation-{familyName}.txt");

        var blockingIssues = new List<string>();
        var warningIssues = new List<string>();
        var brandingIssues = new List<string>();
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
                var prefixes = await GetNamespaceFilePrefixesAsync(familyName, cancellationToken);
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

                if (toolFileCount != articleSectionCount || frontmatterToolCount is null || frontmatterToolCount != toolFileCount)
                {
                    blockingIssues.Add("Tool count integrity check failed.");
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
                    blockingIssues.Add("Cross-reference check failed.");
                }

                foreach (var warning in GetRequiredParameterWarnings(sections))
                {
                    warningIssues.Add(warning);
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

                var reportLines = BuildReportLines(
                    familyName,
                    toolFiles,
                    sections,
                    frontmatterToolCount,
                    missingFromArticle,
                    missingFromFiles,
                    blockingIssues,
                    warningIssues,
                    brandingIssues);

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

    private static async Task<IReadOnlyList<string>> GetNamespaceFilePrefixesAsync(string namespaceName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prefixes = new List<string> { namespaceName.ToLowerInvariant() };
        var brandMappings = await DataFileLoader.LoadBrandMappingsAsync();
        if (brandMappings.TryGetValue(namespaceName, out var mapping) && !string.IsNullOrWhiteSpace(mapping.FileName))
        {
            var mappedPrefix = mapping.FileName.Trim().ToLowerInvariant();
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

        for (var index = 0; index < headingMatches.Count; index++)
        {
            var heading = headingMatches[index].Groups[1].Value.Trim();
            if (string.Equals(heading, "Related content", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var startIndex = headingMatches[index].Index;
            var endIndex = index < headingMatches.Count - 1 ? headingMatches[index + 1].Index : body.Length;
            var sectionText = body.Substring(startIndex, endIndex - startIndex).TrimEnd();
            var sectionLines = sectionText.Split('\n');
            var commands = GetMcpCliCommands(sectionText);
            var toolKey = commands.Count > 0
                ? ConvertCommandToToolKey(commands[0], namespaceName)
                : ConvertToSlug(heading);

            var markerLineIndices = new List<int>();
            var exampleHeaderIndex = -1;
            var tableStartIndex = -1;
            string? alternateExampleHeader = null;

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
                }
            }

            var examplePrompts = ExtractExamplePrompts(sectionLines, exampleHeaderIndex);
            var requiredParameters = GetSectionParameterRows(sectionLines)
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
                requiredParameters));
        }

        return new ParsedArticle(frontmatter, sections);
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
        var tableLines = new List<string>();
        var tableStarted = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('|'))
            {
                tableStarted = true;
                tableLines.Add(trimmed);
                continue;
            }

            if (tableStarted)
            {
                break;
            }
        }

        if (tableLines.Count < 2)
        {
            return Array.Empty<ParameterRow>();
        }

        var headerCells = ConvertTableLineToCells(tableLines[0]);
        var parameterIndex = -1;
        var requiredIndex = -1;
        for (var index = 0; index < headerCells.Count; index++)
        {
            var header = RemoveMarkup(headerCells[index]);
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
            return Array.Empty<ParameterRow>();
        }

        var rows = new List<ParameterRow>();
        foreach (var line in tableLines.Skip(1))
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

            var parameterName = RemoveMarkup(cells[parameterIndex]);
            var requiredValue = RemoveMarkup(cells[requiredIndex]);
            var isRequired = Regex.IsMatch(requiredValue, "(?i)^(yes|✅|required\\*?)$") || Regex.IsMatch(requiredValue, "(?i)^required");
            rows.Add(new ParameterRow(parameterName, requiredValue, isRequired));
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

    private static IReadOnlyList<string> GetRequiredParameterWarnings(IEnumerable<ArticleSection> sections)
    {
        var warnings = new List<string>();
        foreach (var section in sections)
        {
            if (section.RequiredParameters.Count == 0)
            {
                continue;
            }

            var missingParameters = new List<string>();
            foreach (var requiredParameter in section.RequiredParameters)
            {
                var coverage = GetConcretePromptCoverage(section.ExamplePrompts, requiredParameter, section.RequiredParameters.Count);
                if (!coverage.Covered && !coverage.PlaceholderDetected)
                {
                    missingParameters.Add(requiredParameter);
                }
            }

            if (missingParameters.Count > 0)
            {
                var suffix = missingParameters.Count > 1 ? "s" : string.Empty;
                var joined = string.Join(", ", missingParameters.Select(parameter => $"'{parameter}'"));
                warnings.Add($"⚠️ {section.ToolKey}: missing {joined} in example prompt{suffix}");
            }
        }

        return warnings;
    }

    private static PromptCoverage GetConcretePromptCoverage(IReadOnlyList<string> examplePrompts, string parameterName, int totalRequiredParameters)
    {
        var slug = ConvertToSlug(parameterName);
        var words = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var wordPattern = string.Join("[-_ ]+", words.Select(Regex.Escape));
        var variantList = new List<string>();

        foreach (var variant in new[]
        {
            parameterName.ToLowerInvariant(),
            string.Join(' ', words),
            string.Join('-', words),
            string.Join('_', words),
        })
        {
            if (!string.IsNullOrWhiteSpace(variant))
            {
                variantList.Add(variant);
            }
        }

        if (words.Length > 1 && new[] { "name", "text", "array", "value" }.Contains(words[^1], StringComparer.Ordinal))
        {
            var baseWords = words[..^1];
            foreach (var variant in new[]
            {
                string.Join(' ', baseWords),
                string.Join('-', baseWords),
                string.Join('_', baseWords),
            })
            {
                if (!string.IsNullOrWhiteSpace(variant))
                {
                    variantList.Add(variant);
                }
            }
        }

        if (words.Length > 0 && string.Equals(words[^1], "text", StringComparison.Ordinal))
        {
            variantList.Add("text");
        }

        if (words.Length > 0 && string.Equals(words[^1], "array", StringComparison.Ordinal))
        {
            variantList.Add(words[0]);
            variantList.Add($"{words[0]}s");
        }

        var variants = variantList
            .Where(variant => !string.IsNullOrWhiteSpace(variant))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var placeholderDetected = false;
        var covered = false;
        foreach (var examplePrompt in examplePrompts)
        {
            if (string.IsNullOrWhiteSpace(examplePrompt))
            {
                continue;
            }

            var trimmedPrompt = examplePrompt.Trim();
            var lowerPrompt = trimmedPrompt.ToLowerInvariant();
            var placeholders = Regex.Matches(trimmedPrompt, "<[^>]+>|\\{[^}]+\\}|\\[[^\\]]+\\]|`[^`]+`")
                .Select(match => match.Value)
                .ToArray();

            foreach (var placeholder in placeholders)
            {
                // Strip outer bracket pair (<…>, {…}, […]) so ConvertToSlug sees the inner text
                var inner = placeholder.Length >= 2 ? placeholder[1..^1] : placeholder;
                // Strip any remaining nested delimiters (handles double-wrapped like `<account>`)
                inner = inner.TrimStart('<', '{', '[').TrimEnd('>', '}', ']');
                var placeholderSlug = ConvertToSlug(inner);
                if (placeholderSlug == slug
                    || placeholderSlug.Contains(slug, StringComparison.Ordinal)
                    || words.Count(word => placeholderSlug.Contains(word, StringComparison.Ordinal)) >= Math.Min(Math.Max(words.Length, 1), 2))
                {
                    placeholderDetected = true;
                }
            }

            var foundVariant = false;
            var matchIndex = -1;
            foreach (var variant in variants)
            {
                var currentIndex = lowerPrompt.IndexOf(variant.ToLowerInvariant(), StringComparison.Ordinal);
                if (currentIndex >= 0)
                {
                    foundVariant = true;
                    matchIndex = currentIndex + variant.Length;
                    break;
                }
            }

            if (!foundVariant && !string.IsNullOrWhiteSpace(wordPattern))
            {
                var wordMatch = Regex.Match(lowerPrompt, $"(?i)\\b{wordPattern}\\b");
                if (wordMatch.Success)
                {
                    foundVariant = true;
                    matchIndex = wordMatch.Index + wordMatch.Length;
                }
            }

            if (foundVariant && matchIndex >= 0)
            {
                var tail = trimmedPrompt[Math.Min(matchIndex, trimmedPrompt.Length)..];
                if (Regex.IsMatch(tail, "^\\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\\s*'[^'<>{}\\[\\]]+'")
                    || Regex.IsMatch(tail, "^\\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\\s*`[^`<>{}\\[\\]]+`")
                    || Regex.IsMatch(tail, "^\\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\\s*https?://\\S+")
                    || Regex.IsMatch(tail, "^\\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\\s*\\[(?!\\s*[<\\{]).+\\]")
                    || Regex.IsMatch(tail, "^\\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\\s*\\{(?!\\s*[<\\{]).+\\}"))
                {
                    covered = true;
                    break;
                }
            }

            if (!covered && totalRequiredParameters == 1 && placeholders.Length == 0)
            {
                if (Regex.IsMatch(trimmedPrompt, "'[^'<>{}\\[\\]]+'")
                    || Regex.IsMatch(trimmedPrompt, "`[^`<>{}\\[\\]]+`")
                    || Regex.IsMatch(trimmedPrompt, "https?://\\S+"))
                {
                    covered = true;
                    break;
                }
            }
        }

        return new PromptCoverage(covered, placeholderDetected);
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

    private static IReadOnlyList<string> BuildReportLines(
        string familyName,
        IReadOnlyList<NamespaceToolFile> toolFiles,
        IReadOnlyList<ArticleSection> sections,
        int? frontmatterToolCount,
        IReadOnlyList<string> missingFromArticle,
        IReadOnlyList<string> missingFromFiles,
        IReadOnlyList<string> blockingIssues,
        IReadOnlyList<string> warningIssues,
        IReadOnlyList<string> brandingIssues)
    {
        var reportLines = new List<string>
        {
            $"=== Tool Family Validation: {familyName} ===",
            $"Tool files found: {toolFiles.Count}",
            $"Article H2 sections: {sections.Count}",
            $"Frontmatter tool_count: {(frontmatterToolCount is not null ? frontmatterToolCount.Value : "missing")}",
            toolFiles.Count == sections.Count && frontmatterToolCount == toolFiles.Count
                ? "✅ Tool count integrity: PASS"
                : "❌ Tool count integrity: FAIL",
            string.Empty,
            "Cross-reference:",
            missingFromArticle.Count == 0
                ? $"  ✅ All {toolFiles.Count} tool files have matching article sections"
                : $"  ❌ Missing from article: {missingFromArticle.Count}",
        };

        if (missingFromArticle.Count > 0)
        {
            reportLines.AddRange(missingFromArticle.Select(item => $"    - {item}"));
        }

        reportLines.Add(missingFromFiles.Count == 0
            ? $"  ✅ All {sections.Count} article sections have matching tool files"
            : $"  ❌ Missing from files: {missingFromFiles.Count}");
        if (missingFromFiles.Count > 0)
        {
            reportLines.AddRange(missingFromFiles.Select(item => $"    - {item}"));
        }

        foreach (var duplicateIssue in blockingIssues.Where(issue => issue.StartsWith("Duplicate ", StringComparison.Ordinal)))
        {
            reportLines.Add($"  ❌ {duplicateIssue}");
        }

        reportLines.Add(string.Empty);
        reportLines.Add("Required params in prompts:");
        var requiredParamWarnings = warningIssues.Where(issue => issue.Contains("missing", StringComparison.OrdinalIgnoreCase)).ToArray();
        var requiredParamsPassingTools = sections.Count - requiredParamWarnings.Length;
        reportLines.Add(requiredParamWarnings.Length == 0
            ? $"  ✅ {requiredParamsPassingTools}/{sections.Count} tools have all required params in examples"
            : $"  ⚠️ {requiredParamsPassingTools}/{sections.Count} tools have all required params in examples");
        reportLines.AddRange(requiredParamWarnings.Select(warning => $"  {warning}"));

        reportLines.Add(string.Empty);
        var markerWarnings = warningIssues.Where(issue => issue.Contains("annotation marker", StringComparison.OrdinalIgnoreCase)).ToArray();
        var totalMarkers = sections.Sum(section => section.MarkerCount);
        reportLines.Add($"Annotation markers: {totalMarkers} found (expected {sections.Count}) {(markerWarnings.Length == 0 && totalMarkers == sections.Count ? "✅" : "⚠️")}");
        reportLines.AddRange(markerWarnings.Select(warning => $"  {warning}"));

        var headerWarnings = warningIssues.Where(issue => issue.Contains("example prompt header", StringComparison.OrdinalIgnoreCase)).ToArray();
        var standardHeaderSections = sections.Count - headerWarnings.Length;
        reportLines.Add($"Example headers: {standardHeaderSections}/{sections.Count} use standard format {(headerWarnings.Length == 0 ? "✅" : "⚠️")}");
        reportLines.AddRange(headerWarnings.Select(warning => $"  {warning}"));

        reportLines.Add($"Branding: {brandingIssues.Count} issue{(brandingIssues.Count == 1 ? string.Empty : "s")} found {(brandingIssues.Count == 0 ? "✅" : "ℹ️")}");
        reportLines.AddRange(brandingIssues.Select(issue => $"  - {issue}"));
        reportLines.Add(string.Empty);

        if (blockingIssues.Count == 0)
        {
            reportLines.Add($"RESULT: PASS {(warningIssues.Count > 0 || brandingIssues.Count > 0 ? $"({warningIssues.Count + brandingIssues.Count} warning{(warningIssues.Count + brandingIssues.Count == 1 ? string.Empty : "s")})" : "(clean)")}");
        }
        else
        {
            reportLines.Add($"RESULT: FAIL ({blockingIssues.Count} blocking issue{(blockingIssues.Count == 1 ? string.Empty : "s")}, {warningIssues.Count + brandingIssues.Count} warning{(warningIssues.Count + brandingIssues.Count == 1 ? string.Empty : "s")})");
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

    private static async Task WriteReportAsync(string reportDirectory, string reportPath, IReadOnlyList<string> reportLines, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllLinesAsync(reportPath, reportLines, cancellationToken);
    }

    private static string NormalizeToolCommand(string command)
        => string.IsNullOrWhiteSpace(command)
            ? command
            : command.Replace("\r", string.Empty, StringComparison.Ordinal).Trim().Replace('_', ' ');

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

    private static string ConvertToSlug(string text)
    {
        var clean = RemoveMarkup(text);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return string.Empty;
        }

        var slug = Regex.Replace(clean.ToLowerInvariant(), "[^a-z0-9]+", "-");
        return slug.Trim('-');
    }

    private static string RemoveMarkup(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var clean = text.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal);
        clean = Regex.Replace(clean, "<[^>]+>", string.Empty);
        clean = Regex.Replace(clean, "\\s+", " ");
        return clean.Trim();
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
        IReadOnlyList<string> RequiredParameters);

    private sealed record ParsedArticle(string Frontmatter, IReadOnlyList<ArticleSection> Sections);

    private sealed record PromptCoverage(bool Covered, bool PlaceholderDetected);

    private sealed record BrandingRule(string PatternText, string Message)
    {
        public Regex Pattern { get; } = new(PatternText, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
