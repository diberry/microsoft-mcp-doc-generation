using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliAnalyzer.Models;

namespace CliAnalyzer.Reports;

public class MarkdownReporter
{
    public async Task GenerateReportAsync(AnalysisResults results, string outputPath)
    {
        var markdown = GenerateMarkdown(results);
        await File.WriteAllTextAsync(outputPath, markdown, Encoding.UTF8);
    }

    private string GenerateMarkdown(AnalysisResults results)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# CLI Analysis Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("## Overall Statistics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("| --- | ---: |");
        sb.AppendLine($"| Namespaces | {results.TotalNamespaces} |");
        sb.AppendLine($"| Tools | {results.TotalTools} |");
        sb.AppendLine($"| Parameters | {results.TotalParameters} |");
        sb.AppendLine($"| Required | {results.TotalRequiredParameters} |");
        sb.AppendLine($"| Optional | {results.TotalOptionalParameters} |");
        sb.AppendLine($"| Avg Params/Tool | {results.AverageTotalPerTool:F2} |");
        sb.AppendLine($"| Avg Required/Tool | {results.AverageRequiredPerTool:F2} |");
        sb.AppendLine($"| Avg Optional/Tool | {results.AverageOptionalPerTool:F2} |");
        sb.AppendLine($"| Avg Tools/Namespace | {results.AverageToolsPerNamespace:F2} |");
        sb.AppendLine();

        sb.AppendLine("## Namespace Breakdown");
        sb.AppendLine();
        sb.AppendLine("| Namespace | Tools | Total Params | Required | Optional | Avg/Tool |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");

        foreach (var ns in results.Namespaces)
        {
            sb.AppendLine($"| {Escape(ns.Name)} | {ns.TotalTools} | {ns.TotalParameters} | {ns.TotalRequiredParameters} | {ns.TotalOptionalParameters} | {ns.AverageTotalPerTool:F2} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Tools by Namespace");
        sb.AppendLine();

        foreach (var ns in results.Namespaces.OrderBy(n => n.Name))
        {
            sb.AppendLine($"### {Escape(ns.Name)}");
            sb.AppendLine();
            sb.AppendLine("| Command | Required | Optional | Total |");
            sb.AppendLine("| --- | ---: | ---: | ---: |");

            foreach (var tool in ns.Tools.OrderByDescending(t => t.TotalParameterCount))
            {
                sb.AppendLine($"| {Escape(tool.Command)} | {tool.RequiredParameterCount} | {tool.OptionalParameterCount} | {tool.TotalParameterCount} |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string Escape(string value)
    {
        return value.Replace("|", "\\|");
    }
}
