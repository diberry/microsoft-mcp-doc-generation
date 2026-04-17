
# Plan: Operationalize azmcp-commands.md for Tool & Parameter Metadata

Leverage azmcp-commands.md as a structured source for tool/namespace definitions, parameter tables, and conditional/optional parameter logic. The goal is to automate extraction and normalization of this metadata for use in tool and parameter documentation generation.

**Implementation Steps:**
1. **Section Identification:**  
   Detect tool/namespace sections using markdown headings and code block patterns.
2. **Parameter Table Parsing:**  
   For each section, extract parameter tables, capturing parameter name, required/optional status, default value, and description.
   Identify and flag conditional/optional parameters, noting any context or command-specific applicability.
3. **Command Syntax Extraction:**  
   Parse example command invocations to clarify parameter usage and context.
4. **Data Normalization:**  
   Structure all extracted data into a normalized format (e.g., JSON, C# objects) for downstream use.
5. **Integration:**  
   Expose this structured metadata to documentation generators, supporting parameter tables and usage examples.
6. **Maintenance:**  
   Document a process for updating the extraction logic when azmcp-commands.md changes.

**Verification:**  
- Validate extraction on a sample of tools, ensuring all parameters (including conditional) are captured and mapped correctly.
- Cross-check with existing parameter include files for accuracy.


## Plan 2: Operationalize e2eTestPrompts.md for Example Prompt Generation

Use e2eTestPrompts.md as the canonical source for realistic, tool-specific example prompts. The goal is to automate extraction and mapping of prompts to tools for inclusion in generated documentation.

**Steps**
1. Parse e2eTestPrompts.md to identify tool name and prompt tables.
2. For each tool, extract all associated example prompts.
3. Normalize tool names for robust mapping (handle underscores, dashes, case).
4. Structure extracted prompts for lookup by tool name.
5. Integrate prompt data into the documentation generation pipeline, ensuring each tool doc can include relevant example prompts.
6. Document update/refresh process for when e2eTestPrompts.md changes.

**Verification**
- Test extraction and mapping for a sample of tools, ensuring all prompts are captured and correctly associated.
- Confirm that generated documentation includes the correct prompts for each tool.