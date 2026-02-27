# Parker's Project History

## What I Know About Testing in This Project

### Test Command
```bash
dotnet test docs-generation.sln
```
For release validation (zero warnings):
```bash
dotnet build docs-generation.sln --configuration Release
dotnet test docs-generation.sln
```

### Test Projects and What They Cover

| Test Project | Source Project | Key Coverage |
|---|---|---|
| `CSharpGenerator.Tests/` | `CSharpGenerator/` | Generator logic, parameter filtering, filename generation |
| `AzmcpCommandParser.Tests/` | `AzmcpCommandParser/` | CLI output parsing |
| `BrandMapperValidator.Tests/` | `BrandMapperValidator/` | Brand mapping validation |
| `ExamplePromptGeneratorStandalone.Tests/` | `ExamplePromptGeneratorStandalone/` | Prompt generation, JSON parsing |
| `ExamplePromptValidator.Tests/` | `ExamplePromptValidator/` | Prompt validation |
| `GenerativeAI.Tests/` | `GenerativeAI/` | AI client, retry logic, LogFileHelper |
| `HorizontalArticleGenerator.Tests/` | `HorizontalArticleGenerator/` | Article generation, ArticleContentProcessor validations |
| `Shared.Tests/` | `Shared/` | DataFileLoader, LogFileHelper, CliVersionReader |
| `TemplateEngine.Tests/` | `TemplateEngine/` | Handlebars helpers, template rendering |
| `TextTransformation.Tests/` | `TextTransformation/` | Text replacements, TransformText, TransformDescription |
| `ToolGeneration_Improved.Tests/` | `ToolGeneration_Improved/` | AI-improved tool doc generation |
| `E2eTestPromptParser.Tests/` | `E2eTestPromptParser/` | E2E test prompt parsing |

### Important Test Patterns I Follow

**ArticleContentProcessor tests** (most complex):
- Test each validation in isolation
- Use varied Azure services — never all Storage or all Key Vault
- Cover edge cases: empty strings, null values, Unicode
- Fabricated RBAC role tests: "Azure X Administrator" should be detected; real "Reader", "Contributor" should not

**Text transformation tests**:
- Test both `TransformText()` and `TransformDescription()`
- Key difference: `TransformDescription` adds trailing period; `TransformText` does not
- Test the "Microsoft Foundry" branding replacement

**Data file loading tests** (Shared.DataFileLoader):
- Use temporary file paths in tests
- Verify thread-safety (concurrent access doesn't corrupt data)

### Common Test Failures I've Debugged

1. **Null reference in test**: Usually missing `InternalsVisibleTo` attribute — add to source `.csproj`
2. **File path issues**: Tests assuming running directory — use `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)` for data files
3. **Service-specific test data**: Tests all using Storage examples — diversify across services
4. **AI-dependent tests failing**: Tests calling real AI — mock with `IGenerativeAIClient` interface instead

### When Morgan Adds a Feature

My workflow:
1. Read what Morgan changed
2. Identify the class/method(s) affected
3. Write tests for the new behavior (happy path + edge cases)
4. Write tests for the old behavior (regression)
5. Run: `dotnet test docs-generation.sln`
6. Fix any failures
7. Report back: "Tests added and passing"
