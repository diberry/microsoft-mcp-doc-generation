# Microsoft Style Guide Validator Prompt

This prompt identifies and fixes violations of the Microsoft Writing Style Guide in Azure MCP Server tool documentation.

## Purpose

Automatically review generated tool documentation to ensure compliance with Microsoft Writing Style Guide standards, based on the repository's [Copilot Instructions](../../../../.github/copilot-instructions.md).

## Common Violations Found in Generated Content

Based on analysis of generated tool documentation, common violations include:

1. **"e.g." instead of "for example"** - Appears in parameter descriptions
2. **Slash constructs** - Using "/" instead of writing out alternatives (e.g., "Get/retrieve/show", "Create/issue/generate", "List/enumerate")
3. **Passive voice** - "is not provided", "are returned", "is returned"
4. **Technical jargon without explanation** - Terms like "JSON array" without context
5. **"etc." or "and so on"** - Incomplete lists
6. **Missing contractions** - "is not" instead of "isn't", "will not" instead of "won't"
7. **Inconsistent parameter formatting** - Mixed use of angle brackets and quotes
8. **Repetitive wording** - "Returns: Returns:" (duplicate)
9. **"may" for possibility** - Should use "might" instead
10. **"may" for permission** - Should use "can" instead
11. **Gerunds in headings** - Headings ending in "-ing" (less common but should be avoided)
12. **Missing periods** - List items more than three words without periods
13. **Second person violations** - Using "you" inconsistently
14. **Imperative mood violations** - "You should" instead of direct commands

## The Prompt

```
Review the following Azure MCP Server tool documentation for Microsoft Writing Style Guide violations and provide corrections.

---
CONTENT TO REVIEW:
{PASTE_CONTENT_HERE}
---

Apply these Microsoft Writing Style Guide rules:

## Voice and Tone
1. **Active voice**: Change passive constructions to active voice
   - ❌ "Results are returned as a JSON array"
   - ✅ "Returns results as a JSON array"
   
2. **Second person**: Address the reader directly when appropriate
   - ❌ "The command retrieves and displays"
   - ✅ "Retrieves and displays" OR "Use this command to retrieve"

3. **Present tense**: Use present tense for descriptions
   - ❌ "This will create a new key"
   - ✅ "This creates a new key" OR "Creates a new key"

4. **Imperative mood**: Direct instructions without "you should"
   - ❌ "You should specify both parameters"
   - ✅ "Specify both parameters"

5. **Contractions**: Use natural contractions
   - ❌ "If not provided"
   - ✅ "If you don't provide" OR "When not provided"
   - ❌ "is not", "will not", "do not"
   - ✅ "isn't", "won't", "don't"

6. **Might vs may**: 
   - Use "might" for possibility
   - Use "can" for permission/capability
   - Never use "may" in either case

## Structure and Format

7. **Sentence case headings**: Only capitalize first word and proper nouns
   - ❌ "Account List", "Database Add"
   - ✅ "Account list", "Database add"

8. **No gerunds in headings**: Avoid "-ing" forms in headings
   - ❌ "Creating a certificate", "Listing accounts"
   - ✅ "Create a certificate", "List accounts"

9. **Concise writing**: Eliminate redundancy
   - ❌ "Create/issue/generate a new certificate"
   - ✅ "Create a certificate"
   - ❌ "Get/retrieve/show details"
   - ✅ "Get details" OR "Retrieve details"
   - ❌ "List/enumerate all keys"
   - ✅ "List all keys"

10. **Avoid etc.**: Complete the list or use "for example"
    - ❌ "Supports types: RSA, EC, etc."
    - ✅ "Supports types such as RSA and EC"

11. **For example instead of e.g.**
    - ❌ "(e.g., my-cosmos-account)"
    - ✅ "(for example, my-cosmos-account)"

12. **That is instead of i.e.**
    - ❌ "(i.e., the account name)"
    - ✅ "(that is, the account name)"

13. **List punctuation**: Items more than three words end with period
    - ❌ "The name of the Key Vault"
    - ✅ "The name of the Key Vault."

## Formatting

14. **Code style for technical terms**: Use backticks for file names, commands, values
    - ❌ "JSON array", "base64 encoded"
    - ✅ "`JSON` array", "`base64` encoded"

15. **Avoid slash constructs**: Write out alternatives clearly
    - ❌ "PFX/PEM file"
    - ✅ "PFX or PEM file"
    - ❌ "and/or"
    - ✅ "and" OR "or" (be specific)

16. **Consistent parameter references**: Use consistent style
    - ❌ Mixed: `--vault <vault>` and '--vault'
    - ✅ Consistent: `--vault <vault>` throughout

## Technical Writing

17. **Explain technical terms**: Add context for technical concepts
    - ❌ "Returns a JSON array"
    - ✅ "Returns the results as a `JSON` array"

18. **Clear conditions**: State conditions explicitly
    - ❌ "If no container specified"
    - ✅ "If you don't specify a container" OR "When no container is specified"

19. **Avoid redundancy**: Check for repeated words
    - ❌ "Returns: Returns: name, ID, keyId"
    - ✅ "Returns: name, ID, keyId"

20. **Capital "Must" for requirements**: Clarify obligation level
    - ❌ "You must specify"
    - ✅ "Specify" OR "Required:" (if already in required column)

---

## Output Format

For each violation found:

### 1. [Heading or Section Name]
**Line/Location**: [Approximate location in the content]
**Violation Type**: [Rule number and name from above]
**Current Text**: 
```
[Exact problematic text]
```
**Corrected Text**:
```
[Fixed version]
```
**Explanation**: [Brief reason for the change]

---

## Summary Statistics
- Total violations found: [number]
- Most common violation: [violation type]
- Sections affected: [list]

---

## Corrected Full Text
[Provide the complete corrected version of the content]

```

## Example Usage

### Input Content
```markdown
## Account List

<!-- cosmos account list -->

List all Cosmos DB accounts in a subscription. This command retrieves and displays all Cosmos DB accounts
available in the specified subscription. Results include account names and are returned as a JSON array.
```

### Expected Output
```
### 1. Account List (Heading)
**Line/Location**: Line 1
**Violation Type**: Rule 7 - Sentence case headings
**Current Text**: 
```
## Account List
```
**Corrected Text**:
```
## Account list
```
**Explanation**: Headings should use sentence case. Only capitalize the first word and proper nouns. "List" is not a proper noun.

---

### 2. Account List (Description)
**Line/Location**: Lines 5-6
**Violation Type**: Rule 1 - Active voice
**Current Text**: 
```
Results include account names and are returned as a JSON array.
```
**Corrected Text**:
```
Returns account names as a `JSON` array.
```
**Explanation**: Changed from passive "are returned" to active "Returns". Also applied Rule 14 (code style for JSON).

---

### 3. Account List (Description)
**Line/Location**: Line 6
**Violation Type**: Rule 14 - Code style for technical terms
**Current Text**: 
```
are returned as a JSON array
```
**Corrected Text**:
```
Returns results as a `JSON` array
```
**Explanation**: Technical terms like JSON should be formatted with backticks for code style.

---

## Summary Statistics
- Total violations found: 3
- Most common violation: Active voice (Rule 1)
- Sections affected: Account List

---

## Corrected Full Text
```markdown
## Account list

<!-- cosmos account list -->

List all Cosmos DB accounts in a subscription. This command retrieves and displays all Cosmos DB accounts
available in the specified subscription. Returns account names as a `JSON` array.
```
```

## Advanced Example with Parameters

### Input Content
```markdown
## Certificate Create

<!-- keyvault certificate create -->

Create/issue/generate a new certificate in an Azure Key Vault using the default certificate policy. Required: --vault, --certificate, --subscription. Optional: --tenant <tenant>. Returns: name, ID, keyId, secretId, cer (base64), thumbprint, enabled, notBefore, expiresOn, createdOn, updatedOn, subject, issuerName. Creates a new certificate version if it already exists.

| Parameter | Required or optional | Description |
|-----------|-------------|-------------|
| **Vault** | Required | The name of the Key Vault. |
| **Certificate** | Required | The name of the certificate. |
```

### Expected Output
```
### 1. Certificate Create (Heading)
**Line/Location**: Line 1
**Violation Type**: Rule 7 - Sentence case headings
**Current Text**: 
```
## Certificate Create
```
**Corrected Text**:
```
## Certificate create
```
**Explanation**: Headings should use sentence case. Only "Certificate" is capitalized as it appears at the start.

---

### 2. Certificate Create (Description - Slash construct)
**Line/Location**: Line 5
**Violation Type**: Rule 9 - Concise writing (avoid slashes)
**Current Text**: 
```
Create/issue/generate a new certificate
```
**Corrected Text**:
```
Create a certificate
```
**Explanation**: Slash constructs like "Create/issue/generate" are difficult to read. Choose one clear verb. "Create" is most common and clear.

---

### 3. Certificate Create (Description - base64)
**Line/Location**: Line 5
**Violation Type**: Rule 14 - Code style for technical terms
**Current Text**: 
```
cer (base64)
```
**Corrected Text**:
```
cer (`base64`)
```
**Explanation**: Technical terms and encoding formats like "base64" should use code style with backticks.

---

### 4. Certificate Create (Parameter Description)
**Line/Location**: Parameter table, Description column
**Violation Type**: Rule 13 - List punctuation
**Current Text**: 
```
The name of the Key Vault.
```
**Corrected Text**:
```
The name of the Key Vault.
```
**Explanation**: This is already correct - descriptions more than three words should end with a period.

---

## Summary Statistics
- Total violations found: 4
- Most common violation: Sentence case headings (Rule 7)
- Sections affected: Certificate Create

---

## Corrected Full Text
```markdown
## Certificate create

<!-- keyvault certificate create -->

Create a certificate in an Azure Key Vault using the default certificate policy. Required: `--vault`, `--certificate`, `--subscription`. Optional: `--tenant <tenant>`. Returns: name, ID, keyId, secretId, cer (`base64`), thumbprint, enabled, notBefore, expiresOn, createdOn, updatedOn, subject, issuerName. Creates a new certificate version if it already exists.

| Parameter | Required or optional | Description |
|-----------|-------------|-------------|
| **Vault** | Required | The name of the Key Vault. |
| **Certificate** | Required | The name of the certificate. |
```
```

## Batch Processing Multiple Files

When reviewing multiple tool documentation files:

1. **Process each tool section separately**
2. **Track violations across files** to identify patterns
3. **Generate a summary report** showing:
   - Total violations per file
   - Most common violations across all files
   - Files with most violations
4. **Provide consolidated corrections** file-by-file

## Quality Checklist

After applying corrections, verify:

- [ ] All headings use sentence case
- [ ] No slash constructs remain (Create/issue/generate → Create)
- [ ] "e.g." replaced with "for example"
- [ ] "i.e." replaced with "that is"
- [ ] "etc." replaced with complete lists or "for example"
- [ ] Active voice used throughout
- [ ] Technical terms use code style (`JSON`, `base64`)
- [ ] No "may" for possibility (use "might") or permission (use "can")
- [ ] List items >3 words end with periods
- [ ] Contractions used naturally ("isn't" not "is not")
- [ ] No redundant phrases ("Returns: Returns:" → "Returns:")
- [ ] Imperative mood for instructions (no "you should")
- [ ] No gerunds in headings (-ing forms)

## Integration with Other Tools

This validator works alongside:
- [master-prompt-generator.md](master-prompt-generator.md) - Generates example prompts
- [tool-description-analyzer-prompt.md](tool-description-analyzer-prompt.md) - Separates human/MCP content
- [example-prompts-template.hbs](example-prompts-template.hbs) - Formats example sections

Use this validator **after** generating content but **before** publishing to ensure style compliance.

## Automated Usage Pattern

1. Generate tool documentation using existing tools
2. Run through Style Guide Validator
3. Apply corrections automatically or review manually
4. Validate with final checklist
5. Publish corrected documentation

## Related Resources

- [Microsoft Writing Style Guide](https://learn.microsoft.com/style-guide/welcome/)
- [Repository Copilot Instructions](../../../../.github/copilot-instructions.md)
- [Azure MCP Server Tools Documentation](../../tools/)
