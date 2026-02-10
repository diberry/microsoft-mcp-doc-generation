using System.Collections.Generic;
using System.Linq;

namespace CliAnalyzer.Models;

public class NamespaceAnalysis
{
    public required string Name { get; set; }
    public List<Tool> Tools { get; set; } = [];
    public int TotalTools => Tools.Count;
    public int TotalParameters => Tools.Sum(t => t.TotalParameterCount);
    public int TotalRequiredParameters => Tools.Sum(t => t.RequiredParameterCount);
    public int TotalOptionalParameters => Tools.Sum(t => t.OptionalParameterCount);
    public double AverageRequiredPerTool => TotalTools > 0 ? TotalRequiredParameters / (double)TotalTools : 0;
    public double AverageOptionalPerTool => TotalTools > 0 ? TotalOptionalParameters / (double)TotalTools : 0;
    public double AverageTotalPerTool => TotalTools > 0 ? TotalParameters / (double)TotalTools : 0;
}

public class AnalysisResults
{
    public List<NamespaceAnalysis> Namespaces { get; set; } = [];
    public int TotalNamespaces => Namespaces.Count;
    public int TotalTools => Namespaces.Sum(n => n.TotalTools);
    public int TotalParameters => Namespaces.Sum(n => n.TotalParameters);
    public int TotalRequiredParameters => Namespaces.Sum(n => n.TotalRequiredParameters);
    public int TotalOptionalParameters => Namespaces.Sum(n => n.TotalOptionalParameters);
    public double AverageRequiredPerTool => TotalTools > 0 ? TotalRequiredParameters / (double)TotalTools : 0;
    public double AverageOptionalPerTool => TotalTools > 0 ? TotalOptionalParameters / (double)TotalTools : 0;
    public double AverageTotalPerTool => TotalTools > 0 ? TotalParameters / (double)TotalTools : 0;
    public double AverageToolsPerNamespace => TotalNamespaces > 0 ? TotalTools / (double)TotalNamespaces : 0;
}
