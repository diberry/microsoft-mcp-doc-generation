# Placeholder convention

All templates in this skill are filled by `scripts/render-outputs.{ps1,sh}` using pure string substitution. The LLM does not free-generate Teams paste text or report rows.

## Tokens

- Scalar tokens use `{{UPPER_SNAKE_CASE}}`.
- Repeating rows use `{{#ROWS}} ... {{/ROWS}}`.
- Missing display values are rendered as readable fallbacks such as `(none)` or `(unresolved)`.

## Row tokens

| Token | Meaning |
|---|---|
| `{{RUN_ID}}` | Run identifier (`yyyy-MM-dd-HHmm`) |
| `{{GENERATED_AT}}` | UTC ISO-8601 render timestamp |
| `{{PR_NUMBER}}` | Pull request number |
| `{{PR_TITLE}}` | Pull request title, collapsed to one plain-text line |
| `{{PR_URL}}` | Pull request URL |
| `{{OWNERS}}` | GitHub CODEOWNERS tokens for display |
| `{{MENTIONS}}` | Teams-pasteable `@alias` mentions; teams remain `@org/team`; unresolved handles remain `@handle (unresolved)` |
| `{{UNRESOLVED}}` | Unresolved individual GitHub handles |

## Teams paste rules

`teams-paste.txt.tmpl` is plain text only. It intentionally avoids HTML and markdown links so Dina can paste it into Teams and let Teams resolve `@alias` mentions.
