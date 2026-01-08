
# Plan: Improve Azure Service Article Generation with LLMs

Enhance horizontal article generation for all Azure services by refining prompts, templates, and post-processing to leverage LLM knowledge and produce richer, more actionable content. Use the ACR service's inputs and outputs as a reference for common patterns and improvement opportunities. The goal is to ensure articles are informative, scenario-driven, and reflect best practices from official Azure documentation for any Azure service.

## Steps
1. Refine system and user prompts in `docs-generation/prompts/` to explicitly request deeper Azure service knowledge, realistic scenarios, and best practices for each service.
2. Update the Handlebars template in `docs-generation/templates/horizontal-article-template.hbs` to better surface AI-generated content, scenarios, and actionable guidance for any service.
3. Add post-processing logic in `HorizontalArticleGenerator.cs` to validate and enrich LLM output (e.g., fill missing fields, retry on incomplete JSON, inject official links) for all services.
4. Integrate Azure documentation links and RBAC role details by cross-referencing with official docs in the output for each service.
5. Test generation for a variety of Azure services (using ACR as an example), reviewing output for completeness, accuracy, and practical value.

## Further Considerations
1. Should scenario count and detail be increased for key services or those with many tools?
2. Optionally add fallback content or error messages if LLM output is incomplete for any service.
3. Consider prompt engineering to guide LLMs toward more practical, user-focused examples for all Azure services.
