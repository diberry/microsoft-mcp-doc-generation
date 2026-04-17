
# Plan: Operationalize e2eTestPrompts.md for Example Prompt Generation

Use e2eTestPrompts.md as the canonical source for realistic, tool-specific example prompts. The goal is to automate extraction and mapping of prompts to tools for inclusion in generated documentation.

**Implementation Steps:**
1. **Prompt Table Parsing:**  
	Identify and parse markdown tables mapping tool names to example prompts.
2. **Prompt Extraction:**  
	For each tool, extract all associated prompts, supporting multiple prompts per tool.
3. **Tool Name Normalization:**  
	Normalize tool names (handle underscores, dashes, case) for robust mapping.
4. **Data Structuring:**  
	Organize prompts in a lookup structure keyed by tool name.
5. **Integration:**  
	Integrate prompt data into the documentation generation pipeline, ensuring each tool doc includes relevant prompts.
6. **Maintenance:**  
	Document a process for updating the extraction logic when e2eTestPrompts.md changes.

**Verification:**  
- Test extraction and mapping for a sample of tools, ensuring all prompts are captured and correctly associated.
- Confirm that generated documentation includes the correct prompts for each tool.
