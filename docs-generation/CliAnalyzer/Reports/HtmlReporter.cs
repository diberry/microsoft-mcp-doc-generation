using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliAnalyzer.Models;

namespace CliAnalyzer.Reports;

public class HtmlReporter
{
    public async Task GenerateReportAsync(AnalysisResults results, string outputPath)
    {
        var html = GenerateHtml(results);
        await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8);
    }

    private string GenerateHtml(AnalysisResults results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <title>CLI Analysis Report</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetCss());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("  <div class=\"container\">");
        sb.AppendLine("    <h1>CLI Analysis Report</h1>");
        sb.AppendLine($"    <p class=\"timestamp\">Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

        // Overall Statistics
        sb.AppendLine("    <section class=\"section\">");
        sb.AppendLine("      <h2>Overall Statistics</h2>");
        sb.AppendLine("      <div class=\"stats-grid\">");
        sb.AppendLine($"        <div class=\"stat-card\"><span class=\"stat-label\">Namespaces</span><span class=\"stat-value\">{results.TotalNamespaces}</span></div>");
        sb.AppendLine($"        <div class=\"stat-card\"><span class=\"stat-label\">Tools</span><span class=\"stat-value\">{results.TotalTools}</span></div>");
        sb.AppendLine($"        <div class=\"stat-card\"><span class=\"stat-label\">Parameters</span><span class=\"stat-value\">{results.TotalParameters}</span></div>");
        sb.AppendLine($"        <div class=\"stat-card\"><span class=\"stat-label\">Required</span><span class=\"stat-value required\">{results.TotalRequiredParameters}</span></div>");
        sb.AppendLine($"        <div class=\"stat-card\"><span class=\"stat-label\">Optional</span><span class=\"stat-value optional\">{results.TotalOptionalParameters}</span></div>");
        sb.AppendLine($"        <div class=\"stat-card\"><span class=\"stat-label\">Avg Params/Tool</span><span class=\"stat-value\">{results.AverageTotalPerTool:F2}</span></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </section>");

        // Namespace Table
        sb.AppendLine("    <section class=\"section\">");
        sb.AppendLine("      <h2>Namespace Breakdown</h2>");
        sb.AppendLine("      <table class=\"data-table\">");
        sb.AppendLine("        <thead>");
        sb.AppendLine("          <tr>");
        sb.AppendLine("            <th>Namespace</th>");
        sb.AppendLine("            <th>Tools</th>");
        sb.AppendLine("            <th>Total Params</th>");
        sb.AppendLine("            <th>Required</th>");
        sb.AppendLine("            <th>Optional</th>");
        sb.AppendLine("            <th>Avg/Tool</th>");
        sb.AppendLine("          </tr>");
        sb.AppendLine("        </thead>");
        sb.AppendLine("        <tbody>");

        foreach (var ns in results.Namespaces)
        {
            sb.AppendLine("          <tr>");
            sb.AppendLine($"            <td><strong>{HtmlEncode(ns.Name)}</strong></td>");
            sb.AppendLine($"            <td>{ns.TotalTools}</td>");
            sb.AppendLine($"            <td>{ns.TotalParameters}</td>");
            sb.AppendLine($"            <td class=\"required\">{ns.TotalRequiredParameters}</td>");
            sb.AppendLine($"            <td class=\"optional\">{ns.TotalOptionalParameters}</td>");
            sb.AppendLine($"            <td>{ns.AverageTotalPerTool:F2}</td>");
            sb.AppendLine("          </tr>");
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </section>");

        // Top Tools
        sb.AppendLine("    <section class=\"section\">");
        sb.AppendLine("      <h2>Top 20 Tools by Parameter Count</h2>");
        sb.AppendLine("      <table class=\"data-table\">");
        sb.AppendLine("        <thead>");
        sb.AppendLine("          <tr>");
        sb.AppendLine("            <th>Namespace</th>");
        sb.AppendLine("            <th>Tool</th>");
        sb.AppendLine("            <th>Command</th>");
        sb.AppendLine("            <th>Required</th>");
        sb.AppendLine("            <th>Optional</th>");
        sb.AppendLine("            <th>Total</th>");
        sb.AppendLine("          </tr>");
        sb.AppendLine("        </thead>");
        sb.AppendLine("        <tbody>");

        var topTools = results.Namespaces
            .SelectMany(n => n.Tools.Select(t => (Namespace: n.Name, Tool: t)))
            .OrderByDescending(x => x.Tool.TotalParameterCount)
            .Take(20);

        foreach (var (ns, tool) in topTools)
        {
            sb.AppendLine("          <tr>");
            sb.AppendLine($"            <td>{HtmlEncode(ns)}</td>");
            sb.AppendLine($"            <td>{HtmlEncode(tool.Name)}</td>");
            sb.AppendLine($"            <td class=\"command\">{HtmlEncode(tool.Command)}</td>");
            sb.AppendLine($"            <td class=\"required\">{tool.RequiredParameterCount}</td>");
            sb.AppendLine($"            <td class=\"optional\">{tool.OptionalParameterCount}</td>");
            sb.AppendLine($"            <td><strong>{tool.TotalParameterCount}</strong></td>");
            sb.AppendLine("          </tr>");
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </section>");

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GetCss()
    {
        return @"
body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
  background-color: #f5f5f5;
  color: #333;
  margin: 0;
  padding: 0;
}

.container {
  max-width: 1400px;
  margin: 0 auto;
  padding: 20px;
  background-color: white;
}

h1 {
  color: #2c3e50;
  border-bottom: 3px solid #3498db;
  padding-bottom: 10px;
}

h2 {
  color: #34495e;
  margin-top: 30px;
  margin-bottom: 15px;
}

.timestamp {
  color: #7f8c8d;
  font-size: 14px;
  margin: 0 0 20px 0;
}

.section {
  margin: 20px 0;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 15px;
  margin: 20px 0;
}

.stat-card {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 20px;
  border-radius: 8px;
  text-align: center;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}

.stat-label {
  display: block;
  font-size: 12px;
  opacity: 0.9;
  margin-bottom: 8px;
}

.stat-value {
  display: block;
  font-size: 28px;
  font-weight: bold;
}

.stat-card.required .stat-value {
  color: #ffcdd2;
}

.stat-card.optional .stat-value {
  color: #c8e6c9;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  margin: 20px 0;
  box-shadow: 0 2px 4px rgba(0,0,0,0.05);
}

.data-table thead {
  background-color: #2c3e50;
  color: white;
}

.data-table th {
  padding: 12px 15px;
  text-align: left;
  font-weight: 600;
  font-size: 13px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.data-table td {
  padding: 10px 15px;
  border-bottom: 1px solid #ecf0f1;
}

.data-table tbody tr:hover {
  background-color: #f8f9fa;
}

.data-table tbody tr:nth-child(even) {
  background-color: #f9f9f9;
}

.data-table td.required {
  color: #c0392b;
  font-weight: 500;
}

.data-table td.optional {
  color: #27ae60;
  font-weight: 500;
}

.data-table td.command {
  font-family: 'Courier New', monospace;
  font-size: 12px;
  color: #7f8c8d;
}

@media (max-width: 768px) {
  .container {
    padding: 10px;
  }
  
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
  
  .data-table {
    font-size: 12px;
  }
  
  .data-table th, .data-table td {
    padding: 8px 10px;
  }
}
";
    }

    private string HtmlEncode(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }
}
