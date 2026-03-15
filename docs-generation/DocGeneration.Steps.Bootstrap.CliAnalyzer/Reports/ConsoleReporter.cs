using System;
using System.Collections.Generic;
using System.Linq;
using CliAnalyzer.Models;

namespace CliAnalyzer.Reports;

public class ConsoleReporter
{
    public void PrintSummary(AnalysisResults results)
    {
        try
        {
            Console.Clear();
        }
        catch
        {
            // Console.Clear() may fail in some environments, skip it
        }
        
        PrintHeader("CLI ANALYSIS SUMMARY");
        Console.WriteLine();

        PrintSection("OVERALL STATISTICS", () =>
        {
            PrintStat("Total Namespaces", results.TotalNamespaces);
            PrintStat("Total Tools", results.TotalTools);
            PrintStat("Total Parameters", results.TotalParameters);
            PrintStat("Total Required Parameters", results.TotalRequiredParameters);
            PrintStat("Total Optional Parameters", results.TotalOptionalParameters);
            Console.WriteLine();
            PrintStat("Avg Tools per Namespace", results.AverageToolsPerNamespace, 2);
            PrintStat("Avg Required Params per Tool", results.AverageRequiredPerTool, 2);
            PrintStat("Avg Optional Params per Tool", results.AverageOptionalPerTool, 2);
            PrintStat("Avg Total Params per Tool", results.AverageTotalPerTool, 2);
        });

        Console.WriteLine();
        PrintSection("NAMESPACE BREAKDOWN", () =>
        {
            PrintNamespaceTable(results.Namespaces);
        });

        Console.WriteLine();
        PrintSection("TOP TOOLS BY PARAMETER COUNT", () =>
        {
            var topTools = results.Namespaces
                .SelectMany(n => n.Tools.Select(t => (Namespace: n.Name, Tool: t)))
                .OrderByDescending(x => x.Tool.TotalParameterCount)
                .Take(10)
                .ToList();

            if (topTools.Count > 0)
            {
                PrintTopToolsTable(topTools);
            }
            else
            {
                Console.WriteLine("No tools found.");
            }
        });

        Console.WriteLine();
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"Report generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    private void PrintNamespaceTable(List<NamespaceAnalysis> namespaces)
    {
        var headers = new[] { "Namespace", "Tools", "Params", "Required", "Optional", "Avg/Tool" };
        var columnWidths = new[] { 20, 8, 8, 10, 10, 10 };

        PrintTableHeader(headers, columnWidths);

        foreach (var ns in namespaces)
        {
            var row = new object[]
            {
                ns.Name,
                ns.TotalTools,
                ns.TotalParameters,
                ns.TotalRequiredParameters,
                ns.TotalOptionalParameters,
                ns.AverageTotalPerTool.ToString("F1")
            };
            PrintTableRow(row, columnWidths);
        }

        PrintTableSeparator(columnWidths);
    }

    private void PrintTopToolsTable(List<(string Namespace, Tool Tool)> tools)
    {
        var headers = new[] { "Namespace", "Tool", "Command", "Required", "Optional", "Total" };
        var columnWidths = new[] { 15, 20, 30, 9, 9, 7 };

        PrintTableHeader(headers, columnWidths);

        foreach (var (ns, tool) in tools)
        {
            var row = new object[]
            {
                ns,
                tool.Name,
                tool.Command.Length > 30 ? tool.Command[..27] + "..." : tool.Command,
                tool.RequiredParameterCount,
                tool.OptionalParameterCount,
                tool.TotalParameterCount
            };
            PrintTableRow(row, columnWidths);
        }

        PrintTableSeparator(columnWidths);
    }

    private void PrintTableHeader(string[] headers, int[] columnWidths)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
        }
        catch
        {
            // Console color may not be supported
        }
        
        for (int i = 0; i < headers.Length; i++)
        {
            Console.Write(headers[i].PadRight(columnWidths[i]));
        }
        
        try
        {
            Console.ResetColor();
        }
        catch
        {
            // Ignore
        }
        
        Console.WriteLine();
        Console.WriteLine(string.Concat(Enumerable.Range(0, columnWidths.Sum()).Select(_ => "-")));
    }

    private void PrintTableRow(object[] values, int[] columnWidths)
    {
        for (int i = 0; i < values.Length; i++)
        {
            var value = values[i]?.ToString() ?? "";
            if (i == values.Length - 1)
            {
                Console.WriteLine(value.PadRight(columnWidths[i]));
            }
            else
            {
                Console.Write(value.PadRight(columnWidths[i]));
            }
        }
    }

    private void PrintTableSeparator(int[] columnWidths)
    {
        Console.WriteLine(string.Concat(Enumerable.Range(0, columnWidths.Sum()).Select(_ => "-")));
    }

    private void PrintStat(string label, object value, int precision = 0)
    {
        var formattedValue = value is double d ? d.ToString($"F{precision}") : value.ToString();
        Console.WriteLine($"  {label,-35} {formattedValue,10}");
    }

    private void PrintHeader(string title)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
        }
        catch
        {
            // Console color may not be supported
        }
        
        Console.WriteLine(new string('=', 80));
        Console.WriteLine(title.PadRight(80));
        Console.WriteLine(new string('=', 80));
        
        try
        {
            Console.ResetColor();
        }
        catch
        {
            // Ignore
        }
    }

    private void PrintSection(string title, Action contentAction)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
        }
        catch
        {
            // Console color may not be supported
        }
        
        Console.WriteLine($"\n{title}");
        Console.WriteLine(new string('-', title.Length));
        
        try
        {
            Console.ResetColor();
        }
        catch
        {
            // Ignore
        }
        
        contentAction();
    }

    public void PrintNamespaceDetail(NamespaceAnalysis ns)
    {
        PrintHeader($"NAMESPACE: {ns.Name}");
        Console.WriteLine();

        PrintSection("STATISTICS", () =>
        {
            PrintStat("Total Tools", ns.TotalTools);
            PrintStat("Total Parameters", ns.TotalParameters);
            PrintStat("Required Parameters", ns.TotalRequiredParameters);
            PrintStat("Optional Parameters", ns.TotalOptionalParameters);
            Console.WriteLine();
            PrintStat("Avg Required per Tool", ns.AverageRequiredPerTool, 2);
            PrintStat("Avg Optional per Tool", ns.AverageOptionalPerTool, 2);
            PrintStat("Avg Total per Tool", ns.AverageTotalPerTool, 2);
        });

        Console.WriteLine();
        PrintSection("TOOLS", () =>
        {
            var headers = new[] { "Tool Name", "Command", "Req", "Opt", "Total" };
            var columnWidths = new[] { 25, 35, 5, 5, 6 };

            PrintTableHeader(headers, columnWidths);

            foreach (var tool in ns.Tools)
            {
                var row = new object[]
                {
                    tool.Name,
                    tool.Command.Length > 35 ? tool.Command[..32] + "..." : tool.Command,
                    tool.RequiredParameterCount,
                    tool.OptionalParameterCount,
                    tool.TotalParameterCount
                };
                PrintTableRow(row, columnWidths);
            }

            PrintTableSeparator(columnWidths);
        });
    }

    public void PrintToolDetail(string namespaceName, Tool tool)
    {
        PrintHeader($"TOOL: {namespaceName} / {tool.Name}");
        Console.WriteLine();

        try
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
        }
        catch { }
        
        Console.WriteLine($"Command: {tool.Command}");
        
        try
        {
            Console.ResetColor();
        }
        catch { }
        
        Console.WriteLine();

        if (!string.IsNullOrWhiteSpace(tool.Description))
        {
            PrintSection("DESCRIPTION", () =>
            {
                Console.WriteLine(WrapText(tool.Description, 76));
            });
            Console.WriteLine();
        }

        PrintSection("PARAMETERS", () =>
        {
            PrintStat("Required", tool.RequiredParameterCount);
            PrintStat("Optional", tool.OptionalParameterCount);
            PrintStat("Total", tool.TotalParameterCount);
            Console.WriteLine();

            if (tool.Options.Count > 0)
            {
                var requiredParams = tool.Options.Where(p => p.Required).ToList();
                var optionalParams = tool.Options.Where(p => !p.Required).ToList();

                if (requiredParams.Count > 0)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    catch { }
                    
                    Console.WriteLine("REQUIRED PARAMETERS:");
                    
                    try
                    {
                        Console.ResetColor();
                    }
                    catch { }
                    
                    PrintParameterList(requiredParams);
                    Console.WriteLine();
                }

                if (optionalParams.Count > 0)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                    }
                    catch { }
                    
                    Console.WriteLine("OPTIONAL PARAMETERS:");
                    
                    try
                    {
                        Console.ResetColor();
                    }
                    catch { }
                    
                    PrintParameterList(optionalParams);
                }
            }
        });
    }

    private void PrintParameterList(List<Parameter> parameters)
    {
        foreach (var param in parameters)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
            }
            catch { }
            
            Console.WriteLine($"  {param.Name}");
            
            try
            {
                Console.ResetColor();
            }
            catch { }
            
            Console.WriteLine($"    Type: {param.Type}");
            if (!string.IsNullOrWhiteSpace(param.Description))
            {
                Console.WriteLine($"    Description: {WrapText(param.Description, 72, 4)}");
            }
            Console.WriteLine();
        }
    }

    private string WrapText(string text, int maxWidth, int indent = 0)
    {
        if (text.Length <= maxWidth)
            return text;

        var indentStr = new string(' ', indent);
        var lines = new List<string>();
        var currentLine = "";

        var words = text.Split(' ');
        foreach (var word in words)
        {
            if ((currentLine + word).Length > maxWidth)
            {
                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(indentStr + currentLine.Trim());
                }
                currentLine = word + " ";
            }
            else
            {
                currentLine += word + " ";
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(indentStr + currentLine.Trim());
        }

        return string.Join("\n", lines);
    }
}
