using System.Collections.Generic;
using System.Linq;
using CliAnalyzer.Models;

namespace CliAnalyzer.Analyzers;

public class CliDataAnalyzer
{
    public AnalysisResults Analyze(CliResponse data)
    {
        var namespaceGroups = data.Results
            .GroupBy(t => t.Namespace ?? "unknown")
            .OrderBy(g => g.Key)
            .ToList();

        var namespaces = namespaceGroups
            .Select(g => new NamespaceAnalysis
            {
                Name = g.Key,
                Tools = g.OrderBy(t => t.Name).ThenBy(t => t.Command).ToList()
            })
            .ToList();

        return new AnalysisResults { Namespaces = namespaces };
    }
}
