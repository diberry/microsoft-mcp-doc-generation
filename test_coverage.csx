using Shared;
var test1 = new[] { "messages [{config}]" };
var test2 = new[] { "messages [{'role': 'user'}]" };
Console.WriteLine("Test 1: " + ParameterCoverageChecker.GetConcretePromptCoverage(test1, "messages", 1).Covered);
Console.WriteLine("Test 2: " + ParameterCoverageChecker.GetConcretePromptCoverage(test2, "messages", 1).Covered);
