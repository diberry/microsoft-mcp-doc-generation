### 2026-05-30: Empty namespace summary causes article failure
**By:** Morgan
**What:** Added validation check after AggregateAIData — if ServiceShortDescription or ServiceOverview is empty, fail the article with a clear error message rather than silently generating broken output.
**Why:** Rubber-duck review caught that the empty-fallback path produced valid-looking but content-corrupted articles that passed all validation gates.
