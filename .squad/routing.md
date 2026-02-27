# Routing — Azure MCP Documentation Generator

Use this file to determine which agent(s) to spawn for any given task.

## Routing Rules

### C# / .NET Code → Morgan

Spawn Morgan for:
- Any `.cs` file changes in `docs-generation/`
- Generator logic: `CSharpGenerator/`, `TemplateEngine/`, `NaturalLanguageGenerator/`, `TextTransformation/`
- Shared library: `Shared/`, `ToolFamily/`, `ToolMetadataExtractor/`
- Specialized generators: `AnnotationGenerator`, `PageGenerator`, `ParameterGenerator`
- Handlebars template helpers (`HandlebarsTemplateEngine.cs`)
- Output format changes in `.hbs` template files under `docs-generation/templates/`
- Central package management (`Directory.Packages.props`)

### Tests → Parker

Spawn Parker for:
- Any `*.Tests/` project changes
- New test cases or test coverage improvements
- CI test failures (`dotnet test docs-generation.sln`)
- Bug fixes (Parker must add regression tests for every bug fix)

**Critical**: Morgan + Parker are often spawned together — Morgan writes code, Parker writes tests.

### Scripts / CI / Docker → Quinn

Spawn Quinn for:
- PowerShell scripts (`.ps1`) in `docs-generation/scripts/`
- Bash scripts (`.sh`): `start.sh`, `docs-generation/scripts/*.sh`
- GitHub Actions workflows (`.github/workflows/*.yml`)
- Docker files (`Dockerfile`, `docker-compose.yml`)
- Infrastructure files (`azure.yaml`, `infra/`)
- Cross-platform bash↔PowerShell interop issues

### AI / Prompts / Azure OpenAI → Sage

Spawn Sage for:
- Prompt files (`docs-generation/prompts/`, project-specific `prompts/` dirs)
- `GenerativeAI/` package changes
- `ExamplePromptGeneratorStandalone/`
- `HorizontalArticleGenerator/`
- JSON response parsing from AI
- Retry/rate-limiting logic for API calls
- Model selection and API version changes

### Architecture / Cross-Cutting → Avery

Spawn Avery for:
- New project/package creation (must add to `docs-generation.sln`)
- Architecture decisions affecting multiple projects
- Data file structure changes (`docs-generation/data/*.json`)
- Brand mapping changes (`brand-to-server-mapping.json`)
- README updates for new features
- PR reviews that span multiple domains
- Dependency updates

### Documentation / Decisions → Reeve

Spawn Reeve for:
- Logging architectural decisions to `.squad/decisions.md`
- README changes (`README.md`, `docs-generation/README.md`, project `README.md` files)
- Documentation in `docs/` directory
- Session summaries after multi-step work
- Updating `copilot-instructions.md` when project conventions change

## Parallel Spawning Patterns

| Scenario | Agents to Spawn Simultaneously |
|----------|-------------------------------|
| Bug fix in generator | Morgan + Parker |
| New C# feature | Avery (design) → Morgan + Parker (implement) |
| New script | Quinn + Reeve (docs) |
| New AI feature | Sage + Morgan (if C# integration needed) + Parker (tests) |
| Architecture change | Avery + Reeve (decisions logging) |
| CI failure | Quinn (pipeline) + Parker (tests) |

## Escalation

When a task is ambiguous, Avery makes the final call on scope, approach, and routing.
