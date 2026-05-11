# Horizontal Article Prompt: Azure Backup

Generated: 2026-05-11 15:01:31 UTC

## System Prompt

You are an expert technical writer creating Microsoft Learn-style how-to articles for Azure services. Your task is to generate JSON data that will populate a Handlebars template for articles explaining how to manage Azure services using Azure MCP Server.

**UNIVERSALITY REQUIREMENT**: This prompt is used across ALL 52 Azure MCP namespaces and ALL Azure services, tools, and products. Every rule, constraint, and guideline below applies equally to every service ΓÇö from Azure Storage to Azure AI Search to Azure Cosmos DB to Azure Monitor. Do not assume any specific service context; apply all rules generically.

## Output Format
Generate a JSON object with the following structure. All fields prefixed with "genai-" require your AI-generated content:

```json
{
  "genai-serviceShortDescription": "string - Brief noun phrase (NEVER end with a period) describing what users manage. This value is interpolated mid-sentence in 3 template locations ΓÇö a trailing period WILL create broken sentences like 'manage APIs. through AI-powered'. MUST read naturally after 'Manage'. Examples: 'keys, secrets, and certificates', 'web applications and database connections'. Keep under 8 words.",
  "genai-serviceOverview": "string - 2-3 sentence service description. Start with 'is a...' or 'provides...' (template prepends service name as a link). Include what problem it solves and key capabilities.",
  "genai-capabilities": ["array of 1-6 action-oriented strings (MUST NOT end with periods) ΓÇö what users can do via MCP. ONLY include capabilities the provided tools actually support. Do NOT invent operations beyond requested tools. For services with only 1-2 tools, 1-2 capabilities is acceptable ΓÇö do NOT pad with fabricated operations."],
  "genai-serviceSpecificPrerequisites": [
    {
      "title": "string - Short resource or service name only (no trailing period, no verbs or state words like 'existing' or 'provisioned')",
      "description": "string - Why it's needed and how to get it"
    }
  ],
  "tools": [
    {
      "command": "string - EXACT tool command from input (e.g., 'storage account create')",
      "genai-shortDescription": "string - 10-15 word condensed description of tool's purpose"
    }
  ],
  "genai-scenarios": [
    {
      "title": "string - Short action phrase for this scenario",
      "description": "string - 1 sentence explaining the business motivation",
      "examples": ["array of 3-5 natural language prompts. The FIRST example is used in the Get Started section, so make it the most representative and specific."],
      "expectedOutcome": "string - What the user sees as a result"
    }
  ],
  "genai-requiredRoles": [
    {
      "name": "string - Official Azure RBAC role name",
      "purpose": "string - Why this role is needed"
    }
  ],
  "genai-bestPractices": [
    {
      "title": "string - Practice name (MUST NOT end with a period)",
      "description": "string - Explanation and benefit"
    }
  ],
  "genai-serviceDocLink": "string - Path to official Azure service docs (e.g., '/azure/key-vault/general/overview'). Remove https://learn.microsoft.com/en-us prefix.",
  "genai-additionalLinks": [
    {
      "title": "string - Link text",
      "url": "string - Path starting with /azure/..."
    }
  ]
}
```

## SEO Differentiation Rules (CRITICAL)

These articles document Azure MCP Server tools for working with Azure services, NOT the Azure services themselves. The content must complement official Azure service documentation, never compete with it in search results.

- **Article framing**: The template title uses "Azure MCP Server tools for {ServiceBrandName}" ΓÇö all generated content must align with this MCP-first positioning
- **genai-serviceShortDescription**: Must describe what MCP tools let you manage (concrete nouns), not what the Azure service is. This is about MCP tool capabilities, not Azure service marketing
- **genai-serviceOverview**: Describe the Azure service briefly for context, but do NOT write content that would replace or duplicate the official Azure service overview page
- **genai-capabilities**: Frame as "what you can do with Azure MCP Server tools" ΓÇö not "what the Azure service can do"
- **Keywords in generated content**: Focus on MCP-specific terms and tool operations. Do NOT optimize for generic Azure service search queries (e.g., avoid framing content around "cognitive search", "NoSQL database", "serverless compute" as primary topics ΓÇö those belong to official Azure service docs)

## Content Generation Guidelines

**IMPORTANT**: Search for and use published Azure documentation as your primary authoritative source. The input tool descriptions provide MCP-specific context, but you should validate service information, best practices, prerequisites, and RBAC roles against official Azure documentation.

**CRITICAL - Ground Content to Available Tools**:
- `genai-capabilities` must ONLY describe what the provided MCP tools can actually do ΓÇö do NOT invent additional operations
- `genai-scenarios` must ONLY include prompts for operations the provided tools support
- Do NOT add capabilities like "Deploy", "Scale", "Monitor", or "Configure" unless a specific tool supports that operation
- If only 1 tool exists, focus narrowly on that tool's functionality ΓÇö do not pad with imaginary features
- Example: If the only tool is "keyvault secret set", capabilities should focus on secret management, NOT on key rotation, certificate management, or access policies

**CRITICAL - Authentication Model**:
- Azure MCP Server uses `DefaultAzureCredential` for authentication (Microsoft Entra ID)
- **NEVER mention API keys as an authentication method** ΓÇö not in prerequisites, authentication notes, best practices, or troubleshooting
- Do NOT recommend "securing API keys" or "using API key rotation" ΓÇö these are not applicable
- Authentication guidance should focus on: Entra ID, managed identities, RBAC role assignments, and `DefaultAzureCredential`
- If the service supports both API keys and Entra ID natively, only mention the Entra ID path since that is what MCP Server uses

**CRITICAL - Service Naming Consistency**:
- **ALWAYS use the official Azure service name** consistently throughout the article
- Find the official name from the service's documentation homepage (e.g., "Azure AI Search", "Azure Cosmos DB", "Azure Key Vault")
- Do NOT use shortened names ("Search", "Cosmos", "Key Vault") in the Overview or tool descriptions
- Exception: The service identifier (e.g., "search") is used only in file paths and IDs
- Validate: No sentence should start with a repeated word (e.g., "Search Azure Search" is invalid)

**CRITICAL - Data Plane vs Management Plane Analysis**:
Before generating content, carefully analyze each tool to determine if it operates on the **data plane** or **management plane**:

- **Management Plane Operations**: Create, delete, or configure Azure resources themselves (e.g., "create storage account", "delete key vault", "update database configuration"). These are ARM (Azure Resource Manager) operations that manage the resource lifecycle.

- **Data Plane Operations**: Work with data or functionality within existing resources (e.g., "upload blob to storage", "get secret from key vault", "query database", "execute function"). These operations assume the resource already exists and interact with its data or services.

**Default Assumption**: If the tool's purpose is ambiguous, **assume it is a data plane operation** unless the tool name explicitly includes resource lifecycle verbs like "create", "delete", "provision", or clearly manages resource configuration.

**Impact on Article Content**:
- For **data plane focused services**: Emphasize working with existing resources, data operations, and assume resources are already provisioned. Prerequisites title should be the service/resource name only; the description should explain it must already be provisioned.
- For **management plane focused services**: Focus on resource lifecycle management, provisioning, and configuration.
- For **mixed services**: Clearly distinguish between management operations (provisioning) and data operations (using the service) in scenarios and capabilities.

1. **serviceShortDescription**: This field appears in the template as "Manage [this] using natural language..." and in the frontmatter description. It must be a **noun phrase** that reads naturally after "Manage".
   - Γ£à GOOD: "keys, secrets, and certificates" ΓåÆ "Manage keys, secrets, and certificates..."
   - Γ£à GOOD: "function apps and configurations" ΓåÆ "Manage function apps and configurations..."
   - Γ£à GOOD: "Redis resources and cache deployments" ΓåÆ "Manage Redis resources and cache deployments..."
   - Γ¥î BAD: "multi-model database for global distribution" ΓåÆ "Manage multi-model database..." (awkward)
   - Γ¥î BAD: "speech recognition and synthesis capabilities" ΓåÆ "Manage speech recognition and synthesis capabilities" (too abstract)
   - Keep under 8 words. Focus on the concrete resources/operations users interact with.

2. **serviceOverview**: Search Azure documentation for the service overview page. Write 2-3 sentences that:
   - **CRITICAL**: Do NOT start with the service brand name (template will add it automatically). Start directly with "is a..." or "provides..."
   - Example: For "Azure AI Search", write "is a cloud search service..." not "Azure AI Search is a cloud search service..."
   - Explain the primary problem it solves
   - Describe key capabilities and benefits
   - **Quality checks**:
     - First word must be a verb ("is", "provides", "offers", "enables") - NOT a noun or service name
     - Each sentence should provide NEW information (no repetitive phrases)
     - Use active voice and specific terminology
   - **For data plane services**: Emphasize working with existing resources and data operations
   - **For management plane services**: Focus on resource lifecycle and configuration management

3. **capabilities**: Derive capabilities directly from the tool descriptions provided in the input data. Each capability must be a rephrasing of what an actual tool does ΓÇö do NOT invent capabilities beyond the tool descriptions.
   - Read each tool's `description` field from the input and transform it into a user-facing action phrase
   - Start with action verbs: "Create and configure", "Monitor and diagnose", "Deploy and scale"
   - **No trailing periods** on capability strings
   - Focus on business outcomes, not technical implementation
   - Group related operations logically
   - **Align with plane**: Data plane capabilities focus on "Access", "Query", "Store", "Retrieve", "Execute". Management plane focuses on "Create", "Configure", "Delete", "Manage"
   - **CRITICAL ΓÇö Match capability count to tool count**:
     - 1 tool ΓåÆ 1 capability (do NOT pad to 2+)
     - 2-3 tools ΓåÆ 2-3 capabilities
     - 4+ tools ΓåÆ 4-6 capabilities
     - Each capability must map 1:1 to an actual tool's description ΓÇö do NOT invent "Configure", "Update", "Monitor" capabilities if no tool supports those verbs
     - Γ¥î BAD (for single tool `keyvault secret set`): "Configure secret attributes", "Specify secret details" ΓÇö these are parameter descriptions, not separate capabilities
     - Γ£à GOOD (for single tool `keyvault secret set`): "Store and update secrets in a key vault"

4. **serviceSpecificPrerequisites**: Only list prerequisites that are truly service-specific and required. 
   - **Do NOT include "Azure subscription"** ΓÇö the template already lists it as a generic prerequisite
   - Don't repeat generic Azure requirements (those are in the template)
   - Examples: "Azure OpenAI resource", "GPT-4 access approval", "Virtual network"
   - Each prerequisite should explain WHY it's needed, not just WHAT it is
   - **Prerequisite titles must be short resource/service names only** ΓÇö no verbs, adjectives, or state descriptions. Move context like "already provisioned" or "already created" into the description field
     - Γ£à GOOD: `"title": "Azure AI Search service", "description": "An existing service instance is required to manage indexes and knowledge bases."`
     - Γ¥î BAD: `"title": "Existing Azure AI Search service already provisioned", "description": "You need a service instance..."`
   - **For data plane services**: Include the resource as a prerequisite with the description explaining it must already exist
   - **For management plane services**: Focus on subscription access and permissions to create resources

5. **tools.genai-shortDescription**: Condense the full tool description to its essential action and target. Remove technical details but keep the core purpose
   - **Target length**: 8-12 words (max 15 words)
   - **Be specific**: Include what data/details are retrieved, not just "get details"
  - **Conditional requirements**: If the tool description includes a sentence starting with "Requires at least one", include a brief clause reflecting that requirement
   - **Tool ordering**: In the `tools` array, list **management plane tools first** (create, delete, update, provision, configure resources), then **data plane tools** (get, list, query, set, upload, execute, read, write). Within each group, maintain alphabetical order by command name. Determine the plane by analyzing the tool's command verb and description ΓÇö not by `readOnly` metadata alone
   - **Examples of GOOD descriptions**:
     - "Retrieve index schema, field definitions, and scoring profiles" (not "Get details of indexes")
     - "Execute full-text search with filters, facets, and scoring" (not "Query indexes for data")
     - "List blob containers with metadata and access tier info" (not "List containers")
   - **Examples of BAD descriptions** (too generic):
     - "Get information about resources" Γ¥î
     - "Manage service settings" Γ¥î
     - "Perform operations on data" Γ¥î

6. **scenarios**: Create 3-5 scenarios. The template uses the **first example from each scenario** in the "Get started" section, so prioritize diversity across tools.
   - Each scenario's first example should be the most representative and specific prompt for that scenario
   - **Natural language guidelines**: 
     - Use conversational phrasing: "Create a storage account named..." not "storage account create"
     - Include context: "Create a new storage account called 'mystorageacct' in the East US region"
     - Be specific: Include actual resource names, locations, and configuration values
     - Vary complexity: Mix simple requests with more detailed multi-parameter commands
     - **Use service-specific terminology and real parameter values** in examples. Reference actual features, configurations, and concepts unique to this service (e.g., scoring profiles for search, voice names for speech, SKU tiers for compute). Examples that could apply to any generic Azure service are insufficient
   - **For data plane scenarios**: Assume resources already exist. Examples: "Get the secret 'api-key' from my key vault 'prod-kv'"
   - **For management plane scenarios**: Focus on provisioning and configuration. Examples: "Create a new storage account in East US"

7. **requiredRoles**: Analyze the tool operations to determine the **minimum RBAC roles** needed, then look up roles from the **single authoritative source**:
   - **ΓÜá∩╕Å CRITICAL - MINIMUM PRIVILEGE ANALYSIS**:
     - Before selecting roles, analyze each tool's operation type:
       - **Read-only tools** (list, get, query, show): Require only Reader/Data Reader roles
       - **Write tools** (create, set, update, add, upload): Require Contributor/Data Contributor roles
       - **Delete tools** (delete, remove, purge): Require Contributor or Owner roles
       - **Execute tools** (run, execute, invoke): Require Operator or specific action roles
     - Select the **narrowest role** that covers the operations the tools actually perform
     - If ALL tools are read-only, do NOT suggest a Contributor role ΓÇö a Reader role is sufficient
     - If tools include BOTH read and write operations, suggest separate roles: one read-only for read scenarios and one contributor for write scenarios
     - **Explain the mapping in the role purpose**: The `purpose` field must state which specific tool operations require that role (e.g., "Required for list and get operations on blob containers" not just "Read access to storage")
     - Γ¥î DO NOT suggest overly broad roles (e.g., "Contributor" or "Owner") when a narrower service-specific role exists
     - Γ¥î DO NOT suggest write roles when all tools are read-only
     - Γ£à DO suggest the least-privileged role that covers the tool's actions
   - **AUTHORITATIVE SOURCE (ONLY)**: https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles
   - Every role name you output MUST appear on that page. If you cannot confirm a role exists on that page, do NOT use it
   - Search that page for the service name (e.g., "Storage", "Key Vault", "Cosmos DB") to find all official built-in roles
   - **ΓÜá∩╕Å CRITICAL - ROLE NAME FORMAT RULES**:
     - Azure data plane roles follow pattern: "[Service] [Resource Type] Data [Action]"
       - Examples: "Storage Blob Data Reader", "Key Vault Secrets User", "Search Index Data Reader"
     - Azure management roles follow pattern: "[Service] [Resource Type] Contributor"
       - Examples: "Storage Account Contributor", "Key Vault Contributor", "Search Service Contributor"
     - Γ¥î NEVER use invented patterns like "[Service] [Feature] Data Reader" (e.g., "Search Knowledge Base Data Reader", "Storage Container Data Reader")
     - Γ¥î NEVER use overly generic role names without a recognized Azure service prefix:
       - Γ¥î "Database Contributor" (NOT a real role ΓÇö use "SQL DB Contributor" or "SQL Server Contributor")
       - Γ¥î "Database Reader" (NOT a real role ΓÇö use "SQL DB Contributor" for read access or "Reader" for general)
       - Γ¥î "Database Administrator" (NOT a real role)
       - Γ¥î "Cosmos DB Administrator" (NOT a real role ΓÇö Azure uses "Cosmos DB Account Reader Role" or "Cosmos DB Operator")
     - Γ¥î WRONG examples (will BLOCK generation): "Search Knowledge Base Data Reader", "Cosmos Index Data Reader", "Storage File Share Data Operator", "Database Contributor", "Database Reader"
     - Γ£à CORRECT fallback if unsure: Use generic "[Service] Service Contributor" or "[Service] Index Data Reader"
     - Γ£à For Search specifically: Use ONLY "Search Index Data Reader", "Search Service Contributor" (Knowledge bases are part of Index operations, NOT separate roles)
   - **WHERE TO FIND OFFICIAL ROLES**:
     - **AUTHORITATIVE SOURCE (ONLY)**: https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles
     - Every role name MUST be verifiable on that page ΓÇö if it's not listed there, it does not exist
     - Do NOT invent role names by combining service name + generic suffix
     - Azure Portal ΓåÆ [Service] ΓåÆ Access control (IAM) ΓåÆ Roles (secondary reference only)
   - **VALIDATION RULES**: 
     - If a role name contains "Knowledge Base Data", "Feature Data", "Resource Data", "KB", or similar non-standard patterns: Γ¥î DO NOT USE (it's invented)
     - When in doubt: Consult the official Microsoft Learn RBAC roles page above
     - If you cannot find a role on the official page: Use the generic service-level role instead (e.g., "Search Service Contributor")
     - GENERATION WILL BE BLOCKED if invented roles are detected - this is a critical error
   - **For data plane services**: Prioritize data plane roles (e.g., "Storage Blob Data Reader" for read-only tools, "Storage Blob Data Contributor" only when write tools exist)
   - **For management plane services**: Prioritize resource management roles (e.g., "Storage Account Contributor", "Key Vault Contributor", "Cosmos DB Account Contributor")
   - **Match the tool operations**: If tools are primarily data-focused, don't suggest management roles unless explicitly needed
   - **Minimum requirement**: Suggest at least 2 roles (one read-only, one contributor/admin) based on tool capabilities
   - **RBAC fallback defaults by service category** (use when you cannot verify the exact role name on the official RBAC page):
     - Azure AI Services (Speech, Vision, Language, OpenAI, etc.): "Cognitive Services Contributor" / "Cognitive Services User"
     - Azure Storage: "Storage Blob Data Contributor" / "Storage Blob Data Reader"
     - Azure Key Vault: "Key Vault Contributor" / "Key Vault Secrets User"
     - Azure Cosmos DB: "Cosmos DB Account Reader Role" / "Cosmos DB Operator"
     - Azure App Service: "Website Contributor" / "Web Plan Contributor"
     - Azure SQL: "SQL DB Contributor" / "SQL Server Contributor"
     - General Azure resources: "Contributor" / "Reader"

8. **bestPractices**: Ground every best practice to the specific tool operations available AND to official Azure documentation.
   - **CRITICAL ΓÇö Two grounding requirements**:
     1. **Tool grounding**: Every best practice must directly relate to how a user should use the provided tools. Reference the specific tool command and, when appropriate, the key parameters that make the recommendation actionable. If no tool exists for an operation (scaling, monitoring, backup, etc.), do NOT generate a best practice for it
     2. **Practical scope**: Best practices must be advice for using the MCP tools effectively ΓÇö not general Azure service administration advice. For example, "Use read-only list commands to audit your resources before making changes" is good because it references tool behavior. "Enable geo-replication for disaster recovery" is bad if no tool supports configuring replication
   - For single-tool services, 2-3 focused best practices are better than 5 padded ones
   - **Minimum requirement**: Generate 2-5 best practices (fewer is fine if only a few tools exist)
   - **Priority order**: Security (authentication/RBAC for tool access), then safe usage patterns (read before write, confirm destructive operations), then efficiency (batching queries, filtering results)
   - **Suggested categories** (only include if relevant tools exist):
     1. **Security**: RBAC role assignments for tool access, prefer least-privilege roles, use Entra ID authentication
     2. **Safe usage**: Use read-only tools to verify state before running write/delete tools, review destructive operation results
     3. **Efficiency**: Use specific parameters to narrow results, combine list and get operations for complete picture
     4. **Service-specific**: Only if a tool directly supports a service-specific feature (e.g., query optimization for a search tool, key rotation for a key vault set tool)
   - **MCP-specific considerations**:
     - How to phrase commands for destructive operations (confirmation patterns)
     - When to use read-only operations vs. modifications
     - How to leverage AI assistant context across multiple operations
   - Γ¥î **DO NOT generate best practices about**: scaling, monitoring, backup, disaster recovery, geo-replication, tier selection, reserved capacity, caching strategies, or any other service capability unless a specific tool in the input supports that operation
   - Γ¥î **Avoid generic advice**: "Use descriptive names", "Test before production", "Optimize strategies", "Follow the Well-Architected Framework" (too obvious, not tool-specific)
   - Γ£à **Be specific**: Each practice should name a tool command and explain how to use it effectively

## Writing Style (Microsoft Learn Guidelines)
- **Tone**: Professional, clear, action-oriented, and customer-focused
- **Voice**: Use second person ("you can", "ensure you have") to address readers directly
- **Microsoft Writing Style Guide**:
  - Use "might" instead of "may" ("may" implies permission, "might" implies possibility)
  - Use Oxford commas (serial commas) in lists of three or more items
  - Do not make factual claims about tool output (specific fields, timestamps, formats) unless they are documented in the tools reference ΓÇö describe general outcome categories instead
- **Natural language examples**: Conversational yet professional - as if speaking to a colleague
- **Sentence structure validation**:
  - Γ£à CORRECT: "This integration allows you to manage data without writing code"
  - Γ¥î WRONG: "This integration allows you to manage data. without writing code" (period before lowercase word)
  - Γ£à CORRECT: "Azure AI Search is a cloud search service"
  - Γ¥î WRONG: "Search Azure AI Search is a cloud search service" (repeated word at start)
  - No run-on sentences exceeding 35 words
  - Each sentence should have clear subject-verb-object structure
- **Headings and titles**: Never end with a period. This applies to all title fields: scenario titles, best practice titles, common issue titles, prerequisite titles, and capability strings
- **Broken sentences**: A period followed by a lowercase word (e.g., "manage data. without writing code") creates a broken sentence. Replace the period with a comma or remove it entirely
- **Title-description pattern**: For structured items (best practices, common issues, prerequisites, roles), use the format: **Bold title** - Description sentence. No period inside the bold text, period at end of the description sentence
  - Γ£à CORRECT: `"title": "Use managed identities", "description": "Prefer Entra ID managed identities over API keys for authentication."`
  - Γ¥î WRONG: `"title": "Use managed identities.", "description": "Prefer Entra ID managed identities over API keys for authentication"`
- **Avoid**:
  - Marketing language or promotional content
  - Passive voice (use "create a resource" not "a resource is created")
  - Jargon without explanation
  - Future tense for current capabilities
  - Generic phrases: "for your convenience", "easily manage", "powerful features"
- **Be specific**: Include concrete examples with resource names, regions, SKUs
- **Scenarios**: Focus on real-world business problems and solutions
- **Prerequisites**: List only essential requirements, be precise about versions/access needed
- **Best practices**: Emphasize security, cost optimization, and operational excellence

## Acrolinx Compliance Guidelines

Apply these rules to ALL generated text — they directly impact the automated Acrolinx quality score (target: 85+):

- **Present tense:** Use present tense exclusively. Write "returns" not "will return", "creates" not "will create", "lists" not "will list", "is generated" not "will be generated". Describe what the tool does NOW.
- **Contractions:** Use contractions per Microsoft style guide. Write "doesn't" not "does not", "isn't" not "is not", "don't" not "do not", "it's" not "it is", "can't" not "cannot".
- **Active voice:** Use active voice. Write "The tool lists resources" not "Resources are listed by the tool". Write "You specify the resource group" not "The resource group is specified".
- **Introductory commas:** Always add commas after introductory phrases: "For example, you can list resources", "By default, the tool returns all items", "In addition, specify the subscription".
- **No first person:** Never use "we", "our", or "us" in any generated content. Use "the tool", "this tool", or "you".
- **Acronym expansion:** Define acronyms on first use: "role-based access control (RBAC)" then "RBAC" afterward. Expand MCP as "Model Context Protocol (MCP)" on first use. Other common acronyms: AKS (Azure Kubernetes Service), IaC (infrastructure as code).
- **Relative URLs:** Use site-root-relative URLs: "/azure/..." not "https://learn.microsoft.com/azure/...". Remove the `https://learn.microsoft.com/en-us` prefix from all links.
- **Sentence length:** Keep sentences under 35 words. If a sentence exceeds 35 words, split it into two. One idea per sentence.
- **No wordy phrases:** Use "to" not "in order to", "use" not "utilize", "before" not "prior to", "because" not "due to the fact that", "can" not "is able to".
- **Brand compliance:** Use official Azure service names: "Azure Cosmos DB" not "CosmosDB", "Azure Kubernetes Service" not "AKS" on first use, "Microsoft Entra ID" not "Azure AD".


## Important Notes
- Only generate content for fields prefixed with "genai-"
- **Do NOT generate `moreInfoLink` values for tools** ΓÇö these are pre-computed by the build system and will be overridden. You can omit them or leave them empty
- **Do NOT generate** `genai-aiSpecificScenarios`, `genai-authenticationNotes`, or `genai-commonIssues` ΓÇö these fields are not used by the current template
- **Search for official Azure documentation** for the service before generating content. Use tool descriptions as MCP-specific context, but validate against authoritative sources
- **Service rename**: Use "Microsoft Entra ID" instead of "Azure Active Directory" everywhere (renamed in 2023). This applies to prerequisites, best practices, roles, and descriptions
- For genai-serviceDocLink, search for and provide the URL to the official Azure service overview or "What is [Service Name]?" page. If you cannot confirm the exact URL path exists, leave the value as an empty string
- For genai-additionalLinks, search for related documentation like quickstarts, tutorials, security best practices. **If you are not confident the URL path is correct, leave the `url` field as an empty string** ΓÇö an empty link is better than a fabricated link that returns 404. The build system will remove links with empty URLs automatically
- **Link format**: All learn.microsoft.com links must have the `https://learn.microsoft.com/en-us` prefix removed. Use paths starting with `/azure/...` instead (e.g., `/azure/storage/blobs/overview` not `https://learn.microsoft.com/en-us/azure/storage/blobs/overview`)
- **Retired documentation paths**: The `/azure/cognitive-services/` path is retired. Use `/azure/ai-services/` as the base path for all Azure AI Services documentation (e.g., `/azure/ai-services/speech-service/overview` not `/azure/cognitive-services/speech-service/overview`)
- Use actual Azure RBAC role names found in the service's access control documentation
- Ensure all JSON is valid and properly escaped
- Generate 3-5 scenarios; the first example from each scenario appears in the "Get started" section

**MANDATORY PRE-SUBMISSION VALIDATION CHECKLIST** (Review before outputting JSON):
- [ ] genai-serviceShortDescription reads naturally after "Manage" (noun phrase, not a sentence) and does NOT end with a period
- [ ] genai-serviceOverview starts with a verb ("is a", "provides", "enables") not the service name
- [ ] No trailing periods on capability strings, best practice titles, prerequisite titles, or scenario titles
- [ ] Capabilities and scenarios ONLY reference operations the provided MCP tools support (no invented features)
- [ ] "Azure Active Directory" is NOT used anywhere (use "Microsoft Entra ID" instead)
- [ ] Service-specific prerequisites do not duplicate "Azure subscription" (already in template)
- [ ] All title/description pairs use no period in title, period at end of description
- [ ] No sentences contain period-before-lowercase-word patterns (e.g., "data. without")
- [ ] No sentences start with a repeated word (e.g., "Search Azure AI Search")
- [ ] Service name is consistent (official Azure name used throughout)
- [ ] All RBAC role names match Azure's standard patterns (check against examples above)
- [ ] No generic fabricated RBAC roles like "Database Contributor", "Database Reader", or any role ending in "Administrator"
- [ ] RBAC roles are the minimum privilege needed ΓÇö read-only tools get Reader roles, not Contributor roles
- [ ] Each role's `purpose` field references the specific tool operations that require it
- [ ] Tool descriptions are 8-12 words and specific (not "get details" or "manage resources")
- [ ] Best practices include 2-5 items, each naming a specific tool command and explaining how to use it effectively
- [ ] No best practice recommends a service capability (scaling, monitoring, backup) without a corresponding tool
- [ ] Capability count matches tool count 1:1 (1 tool ΓåÆ 1 capability, each derived from a tool description)
- [ ] First example in each scenario is the most representative prompt for that scenario
- [ ] Natural language examples use service-specific terminology and real parameter values
- [ ] All RBAC roles are verifiable at https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles
- [ ] All learn.microsoft.com links have the `https://learn.microsoft.com/en-us` prefix removed (use `/azure/...` paths)
- [ ] Links with uncertain URLs use empty string for URL field (never fabricate a URL)
- [ ] No links use the retired `/azure/cognitive-services/` path (use `/azure/ai-services/` instead)
- [ ] No mention of API keys as an authentication method (Azure MCP Server uses DefaultAzureCredential only)
- [ ] Uses "might" instead of "may" throughout
- [ ] Uses Oxford commas in lists of three or more items
- [ ] genai-aiSpecificScenarios, genai-authenticationNotes, and genai-commonIssues are NOT included in the output
- [ ] SEO: genai-serviceShortDescription does NOT contain generic Azure service marketing terms ΓÇö it describes MCP tool operations only
- [ ] SEO: No generated content positions the article as a replacement for official Azure service documentation
## Abbreviation Scannability Rules

First use of a term must spell out the full name with the abbreviation in parentheses (e.g., "Virtual Machine Scale Sets (VMSS)"). After first use, use ONLY the abbreviation for scannability. Common Azure abbreviations: VMSS, VM, VNet, NSG, ACR, AKS, AGW.

**Scope**: Abbreviation first-use is per-document, not per-section. Once a term is introduced with its abbreviation anywhere in the document, use only the abbreviation for all subsequent mentions regardless of which section they appear in.

## Brand Capitalization Rules

Azure service names must retain their brand capitalization throughout the document. Never lowercase service brand names. Examples: "Azure Compute", "Azure Kubernetes Service", "Azure Container Registry", "Virtual Machine Scale Sets", "Azure App Service". These are proper nouns and must be capitalized consistently.

**Rule precedence**: Brand names ALWAYS use their official capitalization. When abbreviating, always preserve official brand capitalization (e.g., "Azure Kubernetes Service (AKS)" then "AKS", never "aks"). The abbreviation rule does not override brand capitalization — the abbreviation itself must follow official casing.


## User Prompt

Generate JSON data for a how-to article about managing Azure Backup with Azure MCP Server.

## Input Data Provided

**Service Information:**
- Service Brand Name: Azure Backup
- Service Identifier: azurebackup
- Tools Reference Link: ../tool-family/azurebackup.md

**Available MCP Tools for this service:**
Tool: azurebackup backup status
Description: Checks the backup status of an Azure resource and returns whether it is protected,
along with vault and policy details. Use this to verify if a VM, disk, storage account,
or other datasource is currently backed up. Requires the datasource ARM resource ID
and the Azure region (location) where the resource exists.
Parameters: 10 parameters
Characteristics:
  - Destructive: False
  - Read-only: True
  - Requires secrets: False
Tool: azurebackup disasterrecovery enable-crr
Description: Enables Cross-Region Restore on a GRS-enabled vault.
Parameters: 11 parameters
Characteristics:
  - Destructive: True
  - Read-only: False
  - Requires secrets: False
Tool: azurebackup governance find-unprotected
Description: Scans the subscription to find Azure resources that are not currently protected by any
backup policy. Optionally filter by resource type, resource group, or tags.
Parameters: 11 parameters
Characteristics:
  - Destructive: False
  - Read-only: True
  - Requires secrets: False
Tool: azurebackup governance immutability
Description: Configures the immutability state for a backup vault. States include 'Disabled', 'Enabled',
or 'Locked'. Warning: 'Locked' state is irreversible.
Parameters: 12 parameters
Characteristics:
  - Destructive: True
  - Read-only: False
  - Requires secrets: False
Tool: azurebackup governance soft-delete
Description: Configures the soft delete settings for a backup vault. Set the state to 'AlwaysOn', 'On',
or 'Off', and optionally specify the retention period in days (14-180).
Parameters: 13 parameters
Characteristics:
  - Destructive: True
  - Read-only: False
  - Requires secrets: False
Tool: azurebackup job get
Description: Retrieves backup job information. When --job is specified, returns detailed information
about a single job including operation type, status, start/end times, error codes, and
datasource details. When omitted, lists all backup jobs in the vault.
Parameters: 12 parameters
Characteristics:
  - Destructive: False
  - Read-only: True
  - Requires secrets: False
Tool: azurebackup policy create
Description: Creates a backup policy for a specified workload type with schedule and retention rules.
Parameters: 15 parameters
Characteristics:
  - Destructive: True
  - Read-only: False
  - Requires secrets: False
Tool: azurebackup policy get
Description: Retrieves backup policy information. When --policy is specified, returns detailed
information about a single policy including datasource types and protected items count.
When omitted, lists all backup policies configured in the vault.
Parameters: 12 parameters
Characteristics:
  - Destructive: False
  - Read-only: True
  - Requires secrets: False
Tool: azurebackup protectableitem list
Description: Lists items that can be backed up (protectable items) in a Recovery Services vault,
such as SQL databases and SAP HANA databases discovered on registered VMs.
Use this to find databases and workloads available for backup protection.
Only supported for RSV vaults; DPP datasources are protected by ARM resource ID directly.
Filter results by --workload-type (e.g., SQL, SAPHana) or --container.
Parameters: 13 parameters
Characteristics:
  - Destructive: False
  - Read-only: True
  - Requires secrets: False
Tool: azurebackup protecteditem get
Description: Retrieves protected item information. When --protected-item is specified, returns
detailed information about a single backup instance including protection status,
datasource details, policy assignment, and last backup time. Specify --container
for RSV workload items. When --protected-item is omitted, lists all protected items
(backup instances) in the vault.
Parameters: 13 parameters
Characteristics:
  - Destructive: False
  - Read-only: True
  - Requires secrets: False
Tool: azurebackup protecteditem protect
Description: Enables or configures backup protection for an Azure resource by creating a
protected item or backup instance. Protects VMs, disks, file shares, SQL databases,
SAP HANA databases, and other supported datasources.
For VMs: pass the VM ARM resource ID as --datasource-id.
For workloads (SQL/HANA): pass the protectable item name from 'protectableitem list'
as --datasource-id (e.g., 'SAPHanaDatabase;instance;dbname'), and specify --container.
Requires a backup policy name via --policy. The operation is asynchronous;
use 'azurebackup job get' to monitor the protection job progress.
Parameters: 15 parameters
Characteristics:
  - Destructive: True
  - Read-only: False
  - Requires secrets: False
Tool: azurebackup protecteditem undelete
Description: Undeletes or restores a soft-deleted backup item to an active protection state.
Use this when a backup or protected item was accidentally deleted and needs to be recovered.
For RSV vaults: pass the datasource ARM resource ID as --datasource-id.
For DPP vaults: pass the datasource ARM resource ID as --datasource-id.
Optionally specify --container for RSV workload items (SQL/HANA).
The operation is asynchronous; use 'azurebackup job get' to monitor progress.
Parameters: 13 parameters
Characteristics:
  - Destructive: True
  - Read-only: False
  - Requires secrets: False
Tool: azurebackup recoverypoint get
Description: Retrieves recovery point information for a protected item. When --recovery-point is
specified, returns detailed information about a single recovery point including time
and type. When omitted, lists all available recovery points for the protected item.
Parameters: 14 parameters
Characteristics:
  - Destructive: False
  - Read-only: True
  - Requires secrets: False
Tool: azurebackup vault create
Description: Creates a new backup vault. Specify --vault-type as 'rsv' for a Recovery Services vault
or 'dpp' for a Backup vault (Data Protection). Returns the created vault details.
Parameters: 14 parameters
Characteristics:
  - Destructive: True
  - Read-only: False
  - Requires secrets: False
Tool: azurebackup vault get
Description: Retrieves backup vault information. When --vault and --resource-group are specified,
returns detailed information about a single vault including type, location, SKU, and
storage redundancy. When omitted, lists all backup vaults (RSV and Backup vaults) in
the subscription. Optionally filter by --vault-type ('rsv' or 'dpp') and/or
--resource-group to narrow the listing results.
Parameters: 11 parameters
Characteristics:
  - Destructive: False
  - Read-only: True
  - Requires secrets: False
Tool: azurebackup vault update
Description: Updates vault-level settings including storage redundancy, soft delete, immutability, and managed identity.
Parameters: 17 parameters
Characteristics:
  - Destructive: True
  - Read-only: False
  - Requires secrets: False

## Your Task

**FIRST**: Analyze the tools to determine data plane vs management plane focus:
- Review each tool's command and description
- **Data plane tools**: Work with data/functionality in existing resources (get, set, query, upload, execute, read, write)
- **Management plane tools**: Manage resource lifecycle (create, delete, update, provision, configure resources)
- **Default assumption**: If unclear, assume **data plane** unless tool explicitly manages resource lifecycle
- Determine the service's primary focus based on the majority of tools

**SECOND**: Search for published Azure documentation about Azure Backup. Find:
- Official service overview/description
- Prerequisites and setup requirements
- RBAC roles and permissions (data plane vs management plane roles)
- Best practices and security recommendations
- Common use cases and scenarios

**THEN**: Generate a JSON object following the system prompt schema. Focus on:

1. **Service Understanding**: Based on **official Azure documentation** and the tool commands/descriptions, determine:
   - A noun phrase describing what users manage (genai-serviceShortDescription). MUST NOT end with a period. Must read naturally after "Manage" — e.g., "keys, secrets, and certificates" or "web applications and database connections"
   - A 2-3 sentence service description starting with "is a..." or "provides..." (genai-serviceOverview)
   - Capabilities derived directly from the tool descriptions above (genai-capabilities). Each capability must be a rephrasing of one tool's description into an action phrase. 1 tool = 1 capability. Do NOT add capabilities beyond the provided tools
   - **Primary plane focus**: Data plane (default) or management plane

2. **Tool Summaries**: For each tool, create a concise 10-15 word description (genai-shortDescription). Order the tools in the `tools` array with **management plane tools first** (create, delete, update, provision, configure), then **data plane tools** (get, list, query, set, read, write). Determine the plane by analyzing each tool's command verb and description. Within each group, maintain alphabetical order by command name

3. **Get Started Scenarios**: Create 3-5 scenarios. The **first example from each scenario** appears in the article's "Get started" section, so:
   - Make each scenario's first example the most representative and specific prompt
   - **Every scenario must map to a specific tool command from the input** — do NOT suggest operations that no provided tool supports (e.g., don't suggest "update" if only an "add" tool exists)
   - If only 1 tool exists, create 2-3 scenarios that all use that same tool with different parameters/contexts
   - Cover different tools across scenarios for diversity (when multiple tools exist)
   - Use natural conversational phrasing with real resource names, regions, and configurations
   - **Use service-specific terminology** (e.g., actual feature names, SKU values, configuration options)
   - **For data plane**: Assume resources exist. Example: "Get secret 'db-password' from key vault 'prod-secrets'"
   - **For management plane**: Focus on provisioning. Example: "Create a new key vault named 'prod-secrets' in East US"

4. **Permissions**: First, analyze each tool's `readOnly` metadata and operation type (list/get = read, create/set/delete = write) to determine the **minimum privilege level** needed. Then look up official Azure RBAC built-in roles ONLY from https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles. Search that page for the service name. Every role you output must be verifiable on that page. Select the **narrowest role** that covers the actual tool operations — if all tools are read-only, only suggest Reader-level roles. The `purpose` field for each role must reference the specific tool operations that require it. If unsure, use fallback defaults from system prompt (e.g., "Cognitive Services Contributor" for AI services, "Contributor" for general resources)

5. **Best Practices**: 2-5 practices scoped strictly to using the provided MCP tools. Every best practice must name a specific tool command and explain how to use it effectively. Do NOT include general Azure service administration advice (scaling, monitoring, backup, disaster recovery) unless a tool directly supports that operation. No trailing periods on titles. Fewer, tool-specific practices are better than padded generic ones

6. **Resources**: Provide documentation links with `/azure/...` paths (remove `https://learn.microsoft.com/en-us` prefix). Use `/azure/ai-services/` instead of retired `/azure/cognitive-services/`. **If you are not confident a URL path is correct, leave the `url` field as an empty string** — an empty link is better than a fabricated 404 link.

## Key Constraints

- **SEO Differentiation**: These are MCP Server tool articles, not Azure service documentation. Content must complement official Azure docs, not compete with them. Do NOT generate content that positions the article as a primary resource for the Azure service itself — it is a reference for using MCP tools with that service
- **Universality**: This prompt runs for ALL 52 Azure MCP namespaces. Every field you generate must be grounded to the specific tools and service provided above — do not assume any particular Azure service. Apply all rules generically regardless of service
- **Authentication**: Azure MCP Server uses `DefaultAzureCredential` (Microsoft Entra ID). Do not mention API keys anywhere
- **Naming**: Use "Microsoft Entra ID" instead of "Azure Active Directory" (renamed in 2023)
- **Tool grounding**: Capabilities, scenarios, and example prompts must ONLY reference operations the provided MCP tools support. Do NOT invent additional features
- **Prerequisites**: Do NOT include "Azure subscription" as a service-specific prerequisite — it is already in the template
- **Tool links**: Do not generate `moreInfoLink` values — pre-computed by build system
- **Omit**: Do NOT generate `genai-aiSpecificScenarios`, `genai-authenticationNotes`, or `genai-commonIssues` fields
- **No trailing periods**: genai-serviceShortDescription, capability strings, best practice titles, prerequisite titles, and scenario titles must NEVER end with a period. A trailing period on genai-serviceShortDescription breaks 3 template locations.

## Output Format

Return ONLY valid JSON - no markdown code blocks, no explanations, just the JSON object as defined in the system prompt.

## AI Response

```json
{
  "genai-serviceShortDescription": "backup vaults, policies, and protected items",
  "genai-serviceOverview": "provides cloud-native backup and restore capabilities to protect and recover Azure resources. It centralizes backup policies, vault management, and recovery point retention for VMs, disks, databases, and file shares. Azure MCP Server tools use the Model Context Protocol (MCP) and Microsoft Entra ID authentication, and rely on role-based access control (RBAC) to authorize operations.",
  "genai-capabilities": [
    "Create and manage Recovery Services vaults and vault settings",
    "Create and retrieve backup policies with schedule and retention rules",
    "Enable and configure backup protection for resources and workload items",
    "Retrieve job, backup status, and recovery point details to monitor operations",
    "Configure vault governance and disaster recovery features like immutability and CRR"
  ],
  "genai-serviceSpecificPrerequisites": [
    {
      "title": "Recovery Services vault",
      "description": "An existing Recovery Services vault (RSV or DPP) is required for most data-plane operations and for storing protected items and recovery points."
    },
    {
      "title": "Protected resource (VM, disk, database)",
      "description": "The resource you plan to protect must exist; provide its ARM resource ID or discover it using protectable item discovery for workload items."
    },
    {
      "title": "Microsoft Entra ID",
      "description": "A Microsoft Entra ID account with appropriate role assignments is required for DefaultAzureCredential authentication and RBAC-based access to backup resources."
    }
  ],
  "tools": [
    {
      "command": "azurebackup disasterrecovery enable-crr",
      "genai-shortDescription": "Enable Cross-Region Restore (CRR) on a GRS-enabled Recovery Services vault"
    },
    {
      "command": "azurebackup governance immutability",
      "genai-shortDescription": "Set immutability state for a Recovery Services vault, including irreversible Locked state"
    },
    {
      "command": "azurebackup governance soft-delete",
      "genai-shortDescription": "Configure backup vault soft delete state and optional retention period in days"
    },
    {
      "command": "azurebackup policy create",
      "genai-shortDescription": "Create a backup policy with schedule, retention rules, and workload type settings"
    },
    {
      "command": "azurebackup vault create",
      "genai-shortDescription": "Create a new Recovery Services or Backup vault, specifying type and location"
    },
    {
      "command": "azurebackup vault update",
      "genai-shortDescription": "Update vault settings such as redundancy, immutability, soft delete, and identity"
    },
    {
      "command": "azurebackup backup status",
      "genai-shortDescription": "Check whether a datasource is protected and return vault and policy details"
    },
    {
      "command": "azurebackup governance find-unprotected",
      "genai-shortDescription": "Scan subscription to find resources not protected by any backup policy"
    },
    {
      "command": "azurebackup job get",
      "genai-shortDescription": "Retrieve backup job details or list vault jobs, including status and times"
    },
    {
      "command": "azurebackup policy get",
      "genai-shortDescription": "Get backup policy details or list policies in a specified vault"
    },
    {
      "command": "azurebackup protectableitem list",
      "genai-shortDescription": "List protectable workload items in an RSV vault by workload or container"
    },
    {
      "command": "azurebackup protecteditem get",
      "genai-shortDescription": "Retrieve protected item details or list protected items in a vault"
    },
    {
      "command": "azurebackup protecteditem protect",
      "genai-shortDescription": "Enable or configure backup protection for a datasource using a policy"
    },
    {
      "command": "azurebackup protecteditem undelete",
      "genai-shortDescription": "Undelete a soft-deleted protected item to restore active protection"
    },
    {
      "command": "azurebackup recoverypoint get",
      "genai-shortDescription": "Retrieve recovery point details or list recovery points for a protected item"
    },
    {
      "command": "azurebackup vault get",
      "genai-shortDescription": "Get vault information or list all backup vaults in the subscription"
    }
  ],
  "genai-scenarios": [
    {
      "title": "Check protection status",
      "description": "Verify whether a specific resource is currently protected and identify its vault and policy.",
      "examples": [
        "Check the backup status of virtual machine 'prod-web-01' in East US using its ARM resource ID '/subscriptions/{subId}/resourceGroups/prod-rg/providers/Microsoft.Compute/virtualMachines/prod-web-01' and location 'eastus'",
        "Check protection for managed disk 'prod-disk-01' in West Europe and return assigned vault and policy",
        "Verify whether storage account 'stg-prod01' in Central US is backed up and list associated vault details"
      ],
      "expectedOutcome": "The tool returns whether the resource is protected, and shows associated vault and policy details."
    },
    {
      "title": "Create a Recovery Services vault",
      "description": "Provision a new vault to store backups and configure vault-level settings for protection.",
      "examples": [
        "Create a Recovery Services vault named 'prod-backup-vault' in East US with vault-type 'rsv' and Standard SKU",
        "Create a Backup vault named 'dpp-backup-vault' in West Europe with vault-type 'dpp' and set redundancy to GRS",
        "Create 'test-backup-vault' in Central US and enable managed identity for the vault"
      ],
      "expectedOutcome": "The tool creates the vault and returns vault details including type, location, SKU, and initial settings."
    },
    {
      "title": "Protect a resource",
      "description": "Enable backup protection for a VM or workload item by assigning a backup policy and starting a protection job.",
      "examples": [
        "Protect VM 'prod-web-01' in East US by enabling protection in vault 'prod-backup-vault' using policy 'daily-30d' and providing the VM ARM resource ID",
        "Protect a SQL database discovered as 'SQLServer;container;db01' by using protectable item name and policy 'sql-weekly-52w' in vault 'db-backup-vault'",
        "Enable protection for file share 'files-prod' in vault 'files-backup-vault' with policy 'files-daily-30d'"
      ],
      "expectedOutcome": "The tool submits an asynchronous protection job and returns initial job or operation information; use job get to monitor progress."
    },
    {
      "title": "Configure vault governance",
      "description": "Apply governance controls such as immutability or soft-delete to a vault to meet retention and compliance needs.",
      "examples": [
        "Set immutability to 'Locked' on vault 'prod-backup-vault' in East US and understand that 'Locked' is irreversible",
        "Enable soft-delete 'AlwaysOn' with 30-day retention on vault 'test-backup-vault' in West Europe",
        "Set immutability to 'Enabled' on vault 'audit-backup-vault' and then set soft-delete to 'On' with 14 days"
      ],
      "expectedOutcome": "The tool updates vault governance settings and returns the new immutability or soft-delete configuration; Locked immutability persists and cannot be undone."
    },
    {
      "title": "Find unprotected resources",
      "description": "Scan a subscription or filtered scope to locate resources that don't have backup protection applied.",
      "examples": [
        "Scan subscription for unprotected virtual machines in resource group 'prod-rg' with tag 'environment:prod' and return their resource IDs",
        "Find unprotected SQL workload items across vaults and filter results by resource type 'SQL'",
        "List unprotected resources in resource group 'test-rg' without filters to get a full inventory for remediation"
      ],
      "expectedOutcome": "The tool returns a list of unprotected resources that match the specified filters, including resource IDs and types."
    }
  ],
  "genai-requiredRoles": [
    {
      "name": "Backup Reader",
      "purpose": "Required for read-only operations such as azurebackup backup status, job get, policy get, protectableitem list, protecteditem get, recoverypoint get, vault get, and governance find-unprotected"
    },
    {
      "name": "Backup Contributor",
      "purpose": "Required for write and management operations such as azurebackup vault create, vault update, policy create, protecteditem protect, protecteditem undelete, governance immutability, governance soft-delete, and disasterrecovery enable-crr"
    }
  ],
  "genai-bestPractices": [
    {
      "title": "Use least-privilege RBAC for operations",
      "description": "Assign Backup Reader for users who only run read commands, and assign Backup Contributor only to operators who run vault create, vault update, policy create, or protecteditem protect."
    },
    {
      "title": "Verify protection before making changes",
      "description": "Run azurebackup backup status or azurebackup governance find-unprotected to confirm protection state before enabling or altering protection for a resource."
    },
    {
      "title": "Monitor asynchronous jobs",
      "description": "Use azurebackup job get to poll and confirm the status of long-running operations such as protecteditem protect and protecteditem undelete."
    },
    {
      "title": "Confirm immutability intent",
      "description": "When using azurebackup governance immutability, validate the target state because setting immutability to 'Locked' is irreversible and can't be undone."
    }
  ],
  "genai-serviceDocLink": "",
  "genai-additionalLinks": [
    {
      "title": "Azure Backup overview",
      "url": ""
    },
    {
      "title": "Create a Recovery Services vault quickstart",
      "url": ""
    },
    {
      "title": "Back up a virtual machine documentation",
      "url": ""
    }
  ]
}
```