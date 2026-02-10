# LLM-Based Content Validation Template

This document provides a template and instructions for creating new LLM-based validations for generated content, using the Example Prompt Validation as a reference implementation.

## Overview

The Example Prompt Validation demonstrates a pattern for using LLMs to validate generated documentation content. This template can be adapted for other validation needs such as:
- Validating parameter documentation completeness
- Validating code examples for correctness
- Validating consistency across documentation files
- Validating accessibility and readability standards
- Any other quality checks on generated content

## Architecture Pattern

The validation pattern consists of:
1. **Validation Package** - A standalone .NET library with LLM integration
2. **Validation Prompts** - System and user prompts in `docs-generation/prompts/`
3. **Integration Point** - Called after relevant content is generated
4. **Validation Report** - Markdown report with structured results

## Questions to Answer Before Building

Before creating a new validation, answer these questions to guide your implementation:

### 1. Validation Scope

**Q: What specific content are you validating?**
- Example: "Generated example prompts" (current implementation)
- Your answer: _________________________________

**Q: What quality criteria define "valid" content?**
- Example: "All required parameters present AND wrapped in single quotes"
- Your answer: _________________________________

**Q: What are the specific validation rules?**
- Example: "Exclude infrastructure parameters (subscription, tenant, auth, retry)"
- Your answer: _________________________________

### 2. Input Requirements

**Q: What generated files does the validation need to read?**
- Example: `generated/tools/*.complete.md` files
- Your answer: _________________________________

**Q: Does the validation need the complete file or specific sections?**
- Example: Complete tool file (description, parameters, metadata, example prompts)
- Your answer: _________________________________

**Q: What flags/conditions trigger this validation?**
- Example: `--validate-prompts` flag + `--example-prompts` + `--complete-tools` flags
- Your answer: _________________________________

### 3. Validation Logic

**Q: Can an LLM understand the validation rules from natural language instructions?**
- Example: Yes - LLM can understand "parameter must be in single quotes"
- Your answer: _________________________________

**Q: What context does the LLM need to perform validation?**
- Example: Complete tool documentation with parameters and prompts
- Your answer: _________________________________

**Q: What output structure do you need from the LLM?**
- Example: JSON with per-item validation results, issues list, recommendations
- Your answer: _________________________________

### 4. Naming and Organization

**Q: What is the specific name for this validation?**
- Example: "Example Prompt Validation" (not just "Validation")
- Your answer: _________________________________

**Q: What should the validation package be named?**
- Example: `ExamplePromptValidator`
- Pattern: `{Content}Validator` (specific, not generic)
- Your answer: _________________________________

**Q: What should the prompt files be named?**
- Example: `system-prompt-example-prompt-validation.txt`, `user-prompt-example-prompt-validation.txt`
- Pattern: `system-prompt-{validation-name}.txt`, `user-prompt-{validation-name}.txt`
- Your answer: _________________________________

**Q: What should the report file be named?**
- Example: `example-prompt-validation-report.md`
- Pattern: `{validation-name}-validation-report.md`
- Your answer: _________________________________

### 5. Integration Points

**Q: When should the validation run?**
- Example: After complete tools are generated (at end of content generation)
- Your answer: _________________________________

**Q: What other generation flags/options does it depend on?**
- Example: Requires `--example-prompts` AND `--complete-tools`
- Your answer: _________________________________

**Q: Where in DocumentationGenerator should it be called?**
- Example: Line ~293-297 after complete tool generation
- Your answer: _________________________________

## Implementation Steps

### Step 1: Create Validation Package

1. **Create project directory**
   ```bash
   mkdir docs-generation/{YourValidator}
   ```

2. **Create .csproj file** at `docs-generation/{YourValidator}/{YourValidator}.csproj`
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net9.0</TargetFramework>
       <OutputType>Library</OutputType>
       <Nullable>enable</Nullable>
       <ImplicitUsings>enable</ImplicitUsings>
     </PropertyGroup>
     <ItemGroup>
       <ProjectReference Include="../Shared/Shared.csproj" />
       <ProjectReference Include="../GenerativeAI/GenerativeAI.csproj" />
     </ItemGroup>
   </Project>
   ```

3. **Create validator class** at `docs-generation/{YourValidator}/{YourValidator}.cs`

   Key components:
   ```csharp
   using GenerativeAI;
   
   public class YourValidator
   {
       private readonly GenerativeAIClient? _aiClient;
       private readonly string _systemPrompt;
       private readonly string _userPromptTemplate;
       
       public YourValidator()
       {
           // Initialize AI client and load prompts
           _aiClient = new GenerativeAIClient();
           var systemPromptPath = Path.Combine(promptsDir, "system-prompt-{your-validation}.txt");
           var userPromptPath = Path.Combine(promptsDir, "user-prompt-{your-validation}.txt");
           // Load prompt files...
       }
       
       public bool IsInitialized() 
       {
           return _aiClient != null && 
                  !string.IsNullOrEmpty(_systemPrompt) && 
                  !string.IsNullOrEmpty(_userPromptTemplate);
       }
       
       public async Task<ValidationResult?> ValidateWithLLMAsync(string contentToValidate)
       {
           // Build user prompt
           var userPrompt = _userPromptTemplate.Replace("{CONTENT}", contentToValidate);
           
           // Call LLM
           var response = await _aiClient.GetChatCompletionAsync(_systemPrompt, userPrompt);
           
           // Parse and return results
           return ParseValidationResponse(response);
       }
   }
   
   public class ValidationResult
   {
       // Define your result structure
       public bool IsValid { get; set; }
       public List<ValidationItem> Items { get; set; } = new();
       public string Summary { get; set; } = "";
       public List<string> Recommendations { get; set; } = new();
   }
   
   public class ValidationItem
   {
       public string ItemName { get; set; } = "";
       public bool IsValid { get; set; }
       public List<string> Issues { get; set; } = new();
   }
   ```

4. **Add to solution**
   ```bash
   cd docs-generation
   dotnet sln add {YourValidator}/{YourValidator}.csproj
   ```

### Step 2: Create Validation Prompts

1. **Create system prompt** at `docs-generation/prompts/system-prompt-{your-validation}.txt`

   Structure:
   ```markdown
   # System Prompt for {Your Validation Type}
   
   You are an expert technical validator specializing in [domain]. Your task is to validate [what you're validating].
   
   ## Your Role
   You will receive:
   1. [Description of input]
   
   ## Validation Task
   [Detailed description of what to validate]
   
   ## Definition of Valid Content
   Content is VALID if:
   1. [Criterion 1]
   2. [Criterion 2]
   3. [Criterion 3]
   
   Content is INVALID if:
   1. [Invalid condition 1]
   2. [Invalid condition 2]
   
   ## Excluded/Special Cases
   [Any special handling or exclusions]
   
   ## Validation Process
   1. [Step 1]
   2. [Step 2]
   3. [Step 3]
   
   ## Output Format
   Return a JSON object with this structure:
   ```json
   {
     "isValid": true/false,
     "items": [
       {
         "name": "...",
         "isValid": true/false,
         "issues": ["issue1", "issue2"]
       }
     ],
     "summary": "...",
     "recommendations": ["rec1", "rec2"]
   }
   ```
   
   ## Important Guidelines
   - Be strict: [strict requirements]
   - Be flexible: [where flexibility is allowed]
   - Provide specific feedback: [what kind of feedback]
   - Return valid JSON only - no markdown code blocks
   ```

2. **Create user prompt template** at `docs-generation/prompts/user-prompt-{your-validation}.txt`

   Structure:
   ```markdown
   # User Prompt Template for {Your Validation}
   
   Please validate the following content according to the validation rules.
   
   {DESCRIPTION_OF_CONTENT}
   
   ---
   
   {CONTENT_TO_VALIDATE}
   
   ---
   
   Return your validation results in JSON format as specified in the system prompt.
   ```

### Step 3: Integrate into CSharpGenerator

1. **Add project reference** to `docs-generation/CSharpGenerator/CSharpGenerator.csproj`
   ```xml
   <ItemGroup>
     <ProjectReference Include="../{YourValidator}/{YourValidator}.csproj" />
   </ItemGroup>
   ```

2. **Add CLI flag** in `Program.cs`
   ```csharp
   var validate{YourContent} = args.Contains("--validate-{your-content}");
   Console.WriteLine($"  validate{YourContent}: {validate{YourContent}}");
   ```

3. **Update GenerateAsync signature** in `DocumentationGenerator.cs`
   ```csharp
   public static async Task<int> GenerateAsync(
       // ... existing parameters
       bool validate{YourContent} = false)
   ```

4. **Add validation call** in `DocumentationGenerator.cs` after relevant content is generated
   ```csharp
   // Validate {your content} if requested (after {prerequisite} is generated)
   if (validate{YourContent} && {prerequisiteConditions})
   {
       Console.WriteLine("\n=== Validating {Your Content} with LLM ===");
       await Validate{YourContent}Async(transformedData, {requiredPaths});
   }
   else if (validate{YourContent} && !{prerequisiteConditions})
   {
       Console.WriteLine("\n⚠️  {Your Content} validation requires {prerequisites}");
   }
   ```

5. **Implement validation method** in `DocumentationGenerator.cs`
   ```csharp
   /// <summary>
   /// Validates {your content} using LLM with full context.
   /// </summary>
   private static async Task Validate{YourContent}Async(
       TransformedData data, 
       string {requiredPath1}, 
       string {requiredPath2})
   {
       // Initialize validator
       var validator = new {YourValidator}.{YourValidator}();
       if (!validator.IsInitialized())
       {
           Console.WriteLine($"⚠️  Validator not initialized. Check Azure OpenAI configuration.");
           return;
       }
       
       // Track results
       int totalItems = 0;
       int validItems = 0;
       int invalidItems = 0;
       var validationResults = new List<...>();
       
       // Iterate through content to validate
       foreach (var item in data.{YourContentCollection})
       {
           totalItems++;
           
           // Read content file
           var filePath = Path.Combine({path}, {filename});
           if (!File.Exists(filePath)) continue;
           
           var content = await File.ReadAllTextAsync(filePath);
           
           // Validate
           var result = await validator.ValidateWithLLMAsync(content);
           if (result == null) continue;
           
           validationResults.Add(result);
           
           if (result.IsValid)
           {
               validItems++;
               Console.WriteLine($"  ✅ {item.Name,-50} (valid)");
           }
           else
           {
               invalidItems++;
               Console.WriteLine($"  ❌ {item.Name,-50} ({result.Summary})");
           }
       }
       
       // Print summary
       Console.WriteLine("\n=== Validation Summary ===");
       Console.WriteLine($"Total items: {totalItems}");
       Console.WriteLine($"Valid: {validItems}");
       Console.WriteLine($"Invalid: {invalidItems}");
       
       // Generate report
       await Generate{YourContent}ValidationReport(validationResults, {outputPath});
   }
   
   private static async Task Generate{YourContent}ValidationReport(
       List<ValidationResult> results, 
       string outputPath)
   {
       var report = new System.Text.StringBuilder();
       report.AppendLine("# {Your Content} Validation Report (LLM-Based)");
       report.AppendLine();
       report.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
       // ... build report
       
       await File.WriteAllTextAsync(outputPath, report.ToString());
       Console.WriteLine($"\n📋 {Your Content} validation report saved to: {outputPath}");
   }
   ```

### Step 4: Update generate.sh (Optional)

If you need to add a command-line flag for your validator, you can modify `generate.sh` at the repository root. However, most validators are integrated into the generation pipeline automatically. If you need a separate flag:

```bash
# This is optional - most validators run automatically as part of the pipeline
# Only add this if you need a separate validation command

# Add to OPTIONS section in show_help()
    --validate-{your-content}  Enable {your content} validation

# Add to argument parsing
    --validate-{your-content})
        VALIDATE_{YOUR_CONTENT}=true
        shift
        ;;
```

Alternatively, integrate your validation into the existing PowerShell pipeline (`Generate.ps1` or related scripts).

### Step 5: Create Tests

1. **Create test project** at `docs-generation/{YourValidator}.Tests/{YourValidator}.Tests.csproj`

2. **Write tests** at `docs-generation/{YourValidator}.Tests/{YourValidator}Tests.cs`
   ```csharp
   using Xunit;
   
   public class {YourValidator}Tests
   {
       [Fact]
       public void Constructor_DoesNotThrow()
       {
           var validator = new {YourValidator}();
           Assert.NotNull(validator);
       }
       
       [Fact]
       public void IsInitialized_ReturnsBoolean()
       {
           var validator = new {YourValidator}();
           var isInit = validator.IsInitialized();
           Assert.IsType<bool>(isInit);
       }
       
       [Fact]
       public void ValidationResult_CanBeInstantiated()
       {
           var result = new ValidationResult
           {
               IsValid = true,
               Items = new(),
               Summary = "Test"
           };
           Assert.True(result.IsValid);
       }
   }
   ```

3. **Add to solution**
   ```bash
   dotnet sln add {YourValidator}.Tests/{YourValidator}.Tests.csproj
   ```

### Step 6: Documentation

1. **Create README** at `docs-generation/{YourValidator}/README.md`

   Include:
   - Purpose and overview
   - Validation rules and criteria
   - Usage instructions
   - Configuration requirements (Azure OpenAI)
   - Output format examples
   - Architecture details
   - Integration points

2. **Update main documentation** to reference the new validation

## Testing Your Validation

1. **Build solution**
   ```bash
   cd docs-generation
   dotnet build docs-generation.sln --configuration Release
   ```

2. **Run tests**
   ```bash
   dotnet test {YourValidator}.Tests/{YourValidator}.Tests.csproj
   ```

3. **Test end-to-end**
   ```bash
   cd ..
   # Run full generation (includes all validators in pipeline)
   ./generate.sh all
   
   # Or run specific tool family
   ./generate.sh family keyvault
   ```

4. **Check output**
   - Console output shows validation status
   - Report file created at `generated/logs/{your-content}-validation-report.md`
   - All flags work correctly together

## Best Practices

### DO:
- ✅ Use specific names (e.g., "Example Prompt Validation" not "Validation")
- ✅ Keep validation focused on one aspect of content quality
- ✅ Provide clear, actionable feedback in validation results
- ✅ Pass complete context files to LLM (don't extract/reconstruct)
- ✅ Run validation after all prerequisite content is generated
- ✅ Generate structured reports with specific issues
- ✅ Handle Azure OpenAI configuration errors gracefully
- ✅ Test with and without Azure OpenAI configured

### DON'T:
- ❌ Use generic names like "Validator" or "ContentValidator"
- ❌ Try to validate multiple unrelated things in one validator
- ❌ Manually parse and reconstruct context from files
- ❌ Run validation before prerequisite content exists
- ❌ Fail the entire generation if validation fails
- ❌ Require Azure OpenAI for non-validation operations

## Troubleshooting

**Problem**: Validator not initializing
- Check Azure OpenAI environment variables (FOUNDRY_API_KEY, FOUNDRY_ENDPOINT, FOUNDRY_MODEL_NAME)
- Verify prompt files exist and are readable
- Check for exceptions in constructor

**Problem**: LLM returns invalid JSON
- Update system prompt to emphasize JSON-only output
- Add examples of valid JSON format in system prompt
- Improve JSON parsing with better error handling

**Problem**: Validation always passes/fails
- Review validation criteria in system prompt
- Add more specific examples of valid/invalid content
- Test system prompt in isolation with sample content

**Problem**: Slow validation
- Consider batch validation (multiple items per LLM call)
- Use async/parallel processing where appropriate
- Cache results if validating same content multiple times

## Example Reference

See the **ExamplePromptValidator** implementation for a complete working example:
- Package: `docs-generation/ExamplePromptValidator/`
- Prompts: `docs-generation/prompts/system-prompt-example-prompt-validation.txt` and `user-prompt-example-prompt-validation.txt`
- Integration: `docs-generation/CSharpGenerator/DocumentationGenerator.cs` (lines ~293-2100)
- Tests: `docs-generation/ExamplePromptValidator.Tests/`
- Documentation: `docs-generation/ExamplePromptValidator/README.md`

## Next Steps

After implementing your validation:
1. Test thoroughly with real generated content
2. Iterate on system prompt based on validation quality
3. Document known limitations and edge cases
4. Consider adding metrics/telemetry for validation effectiveness
5. Share learnings to improve this template

---

**Questions or Issues?**

If you encounter problems or have suggestions for improving this template, document them and share with the team. This template should evolve as we build more validations.
