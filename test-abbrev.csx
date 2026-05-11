using System;
using System.Text.RegularExpressions;

var egPattern = new Regex(@"\be\.g\.\s*,?\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
var input = "Supports multiple regions (e.g., eastus, westus2). The default region i.e., westus is recommended.";
var result = egPattern.Replace(input, "for example, ");
Console.WriteLine("RESULT: " + result);
Console.WriteLine("CONTAINS: " + result.Contains("for example, eastus"));
