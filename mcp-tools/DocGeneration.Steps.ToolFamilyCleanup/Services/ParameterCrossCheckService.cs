// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Shared;
using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Removes parameter rows that were hallucinated downstream and do not exist in the
/// Step 1 parameter manifest for the tool.
/// </summary>
internal sealed class ParameterCrossCheckService
{
    public async Task<IReadOnlyList<ToolContent>> StripHallucinatedParametersAsync(
        IReadOnlyList<ToolContent> tools,
        string parameterManifestDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(parameterManifestDirectory))
        {
            return tools;
        }

        var nameContext = await FileNameContext.CreateAsync();
        var rewrittenTools = new List<ToolContent>(tools.Count);

        foreach (var tool in tools)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(tool.Command))
            {
                rewrittenTools.Add(tool);
                continue;
            }

            var manifestPath = Path.Combine(
                parameterManifestDirectory,
                ToolFileNameBuilder.BuildParameterManifestFileName(tool.Command, nameContext));
            if (!File.Exists(manifestPath))
            {
                rewrittenTools.Add(tool);
                continue;
            }

            HashSet<string> validParameters;
            try
            {
                validParameters = await LoadValidParametersAsync(manifestPath, tool.Command, cancellationToken);
            }
            catch (ParameterManifestException)
            {
                throw;
            }

            if (validParameters.Count == 0)
            {
                rewrittenTools.Add(tool);
                continue;
            }

            tool.Content = StripPhantomRows(tool.Content, validParameters, tool.Command);
            rewrittenTools.Add(tool);
        }

        return rewrittenTools;
    }

    private static async Task<HashSet<string>> LoadValidParametersAsync(string manifestPath, string toolCommand, CancellationToken cancellationToken)
    {
        var manifest = await CanonicalParameterManifestLoader.LoadAsync(
            manifestPath,
            expectedCommand: toolCommand,
            expectedNamespace: null,
            currentAzureMcpBuild: null,
            requireNonEmptyParameters: false,
            cancellationToken: cancellationToken);

        return manifest.Parameters
            .SelectMany(static entry => new[] { entry.CanonicalName, entry.DisplayName })
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => NormalizeParameterName(value!))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string StripPhantomRows(string content, ISet<string> validParameters, string command)
    {
        var lines = content.ReplaceLineEndings("\n").Split('\n').ToList();
        var rewritten = new List<string>(lines.Count);
        var inParameterTable = false;
        var preservedHeaderLines = 0;

        foreach (var line in lines)
        {
            if (!inParameterTable && IsParameterHeader(line))
            {
                inParameterTable = true;
                preservedHeaderLines = 1;
                rewritten.Add(line);
                continue;
            }

            if (inParameterTable && preservedHeaderLines == 1 && IsSeparatorLine(line))
            {
                preservedHeaderLines++;
                rewritten.Add(line);
                continue;
            }

            if (inParameterTable && IsTableLine(line))
            {
                var parameterName = ExtractFirstCell(line);
                var normalizedParameter = NormalizeParameterName(parameterName);
                if (string.IsNullOrWhiteSpace(normalizedParameter) || validParameters.Contains(normalizedParameter))
                {
                    rewritten.Add(line);
                }
                else
                {
                    Console.WriteLine($"⚠️ Phantom param stripped: {parameterName} from {command} — not in CLI source manifest");
                }

                continue;
            }

            if (inParameterTable)
            {
                inParameterTable = false;
                preservedHeaderLines = 0;
            }

            rewritten.Add(line);
        }

        return string.Join('\n', rewritten);
    }

    private static bool IsParameterHeader(string line)
        => Regex.IsMatch(line.Trim(), @"^\|\s*Parameter\s*\|", RegexOptions.IgnoreCase);

    private static bool IsSeparatorLine(string line)
        => Regex.IsMatch(line.Trim(), @"^\|\s*[-: ]+\|");

    private static bool IsTableLine(string line)
        => line.TrimStart().StartsWith("|", StringComparison.Ordinal);

    private static string ExtractFirstCell(string line)
    {
        var cells = line.Trim().Trim('|').Split('|');
        return cells.Length == 0 ? string.Empty : cells[0].Trim();
    }

    private static string NormalizeParameterName(string value)
    {
        var clean = value.Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Trim()
            .TrimStart('-')
            .ToLowerInvariant();

        clean = Regex.Replace(clean, @"[\s_]+", "-");
        clean = Regex.Replace(clean, @"[^a-z0-9\-]+", "-");
        return clean.Trim('-');
    }
}
