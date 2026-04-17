// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using ToolMetadataExtractor.Models;

namespace ToolMetadataExtractor.Services;

/// <summary>
/// Service for extracting tool metadata from C# files
/// </summary>
public class MetadataExtractorService
{
    private readonly ILogger<MetadataExtractorService> _logger;
    private readonly string _repoRoot;

    public MetadataExtractorService(ILogger<MetadataExtractorService> logger, string repoRoot)
    {
        _logger = logger;
        _repoRoot = repoRoot;
    }

    /// <summary>
    /// Extract tool metadata from a list of tool paths
    /// </summary>
    /// <param name="toolPaths">List of tool paths to process</param>
    /// <returns>List of extracted tool metadata information</returns>
    public async Task<List<ToolMetadataInfo>> ExtractToolMetadataAsync(IEnumerable<string> toolPaths)
    {
        var results = new List<ToolMetadataInfo>();
        
        foreach (var toolPath in toolPaths)
        {
            try
            {
                _logger.LogInformation("Processing tool: {ToolPath}", toolPath);
                var sourcePath = await FindToolSourceFileAsync(toolPath);
                
                if (string.IsNullOrEmpty(sourcePath))
                {
                    _logger.LogWarning("Could not find source file for tool: {ToolPath}", toolPath);
                    continue;
                }

                var metadata = await ExtractMetadataFromSourceAsync(sourcePath, toolPath);
                if (metadata != null)
                {
                    results.Add(metadata);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tool {ToolPath}", toolPath);
            }
        }
        
        return results;
    }

    /// <summary>
    /// Find the source file for a given tool path
    /// </summary>
    /// <param name="toolPath">The tool path to find (e.g. "storage account list")</param>
    /// <returns>The path to the source file or null if not found</returns>
    private async Task<string> FindToolSourceFileAsync(string toolPath)
    {
        var parts = toolPath.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1)
        {
            return string.Empty;
        }
        
        // Special handling for known tools with unique patterns
        if (toolPath.Equals("storage account list", StringComparison.OrdinalIgnoreCase))
        {
            var specificPath = Path.Combine(_repoRoot, "tools/Azure.Mcp.Tools.Storage/src/Commands/Account/StorageAccountListCommand.cs");
            if (File.Exists(specificPath))
            {
                _logger.LogInformation("Found file using special pattern match: {File}", specificPath);
                return specificPath;
            }
        }
        
        // Generate possible class names based on different naming conventions
        var possibleClassNames = new List<string>();
        
        // Standard naming convention: "storage account list" -> "StorageAccountListCommand"
        var standardName = string.Join("", parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant())) + "Command";
        possibleClassNames.Add(standardName);
        
        // Service-specific naming convention: "storage account list" -> "StorageAccountListCommand"
        if (parts.Length >= 2)
        {
            var servicePrefix = char.ToUpperInvariant(parts[0][0]) + parts[0][1..].ToLowerInvariant();
            var entityName = char.ToUpperInvariant(parts[1][0]) + parts[1][1..].ToLowerInvariant();
            if (parts.Length >= 3)
            {
                var actionName = char.ToUpperInvariant(parts[2][0]) + parts[2][1..].ToLowerInvariant();
                possibleClassNames.Add($"{servicePrefix}{entityName}{actionName}Command");
            }
        }
        
        // KeyVault style convention: "keyvault secret create" -> "SecretCreateCommand"
        if (parts.Length >= 3)
        {
            var entityName = char.ToUpperInvariant(parts[1][0]) + parts[1][1..].ToLowerInvariant();
            var actionName = char.ToUpperInvariant(parts[2][0]) + parts[2][1..].ToLowerInvariant();
            possibleClassNames.Add($"{entityName}{actionName}Command");
        }
        
        _logger.LogInformation("Looking for classes: {ClassNames}", string.Join(", ", possibleClassNames));
        
        // Find possible tool directories
        var possibleDirectories = new List<string>();
        var primaryFolder = parts[0];
        
        if (!string.IsNullOrEmpty(primaryFolder))
        {
            // Check for both singular and plural forms
            var toolsDir = Path.Combine(_repoRoot, "tools");
            var primaryUpperFirst = char.ToUpperInvariant(primaryFolder[0]) + primaryFolder[1..].ToLowerInvariant();
            
            // Check for Azure.Mcp.Tools.X format
            foreach (var dir in Directory.GetDirectories(toolsDir))
            {
                var dirName = Path.GetFileName(dir);
                // Case-insensitive check
                if (dirName.Contains(primaryUpperFirst, StringComparison.OrdinalIgnoreCase) ||
                    dirName.Contains(primaryFolder, StringComparison.OrdinalIgnoreCase))
                {
                    possibleDirectories.Add(dir);
                    _logger.LogInformation("Found potential directory: {Directory}", dir);
                }
            }
        }
        
        // Search in possible directories first
        if (possibleDirectories.Count > 0)
        {
            foreach (var dir in possibleDirectories)
            {
                var commandsDir = Path.Combine(dir, "src", "Commands");
                
                if (Directory.Exists(commandsDir))
                {
                    var allCsFiles = Directory.GetFiles(commandsDir, "*.cs", SearchOption.AllDirectories);
                    _logger.LogInformation("Found {Count} .cs files in {CommandsDir}", allCsFiles.Length, commandsDir);
                    
                    // Try second-level entity directory match (for KeyVault style)
                    if (parts.Length >= 2)
                    {
                        var entityDir = Path.Combine(commandsDir, char.ToUpperInvariant(parts[1][0]) + parts[1][1..].ToLowerInvariant());
                        
                        if (Directory.Exists(entityDir))
                        {
                            _logger.LogInformation("Found entity directory: {EntityDir}", entityDir);
                            var entityFiles = Directory.GetFiles(entityDir, "*.cs", SearchOption.AllDirectories);
                            
                            foreach (var file in entityFiles)
                            {
                                var fileName = Path.GetFileName(file);
                                _logger.LogInformation("Checking file: {FileName}", fileName);
                                
                                foreach (var className in possibleClassNames)
                                {
                                    if (fileName.Equals($"{className}.cs", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.LogInformation("Found file by name match: {File}", file);
                                        return file;
                                    }
                                }
                                
                                var content = await File.ReadAllTextAsync(file);
                                foreach (var className in possibleClassNames)
                                {
                                    if (content.Contains($"class {className}") || 
                                        content.Contains($"class {className}(") ||
                                        content.Contains($"sealed class {className}") || 
                                        content.Contains($"sealed class {className}("))
                                    {
                                        _logger.LogInformation("Found file by content match: {File}", file);
                                        return file;
                                    }
                                }
                            }
                        }
                    }
                    
                    // General search in the commands directory
                    foreach (var file in allCsFiles)
                    {
                        var content = await File.ReadAllTextAsync(file);
                        foreach (var className in possibleClassNames)
                        {
                            if (content.Contains($"class {className}") || 
                                content.Contains($"class {className}(") ||
                                content.Contains($"sealed class {className}") || 
                                content.Contains($"sealed class {className}("))
                            {
                                _logger.LogInformation("Found file by content match: {File}", file);
                                return file;
                            }
                        }
                    }
                }
            }
        }
        
        // Last resort: Search all .cs files
        _logger.LogInformation("Fallback to searching all .cs files");
        var toolsBaseDir = Path.Combine(_repoRoot, "tools");
        var allCsFilesBaseSearch = Directory.GetFiles(toolsBaseDir, "*.cs", SearchOption.AllDirectories);
        
        foreach (var file in allCsFilesBaseSearch)
        {
            var content = await File.ReadAllTextAsync(file);
            foreach (var className in possibleClassNames)
            {
                if (content.Contains($"class {className}") || 
                    content.Contains($"class {className}(") ||
                    content.Contains($"sealed class {className}") || 
                    content.Contains($"sealed class {className}("))
                {
                    _logger.LogInformation("Found file by content match: {File}", file);
                    return file;
                }
            }
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Extract metadata from a source file
    /// </summary>
    /// <param name="sourcePath">Path to the source file</param>
    /// <param name="toolPath">The tool path</param>
    /// <returns>Extracted tool metadata or null if not found</returns>
    private async Task<ToolMetadataInfo?> ExtractMetadataFromSourceAsync(string sourcePath, string toolPath)
    {
        var sourceText = await File.ReadAllTextAsync(sourcePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var root = await syntaxTree.GetRootAsync();

        // Find the class declaration
        var classDeclaration = root
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text.EndsWith("Command"));

        if (classDeclaration == null)
        {
            _logger.LogWarning("Could not find command class in {SourcePath}", sourcePath);
            return null;
        }

        var result = new ToolMetadataInfo
        {
            ToolPath = toolPath,
            SourceFile = sourcePath
        };

        // Extract title using regex first (more reliable for CommandTitle pattern)
        var titleRegex = new Regex(@"private\s+(?:const|readonly)\s+string\s+CommandTitle\s*=\s*""([^""]+)"";");
        var titleMatch = titleRegex.Match(sourceText);
        if (titleMatch.Success)
        {
            result.Title = titleMatch.Groups[1].Value;
            _logger.LogInformation("Found title from CommandTitle constant: {Title}", result.Title);
        }

        // Find the Metadata property
        var metadataProperty = classDeclaration
            .DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.Text == "Metadata");

        if (metadataProperty == null)
        {
            _logger.LogWarning("Could not find Metadata property in {SourcePath}", sourcePath);
            return result;
        }

        // Find title and description properties if not already found
        if (result.Title == null)
        {
            var titleProperty = classDeclaration
                .DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.Identifier.Text == "Title");

            if (titleProperty != null)
            {
                result.Title = ExtractPropertyStringValue(titleProperty);
            }
        }

        // Find description property
        var descriptionProperty = classDeclaration
            .DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.Text == "Description");

        if (descriptionProperty != null)
        {
            result.Description = ExtractPropertyStringValue(descriptionProperty);
        }

        // Extract metadata from the property initializer
        var initializer = metadataProperty
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .FirstOrDefault();

        if (initializer == null)
        {
            _logger.LogWarning("Could not find ToolMetadata initializer in {SourcePath}", sourcePath);
            
            // Fallback to direct string parsing - useful for certain formats like in SecretCreateCommand.cs
            var sourceLines = sourceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var metadataStartLine = -1;
            var metadataEndLine = -1;
            
            for (int i = 0; i < sourceLines.Length; i++)
            {
                if (sourceLines[i].Contains("public override ToolMetadata Metadata => new()"))
                {
                    metadataStartLine = i;
                }
                else if (metadataStartLine >= 0 && sourceLines[i].Contains("};"))
                {
                    metadataEndLine = i;
                    break;
                }
            }
            
            if (metadataStartLine >= 0 && metadataEndLine > metadataStartLine)
            {
                _logger.LogInformation("Using fallback metadata extraction for {SourcePath}", sourcePath);
                for (int i = metadataStartLine + 1; i < metadataEndLine; i++)
                {
                    var line = sourceLines[i].Trim();
                    if (line.Contains("="))
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var propName = parts[0].Trim();
                            var propValue = parts[1].Trim().TrimEnd(',').Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                            result.Metadata[propName] = propValue;
                        }
                    }
                }
            }
            
            return result;
        }

        // Get all initializer expressions
        var initializerExpressions = initializer
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>();

        foreach (var expression in initializerExpressions)
        {
            var propertyName = expression.Left.ToString();
            var value = expression.Right.ToString().ToLowerInvariant() == "true";
            result.Metadata[propertyName] = value;
        }

        return result;
    }

    /// <summary>
    /// Extract string value from a property declaration
    /// </summary>
    private string? ExtractPropertyStringValue(PropertyDeclarationSyntax property)
    {
        var returnExpression = property
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .FirstOrDefault();

        if (returnExpression != null)
        {
            var value = returnExpression.Expression?.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                return CleanStringLiteral(value);
            }
        }

        var arrowExpression = property
            .ExpressionBody?
            .Expression
            .ToString();

        if (!string.IsNullOrEmpty(arrowExpression))
        {
            return CleanStringLiteral(arrowExpression);
        }
        
        // Check for field reference (e.g., CommandTitle)
        var sourceFile = property.SyntaxTree.FilePath;
        if (!string.IsNullOrEmpty(sourceFile) && File.Exists(sourceFile))
        {
            var sourceText = File.ReadAllText(sourceFile);
            var propertyName = property.Identifier.Text;
            
            // Find the field or constant with the same name as the property
            var fieldPattern = $"private const string {propertyName} = @?\"([^\"]+)\"|private const string {propertyName} = @?\"\"\"([^\"\"\"]+)\"\"\"|private readonly string {propertyName} = @?\"([^\"]+)\"|private readonly string {propertyName} = @?\"\"\"([^\"\"\"]+)\"\"\"";
            var match = Regex.Match(sourceText, fieldPattern);
            
            if (match.Success)
            {
                // Return the first non-empty group (the captured string)
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    if (!string.IsNullOrEmpty(match.Groups[i].Value))
                    {
                        return match.Groups[i].Value;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Clean string literals of quotes and escape sequences
    /// </summary>
    private string CleanStringLiteral(string literal)
    {
        // Handle verbatim string literals with triple quotes
        if (literal.StartsWith("\"\"\"") && literal.EndsWith("\"\"\""))
        {
            return literal.Substring(3, literal.Length - 6).Trim();
        }
        
        // Handle regular string literals with quotes
        if (literal.StartsWith("\"") && literal.EndsWith("\""))
        {
            return literal.Substring(1, literal.Length - 2)
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
        }
        
        return literal;
    }
}