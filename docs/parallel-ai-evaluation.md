# Parallel AI Call Evaluation

> **Issue:** #356 — Evaluate batch/parallel AI calls within a namespace  
> **Status:** Evaluation complete — recommendation included  
> **Date:** 2025-07-24

## Overview

This document evaluates the feasibility, benefits, and risks of parallelizing
AI (Azure OpenAI) calls within a single namespace during the documentation
generation pipeline.  Every finding is based on code inspection of the actual
codebase, not assumptions.

---

## 1. Current Architecture

### How AI calls flow today

All AI calls are **sequential, per-tool, per-step**.  Each AI-dependent step
iterates over its work items in a `for`/`foreach` loop with `await` *inside*
the loop body:

```
start.sh (orchestrator)
  └─ foreach namespace (sequential)
       └─ foreach step 0-6 (sequential)
            └─ foreach tool/family (sequential ← AI call here)
```

### AI-dependent steps

| Step | Project | Iterates over | AI calls/item | Explicit delay |
|------|---------|---------------|---------------|----------------|
| 2 | `DocGeneration.Steps.ExamplePrompts.Generation` | Tools (`foreach`) | 1 (or deterministic bypass) | None |
| 3 | `DocGeneration.Steps.ToolGeneration.Improvements` | Composed files (`for`) | 1 | 100 ms between calls |
| 4 | `DocGeneration.Steps.ToolFamilyCleanup` | Tool families (`for`/`foreach`) | 1 per family (metadata) | None |
| 6 | `DocGeneration.Steps.HorizontalArticles` | Services (`for`) | 1 per service | None |

### Key sequential patterns (exact locations)

| Step | File | Loop line(s) | `await` inside loop |
|------|------|-------------|---------------------|
| 2 | `ExamplePrompts.Generation/Program.cs` | 223 (`foreach`) | Line 279 |
| 3 | `ToolGeneration.Improvements/Services/ImprovedToolGeneratorService.cs` | 111 (`for`) | Line 139 |
| 4 | `ToolFamilyCleanup/Services/CleanupGenerator.cs` | 101 (`for`) | Line 122 |
| 6 | `HorizontalArticles/Generators/HorizontalArticleGenerator.cs` | 161 (`for`) | Line 165 |

---

## 2. Thread Safety Audit

### 2.1 GenerativeAIClient

**File:** `DocGeneration.Core.GenerativeAI/GenerativeAIClient.cs`

| Aspect | Assessment | Evidence |
|--------|------------|----------|
| Instance fields | `_chatClient` (readonly `IChatClient`) — **safe** | Set once in constructor (line 20); never reassigned |
| Retry state | **Thread-safe** — all local variables | `retryDelayMs`, `attempt` are stack-local in `ExecuteWithRetryAsync` (lines 77-89) |
| `ExecuteWithRetryAsync` | `private static` method, no shared state | Each concurrent call gets its own retry counter and delay |
| Underlying Azure SDK | `IChatClient` implementations in `Microsoft.Extensions.AI` are designed thread-safe | HttpClient reuse is internal to `AzureOpenAIClient` |
| Locking | None | No `lock`, `SemaphoreSlim`, or `Monitor` |

**Verdict: ✅ Safe for concurrent callers.** Multiple tasks can call
`GetChatCompletionAsync` on the same instance simultaneously.  Each invocation
has isolated retry state.

### 2.2 TokenUsageSummary

**File:** `DocGeneration.Core.Shared/TokenUsage.cs`

| Aspect | Assessment | Evidence |
|--------|------------|----------|
| `Calls` collection | `List<T>` — **NOT thread-safe** | Line 57: `public List<TokenUsageRecord> Calls { get; set; } = new();` |
| `AddCall()` method | **NOT thread-safe** — compound operations | Lines 63-68: `Calls.Add(record)` + four `+=` increments |
| Numeric counters | `+=` on `int` is three operations (read-add-write) | Lines 65-68: race conditions on `TotalPromptTokens`, `TotalCompletionTokens`, `TotalTokens`, `CallCount` |
| Thread-safe collections | None used | No `ConcurrentBag`, `ConcurrentDictionary`, `Interlocked` |

**Verdict: ❌ NOT safe for concurrent writes.**  If parallelized, `AddCall()`
will cause data corruption.  Fix required: either add `lock`, use
`ConcurrentBag<TokenUsageRecord>`, or use `Interlocked.Add()` for counters.

### 2.3 LogFileHelper

**File:** `DocGeneration.Core.Shared/LogFileHelper.cs`

| Aspect | Assessment | Evidence |
|--------|------------|----------|
| Lock object | `private static readonly object _lock` | Line 17 |
| `Initialize()` | Guarded by `lock(_lock)` | Line 29 |
| `GetLogFilePath()` | Double-checked locking pattern | Lines 44-62 |
| `WriteDebug()` | `File.AppendAllText` under `lock(_lock)` | Lines 134-136 |
| `WriteDebugLines()` | `File.AppendAllText` under `lock(_lock)` | Lines 162-164 |
| `Reset()` | Guarded by `lock(_lock)` | Line 188 |

**Verdict: ✅ Safe for concurrent callers.**  All file I/O is serialized
behind a static lock.  Multiple threads can call `WriteDebug` without
corruption.

### 2.4 PromptTokenResolver

**File:** `DocGeneration.Core.Shared/PromptTokenResolver.cs`

| Aspect | Assessment | Evidence |
|--------|------------|----------|
| Cache field | `private static string? _acrolinxRules` | Lazy-initialized |
| Lock | `private static readonly Lock _lock = new()` | Line 19 (uses .NET 9 `Lock` type) |
| Pattern | Double-checked locking | Lines 27-45 |

**Verdict: ✅ Safe for concurrent callers.**

### 2.5 File I/O (output writing)

Each step writes output files during iteration:

| Step | Files per item | Pattern |
|------|---------------|---------|
| 2 | 3 (input prompt, raw output, final) | Unique filenames per tool |
| 3 | 1 (improved file) | Unique filename per tool |
| 4 | 2-3 (metadata, related, stitched) | Unique filename per family |
| 6 | 2+ (prompt, article) | Unique filename per service |

**Verdict: ✅ Safe for concurrent access** — each tool/family writes to
distinct file paths.  No two concurrent tasks would write to the same file
within a step.

### 2.6 Console output

`Console.Write`/`Console.WriteLine` in .NET are **thread-safe** (internally
synchronized).  However, interleaved progress messages (e.g., `[3/10] Processing…`)
would become unreadable.  This is a cosmetic issue, not a correctness issue.

### Summary table

| Resource | Thread-safe? | Fix needed for parallelism |
|----------|-------------|---------------------------|
| `GenerativeAIClient` | ✅ Yes | None |
| `TokenUsageSummary` | ❌ No | Add locking or use concurrent collections |
| `LogFileHelper` | ✅ Yes | None |
| `PromptTokenResolver` | ✅ Yes | None |
| File I/O (outputs) | ✅ Yes | None (unique paths per tool) |
| Console output | ✅ Yes (safe, not pretty) | Suppress or buffer per-tool messages |
| Success/failure counters | ❌ No | Use `Interlocked.Increment` |

---

## 3. Rate Limit Analysis

### 3.1 Azure OpenAI deployment configuration

**Source:** `infra/modules/openai.bicep`, `infra/main.bicep`

| Setting | Value |
|---------|-------|
| Model | `gpt-5-mini` (version `2025-08-07`) |
| SKU tier | `GlobalStandard` |
| TPM capacity | **50,000 TPM** (parameter `gpt5MiniCapacity = 50`, unit = 1000) |
| RPM limit | Not explicitly set; Azure-managed based on TPM |
| Redundancy | Primary (`eastus2`) + secondary (`swedencentral`), same capacity |

### 3.2 GlobalStandard rate limit estimation

For Azure OpenAI `GlobalStandard` deployments, RPM is derived from TPM.
With 50K TPM for a mini model:

- **Estimated RPM:** ~300-500 requests/minute (Azure allocates roughly
  6-10 RPM per 1K TPM for small-token requests, but varies by model and
  payload size)
- **Practical RPM:** Documentation prompts average ~2,000-4,000 tokens per
  request (prompt + completion), so at 50K TPM the effective throughput is
  roughly **12-25 requests/minute** for typical payloads

### 3.3 Current sequential throughput

Based on code inspection:

| Step | Items/namespace | Time per call (est.) | Throughput |
|------|----------------|---------------------|------------|
| 2 (ExamplePrompts) | 5-15 tools (many bypass via deterministic) | 2-4 s | ~15-20 calls/min |
| 3 (ToolGeneration) | 5-15 files + 100 ms delay | 2-4 s + 0.1 s | ~14-18 calls/min |
| 4 (ToolFamilyCleanup) | 1-3 families | 3-6 s | ~10-15 calls/min |
| 6 (HorizontalArticles) | 1 service | 4-8 s | ~8-12 calls/min |

**Current total AI calls per namespace:** ~10-30 calls (varies by tool count)  
**Current wall-clock time per namespace (AI only):** ~30-120 seconds

### 3.4 Estimated parallel throughput

| Concurrency | Calls/min (est.) | Likely outcome |
|-------------|-----------------|----------------|
| 1 (current) | ~15 | No rate limiting |
| 2 | ~30 | Likely within limits |
| 4 | ~60 | May trigger rate limits for large namespaces |
| 8 | ~120 | Will frequently hit 50K TPM ceiling |

**Key constraint:** At concurrency ≥ 4, the TPM ceiling (not RPM) becomes
the bottleneck.  Parallel requests will complete the HTTP round-trip faster,
but the token budget is shared across all concurrent requests.

---

## 4. Design Options

### Option A: SemaphoreSlim-based concurrency

**Approach:** Wrap the existing sequential loop with a `SemaphoreSlim` to
limit concurrent AI calls to N.

```csharp
var semaphore = new SemaphoreSlim(maxConcurrency); // e.g., 3
var tasks = tools.Select(async tool =>
{
    await semaphore.WaitAsync(ct);
    try
    {
        await ProcessToolAsync(tool);
    }
    finally
    {
        semaphore.Release();
    }
});
await Task.WhenAll(tasks);
```

| Aspect | Assessment |
|--------|------------|
| **Pros** | Simple to implement; familiar pattern; easy to tune concurrency via parameter |
| **Cons** | All tasks created eagerly (memory pressure with 100+ tools); error handling requires aggregation; unordered completion |
| **Complexity** | Low — ~20 lines of code change per step |
| **Thread safety fixes** | `TokenUsageSummary.AddCall` needs `lock`; counters need `Interlocked` |
| **Error behavior** | One failure doesn't cancel others (unless CancellationToken is used) |

### Option B: Channel-based producer/consumer pipeline

**Approach:** Use `System.Threading.Channels` with a bounded channel.
Producer enqueues tool items; N consumers dequeue and process.

```csharp
var channel = Channel.CreateBounded<ToolItem>(new BoundedChannelOptions(maxConcurrency));

// Producer
_ = Task.Run(async () => {
    foreach (var tool in tools) await channel.Writer.WriteAsync(tool, ct);
    channel.Writer.Complete();
});

// Consumers
var consumers = Enumerable.Range(0, maxConcurrency).Select(_ =>
    Task.Run(async () => {
        await foreach (var tool in channel.Reader.ReadAllAsync(ct))
            await ProcessToolAsync(tool);
    }));
await Task.WhenAll(consumers);
```

| Aspect | Assessment |
|--------|------------|
| **Pros** | Backpressure built-in; bounded memory; clean separation of concerns |
| **Cons** | More complex; harder to debug; overkill for simple iteration |
| **Complexity** | Medium — ~40 lines; new pattern for codebase |
| **Thread safety fixes** | Same as Option A |
| **Error behavior** | Consumer failures need careful handling to avoid deadlocks |

### Option C: Parallel.ForEachAsync with bounded concurrency

**Approach:** Use .NET 6+ `Parallel.ForEachAsync` with
`MaxDegreeOfParallelism`.

```csharp
await Parallel.ForEachAsync(
    tools,
    new ParallelOptions
    {
        MaxDegreeOfParallelism = maxConcurrency,
        CancellationToken = ct
    },
    async (tool, token) =>
    {
        await ProcessToolAsync(tool);
    });
```

| Aspect | Assessment |
|--------|------------|
| **Pros** | Idiomatic .NET; built-in bounded concurrency; lazy enumeration; clean API |
| **Cons** | Less control over task scheduling; exception aggregation via `AggregateException` |
| **Complexity** | **Lowest** — ~10 lines of code change per step |
| **Thread safety fixes** | Same as Option A |
| **Error behavior** | First unhandled exception cancels remaining work (configurable) |

### Comparison matrix

| Criterion | Option A (SemaphoreSlim) | Option B (Channel) | Option C (Parallel.ForEachAsync) |
|-----------|--------------------------|-------------------|--------------------------------|
| Lines of code | ~20/step | ~40/step | ~10/step |
| Memory efficiency | Eager task creation | Bounded | Lazy enumeration |
| Backpressure | Manual | Built-in | Built-in |
| Debugging | Moderate | Hard | Moderate |
| .NET idiom | Common | Advanced | **Recommended** |
| Error handling | Manual aggregation | Complex | `AggregateException` |
| Cancellation | Manual per-task | Channel completion | Built-in |

---

## 5. Timing Estimates

### Wall-clock time reduction estimates

Assumptions:
- Average AI call latency: 3 seconds
- Network + processing overhead: 0.5 seconds per call
- Rate limit retry: adds 1-16 seconds (exponential backoff)

| Namespace size | Tools with AI calls | Current (sequential) | Concurrency = 2 | Concurrency = 3 | Concurrency = 4 |
|---------------|--------------------|--------------------|-----------------|-----------------|-----------------|
| Small (e.g., `fileshares`) | 3-5 | ~15-25 s | ~8-13 s | ~6-10 s | ~5-8 s |
| Medium (e.g., `appservice`) | 8-12 | ~30-50 s | ~15-25 s | ~10-17 s | ~8-13 s |
| Large (e.g., `compute`) | 15-20 | ~55-80 s | ~28-40 s | ~19-27 s | ~14-20 s |

**Projected savings for full catalog (52 namespaces):**

| Concurrency | Est. total AI time | Savings vs sequential |
|-------------|--------------------|-----------------------|
| 1 (current) | ~30-40 min | — |
| 2 | ~15-20 min | ~50% |
| 3 | ~10-15 min | ~60-65% |
| 4 | ~8-12 min | ~70% (but rate limit risk) |

> **Note:** Steps 4 and 6 have only 1-3 items per namespace, so parallelism
> within those steps provides negligible benefit.  The biggest gain is in
> **Step 2** (ExamplePrompts) and **Step 3** (ToolGeneration), which iterate
> over individual tools.

---

## 6. Risk Analysis

### 6.1 Rate limiting

| Risk | Severity | Mitigation |
|------|----------|------------|
| TPM ceiling exceeded at high concurrency | **High** | Cap concurrency at 2-3; existing exponential backoff retries handle transient 429s |
| Retry storms (all N tasks retry simultaneously) | **Medium** | Add jitter to retry delays (`retryDelayMs + random(0, 500)`) |
| Secondary endpoint failover not concurrency-aware | **Low** | Current failover is not automated in code; manual intervention unchanged |

### 6.2 Error handling complexity

| Risk | Severity | Mitigation |
|------|----------|------------|
| `AggregateException` wrapping obscures root cause | **Medium** | Unwrap and log individual exceptions |
| One tool failure shouldn't cancel all remaining tools | **Medium** | Use try/catch per-task; accumulate failures |
| Partial output (some tools succeed, some fail) | **Low** | Already handled — current code tracks `successCount`/`failureCount` |

### 6.3 Output ordering

| Risk | Severity | Mitigation |
|------|----------|------------|
| Console progress messages interleaved | **Low** | Buffer output per task, flush on completion |
| File output unordered | **None** | Each tool writes to unique files; no ordering dependency |
| Token usage summary ordering | **None** | `TokenUsageSummary.Calls` list order is not meaningful |

### 6.4 Debugging difficulty

| Risk | Severity | Mitigation |
|------|----------|------------|
| Harder to reproduce failures in concurrent execution | **Medium** | Add concurrency level to log messages; support `--concurrency 1` fallback |
| Rate limit errors harder to attribute to specific tool | **Low** | Log tool name with every retry message (already done in current code) |

### 6.5 Correctness

| Risk | Severity | Mitigation |
|------|----------|------------|
| `TokenUsageSummary` data corruption | **High** | Must fix before parallelizing — add `lock` or `Interlocked` |
| Step counters (`successCount++`) race condition | **Medium** | Use `Interlocked.Increment` |
| Console.Write interleaving mid-line | **Low** | Use `Console.WriteLine` only (atomic line writes) |

---

## 7. Recommendation

### Recommended approach: **Option C (Parallel.ForEachAsync) at concurrency 2-3**

**Rationale:**

1. **Lowest implementation complexity** — ~10 lines of code change per step,
   using the idiomatic .NET `Parallel.ForEachAsync` API.

2. **Safe concurrency level** — at 2-3 concurrent calls, the 50K TPM budget
   is unlikely to be exhausted (typical payloads of 2-4K tokens × 3 concurrent
   = 6-12K TPM consumed per batch, well within limits).

3. **Meaningful time savings** — 50-65% reduction in wall-clock time for the
   AI-heavy steps, translating to ~15-25 minutes saved on a full 52-namespace
   catalog run.

4. **Minimal thread safety fixes required:**
   - `TokenUsageSummary.AddCall()` — add a `lock` around the method body (~3
     lines)
   - Step counters — replace `successCount++` with
     `Interlocked.Increment(ref successCount)` (~1 line per counter)
   - No changes needed to `GenerativeAIClient`, `LogFileHelper`, or
     `PromptTokenResolver`

5. **Low risk** — existing exponential backoff retry handles transient rate
   limits; unique output file paths prevent I/O conflicts; `LogFileHelper`
   is already thread-safe.

### Recommended target steps

| Step | Parallelize? | Rationale |
|------|-------------|-----------|
| **Step 2 (ExamplePrompts)** | ✅ Yes | Highest tool count; independent per-tool processing |
| **Step 3 (ToolGeneration)** | ✅ Yes | Independent per-file processing; already has 100 ms delay (remove) |
| Step 4 (ToolFamilyCleanup) | ⬜ No | Only 1-3 families per namespace; negligible benefit |
| Step 6 (HorizontalArticles) | ⬜ No | Only 1 article per namespace; no loop to parallelize |

### Implementation prerequisites

1. **Make `TokenUsageSummary.AddCall()` thread-safe** — add `lock(this)` or
   use `ConcurrentBag<TokenUsageRecord>` + `Interlocked.Add`
2. **Replace `successCount++` / `failureCount++`** with `Interlocked.Increment`
   in Steps 2 and 3
3. **Add `--concurrency N` CLI parameter** (default: 3, min: 1) to allow
   tuning and fallback to sequential
4. **Add jitter to retry delays** in `ExecuteWithRetryAsync` to prevent
   retry storms under concurrent load
5. **Buffer console output** per-task to prevent interleaved progress messages

### What NOT to do

- **Do not parallelize across namespaces** — the orchestrator (`start.sh`)
  already processes namespaces sequentially for good reason (shared CLI
  metadata, clear progress reporting).
- **Do not exceed concurrency 4** — the 50K TPM ceiling will cause frequent
  rate limiting, making the retry overhead negate the parallelism benefit.
- **Do not remove the existing retry logic** — it serves as a safety net
  regardless of concurrency level.
