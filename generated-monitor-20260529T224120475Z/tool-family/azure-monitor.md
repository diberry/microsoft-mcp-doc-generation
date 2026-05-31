---

title: Azure MCP Server tools for Azure Monitor
description: Use Azure MCP Server tools to manage monitoring, metrics, logs, alerts, and diagnostics for Azure resources with natural language prompts from your IDE.
ms.date: 05/29/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 16
mcp-cli.version: 3.0.0-beta.13+cd8d1e8f9924440b33e3e908c390c1599700ccba
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Monitor

The Azure Model Context Protocol (MCP) Server lets you manage Azure Monitor resources, including: createorupdate, definitions, get, get-learning-resource, list, orchestrator-next, orchestrator-start, query, send-brownfield-analysis, and send-enhancement-select, with natural language prompts.

Azure Monitor is an Azure service that provides cloud-based capabilities for your applications. For more information, see [Azure Monitor documentation](/azure/monitor/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Activitylog: List

<!-- @mcpcli monitor activity log list -->

Lists activity logs for the specified Azure resource over the prior number of hours.  
Retrieve activity logs to understand resource deployment history, modification activities, and access patterns.  
It returns activity log events that include timestamp, operation name, status, and caller information.  
The command queries the Azure Monitor activity log.  
Specify the `Resource name` and the number of prior hours to filter results.

For example, check recent activity logs to determine why a deployment failed, or to audit configuration changes.

Example prompts include:

- "List activity logs for resource 'webapp-prod' for the last '720' hours."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource name** |  Required | The name of the Azure resource to retrieve activity logs for. |
| **Event level** |  Optional | The level of activity logs to retrieve. Valid levels are: `Critical`, `Error`, `Informational`, `Verbose`, `Warning`. If not provided, returns all levels. |
| **Hours** |  Optional | The number of hours before now to retrieve activity logs for. |
| **Resource type** |  Optional | The type of the Azure resource (for example, `'Microsoft.Storage/storageAccounts'`). Only provide this if needed to disambiguate between multiple resources with the same name. |
| **Top** |  Optional | The maximum number of activity logs to retrieve. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Healthmodels entity: Get

<!-- @mcpcli monitor health models entity get -->

Get the health status and health events for an entity in an Azure Monitor health model. You can monitor application-level health based on a custom health model, rather than basic Azure resource availability.

For example, you can check the current health and recent health events for an application component in a production resource group.

Example prompts include:

- "Show me the health status of entity 'orders-service' using health model 'app-health-v2' in resource group 'rg-prod'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Entity name** |  Required | The entity to get health for. |
| **Health model name** |  Required | The name of the health model for which to get the health. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Instrumentation: Get learning resource

List available learning resources for Azure Monitor instrumentation, or retrieve the content of a specific resource by `path`. By default, the tool returns all resource paths. When you specify a `path`, the tool returns the full content of that resource. Use the returned paths to browse examples, configuration snippets, and step-by-step guidance for instrumenting applications.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor instrumentation get-learning-resource -->

Example prompts include:

- "List all Azure Monitor onboarding learning resources available."
- "Show all learning resource paths for Azure Monitor instrumentation."
- "Which learning resources are available for Azure Monitor instrumentation onboarding?"
- "Get the onboarding learning resource at path 'onboarding/quickstart.md'."
- "Show me the content of the Azure Monitor onboarding learning resource at path 'instrumentation/setup-guide.md'."
- "Get the content of the Azure Monitor learning resource file at path 'samples/tracing/example.json'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Path** |  Optional | Learning resource path. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor instrumentation get-learning-resource \
  [--path <path>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--path` | string | No | Learning resource path. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ✅

## Instrumentation: Orchestrator next

Get the next instrumentation action after you complete the current one. After you execute the previous response's exact `instruction`, submit a completion note and the session ID to receive the next action. The tool returns the next action to execute, or `complete` when all steps are done.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor instrumentation orchestrator-next -->

Example prompts include:

- "After completing the previous instrumentation step, get the next action for session 'workspace-123' with completion note 'Added UseAzureMonitor() to Program.cs'."
- "Get the next onboarding action for session ID 'app-monitor-session' with completion note 'Ran dotnet add package Microsoft.ApplicationInsights'."
- "Return the next step for session 'workspace-789' with completion note 'Configured Application Insights connection string in appsettings.json'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Completion note** |  Required | One sentence describing what you executed, for example, 'Ran dotnet add package command' or 'Added UseAzureMonitor() to Program.cs'. |
| **Session ID** |  Required | The workspace path returned as sessionId from orchestrator-start. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor instrumentation orchestrator-next \
  --session-id <session-id> \
  --completion-note <completion-note>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--session-id` | string | Yes | The workspace path returned as sessionId from orchestrator-start. |
| `--completion-note` | string | Yes | One sentence describing what you executed, e.g., 'Ran dotnet add package command' or 'Added UseAzureMonitor() to Program.cs' |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ✅

## Instrumentation: Orchestrator start

Start here for Azure Monitor instrumentation. Starts Azure Monitor instrumentation for a workspace, analyzes the workspace, and returns the first action to execute. Execute the returned action exactly as specified in the `instruction` field, and then repeat to continue the orchestration. Don't improvise; follow the `instruction` field precisely.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor instrumentation orchestrator-start -->

Example prompts include:

- "Start Azure Monitor instrumentation orchestration for workspace path '/home/dev/my-app-workspace'."
- "Analyze workspace path '/repos/proj-monitor/workspace' and return the first Azure Monitor instrumentation action."
- "Begin guided Azure Monitor onboarding for workspace path '/opt/projects/prod-service-workspace' and give me step one."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Workspace path** |  Required | Absolute path to the workspace folder. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor instrumentation orchestrator-start \
  --workspace-path <workspace-path>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--workspace-path` | string | Yes | Absolute path to the workspace folder. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ✅

## Instrumentation: Send brownfield analysis

Send brownfield code analysis findings after `orchestrator-start` returns status `analysis_needed`. Before you run this command, scan the workspace source files and fill in the analysis template. For sections that don't exist in the codebase, pass an empty or default object, for example `found: false` and `hasCustomSampling: false`, rather than null. After the call succeeds, continue with `orchestrator-next`.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor instrumentation send-brownfield-analysis -->

Example prompts include:

- "Send brownfield code analysis findings JSON '{"serviceOptions":{"found":false},"initializers":{"found":true,"types":["MyTelemetryInitializer"]},"processors":{"found":false},"clientUsage":{"found":true,"methods":["TrackEvent"]},"sampling":{"hasCustomSampling":false},"telemetryPipeline":{"found":false},"logging":{"found":false}}' to Azure Monitor instrumentation session ID 'workspace/analysis-session-01'."
- "Continue migration orchestration by submitting analysis findings JSON '{"serviceOptions":{"found":false},"initializers":{"found":false},"processors":{"found":false},"clientUsage":{"found":false},"sampling":{"hasCustomSampling":false},"telemetryPipeline":{"found":false},"logging":{"found":false}}' to session ID 'workspace/analysis-session-02'."
- "Send completed brownfield telemetry analysis JSON '{"serviceOptions":{"found":true,"settings":{"instrumentationKeyConfigured":true}},"initializers":{"found":true,"types":["CustomInitializer"]},"processors":{"found":true,"types":["SamplingProcessor"]},"clientUsage":{"found":true,"examples":["new TelemetryClient()"]},"sampling":{"hasCustomSampling":true,"config":"customRule"},"telemetryPipeline":{"found":true,"sinks":["CustomSink"]},"logging":{"found":true,"providers":["Console","AzureMonitor"]}}' for onboarding session ID 'workspace/analysis-session-03'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Findings JSON** |  Required | JSON object with brownfield analysis findings. Required properties:
- serviceOptions: Service options findings from analyzing AddApplicationInsightsTelemetry() call. Null if not found.
- initializers: Telemetry initializer findings from analyzing ITelemetryInitializer or IConfigureOptions&lt;TelemetryConfiguration&gt; implementations. Null if none found.
- processors: Telemetry processor findings from analyzing ITelemetryProcessor implementations. Null if none found.
- clientUsage: TelemetryClient usage findings from analyzing direct TelemetryClient usage. Null if not found.
- sampling: Custom sampling configuration findings. Null if no custom sampling.
- telemetryPipeline: Custom ITelemetryChannel or TelemetrySinks usage findings. Null if not found.
- logging: Explicit logger provider and filter findings. Null if not found.
For sections that do not exist in the codebase, pass an empty/default object (for example found: `false`, hasCustomSampling: `false`) rather than null. |
| **Session ID** |  Required | The workspace path returned as sessionId from orchestrator-start. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor instrumentation send-brownfield-analysis \
  --session-id <session-id> \
  --findings-json <findings-json>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--session-id` | string | Yes | The workspace path returned as sessionId from orchestrator-start. |
| `--findings-json` | string | Yes | JSON object with brownfield analysis findings. Required properties:
- serviceOptions: Service options findings from analyzing AddApplicationInsightsTelemetry() call. Null if not found.
- initializers: Telemetry initializer findings from analyzing ITelemetryInitializer or IConfigureOptions<TelemetryConfiguration> implementations. Null if none found.
- processors: Telemetry processor findings from analyzing ITelemetryProcessor implementations. Null if none found.
- clientUsage: TelemetryClient usage findings from analyzing direct TelemetryClient usage. Null if not found.
- sampling: Custom sampling configuration findings. Null if no custom sampling.
- telemetryPipeline: Custom ITelemetryChannel or TelemetrySinks usage findings. Null if not found.
- logging: Explicit logger provider and filter findings. Null if not found.
For sections that do not exist in the codebase, pass an empty/default object (e.g. found: false, hasCustomSampling: false) rather than null. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ✅

## Instrumentation: Send enhancement select

After `orchestrator-start` returns status `enhancement_available`, submit the chosen enhancement keys. First, present enhancement options to users. Then, call this tool with the chosen enhancement key or keys. Select multiple enhancements by specifying a comma-separated list, for example 'redis,processors'. After the call succeeds, continue with `orchestrator-next` as usual.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor instrumentation send-enhancement-select -->

Example prompts include:

- "Submit enhancement keys 'redis,processors' for instrumentation session ID '/workspaces/my-ws/sessions/abc123'."
- "Send selected enhancement keys 'entityframework,otlp' to session ID '/workspaces/prod-config/sessions/def456'."
- "Continue orchestrator by sending enhancement keys 'redis' for session ID '/workspaces/onboard/sessions/ghi789'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Enhancement keys** |  Required | One or more enhancement keys, comma-separated (for example 'redis', 'redis,processors', 'entityframework,otlp'). |
| **Session ID** |  Required | The workspace path returned as sessionId from orchestrator-start. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor instrumentation send-enhancement-select \
  --session-id <session-id> \
  --enhancement-keys <enhancement-keys>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--session-id` | string | Yes | The workspace path returned as sessionId from orchestrator-start. |
| `--enhancement-keys` | string | Yes | One or more enhancement keys, comma-separated (e.g. 'redis', 'redis,processors', 'entityframework,otlp'). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ✅

## Metrics: Definitions

List metric definitions available for an Azure resource in Azure Monitor. You get metadata for each metric, including name, description, unit, and supported aggregation types. Use that metadata to choose which metrics to collect and monitor for your resource.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor metrics definitions -->

Example prompts include:

- "Get metric definitions for resource 'orders-api' with resource type 'Microsoft.Web/sites' and metric namespace 'SiteMetrics'."
- "Show me all available metrics and their definitions for storage account 'mystorageacct'."
- "What metric definitions are available for the Application Insights resource 'appinsights-prod' filtered by search string 'request' with limit '50'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource name** |  Required | The name of the Azure resource to query metrics for. |
| **Limit** |  Optional | The maximum number of metric definitions to return. Defaults to 10. |
| **Metric namespace** |  Optional | The metric namespace to query. Obtain this value from the azmcp-monitor-metrics-definitions command. |
| **Resource type** |  Optional | The Azure resource type (for example, `'Microsoft.Storage/storageAccounts'`, `'Microsoft.Compute/virtualMachines'`). If not specified, will attempt to infer from resource name. |
| **Search string** |  Optional | A string to filter the metric definitions by. Helpful for reducing the number of records returned. Performs case-insensitive matching on metric name and description fields. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor metrics definitions \
  --resource <resource> \
  [--resource-group <resource-group>] \
  [--resource-type <resource-type>] \
  [--metric-namespace <metric-namespace>] \
  [--search-string <search-string>] \
  [--limit <limit>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--resource-type` | string | No | The Azure resource type (e.g., 'Microsoft.Storage/storageAccounts', 'Microsoft.Compute/virtualMachines'). If not specified, will attempt to infer from resource name. |
| `--resource` | string | Yes | The name of the Azure resource to query metrics for. |
| `--metric-namespace` | string | No | The metric namespace to query. Obtain this value from the azmcp-monitor-metrics-definitions command. |
| `--search-string` | string | No | A string to filter the metric definitions by. Helpful for reducing the number of records returned. Performs case-insensitive matching on metric name and description fields. |
| `--limit` | string | No | The maximum number of metric definitions to return. Defaults to 10. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Metrics: Query

Query Azure Monitor metrics for a resource. The tool returns time series data for the specified metrics, including timestamps and aggregated values. You specify metric names, metric namespace, and resource name to retrieve metric series.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor metrics query -->

Example prompts include:

- "Analyze performance trends for metric names 'requests/duration,requests/count' in metric namespace 'microsoft.insights/components' for resource 'app-insights-prod' over the last 24 hours."
- "Check availability metrics for metric names 'availabilityResults' in metric namespace 'microsoft.insights/components' for resource 'app-insights-prod' for the last 1 hour."
- "Get the Average metric names 'requests/count' from metric namespace 'microsoft.insights/components' for resource name 'api-gateway-prod' with interval 'PT1M'."
- "Investigate error rates using metric names 'exceptions/count,requests/failed' in metric namespace 'microsoft.insights/components' for resource 'app-insights-staging' starting at '2026-05-27T00:00:00Z'."
- "Query metric names 'Percentage CPU' in metric namespace 'Microsoft.Compute/virtualMachines' for resource 'vm-web-01' with resource type 'Microsoft.Compute/virtualMachines' over the last 6 hours."
- "What's the requests per second using metric names 'requests/total' in metric namespace 'microsoft.insights/components' for resource 'app-insights-prod' with max buckets '200'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Metric names** |  Required | The names of metrics to query (comma-separated). |
| **Metric namespace** |  Required | The metric namespace to query. Obtain this value from the azmcp-monitor-metrics-definitions command. |
| **Resource name** |  Required | The name of the Azure resource to query metrics for. |
| **Aggregation** |  Optional | The aggregation type to use (Average, Maximum, Minimum, Total, Count). |
| **End time** |  Optional | The end time for the query in ISO format (for example, `2023-01-01T00:00:00Z`). Defaults to now. |
| **Filter** |  Optional | OData filter to apply to the metrics query. |
| **Interval** |  Optional | The time interval for data points (for example, `PT1H` for 1 hour, `PT5M` for 5 minutes). |
| **Max buckets** |  Optional | The maximum number of time buckets to return. Defaults to 50. |
| **Resource type** |  Optional | The Azure resource type (for example, `'Microsoft.Storage/storageAccounts'`, `'Microsoft.Compute/virtualMachines'`). If not specified, will attempt to infer from resource name. |
| **Start time** |  Optional | The start time for the query in ISO format (for example, `2023-01-01T00:00:00Z`). Defaults to 24 hours ago. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor metrics query \
  --resource <resource> \
  --metric-names <metric-names> \
  --metric-namespace <metric-namespace> \
  [--resource-group <resource-group>] \
  [--resource-type <resource-type>] \
  [--start-time <start-time>] \
  [--end-time <end-time>] \
  [--interval <interval>] \
  [--aggregation <aggregation>] \
  [--filter <filter>] \
  [--max-buckets <max-buckets>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--resource-type` | string | No | The Azure resource type (e.g., 'Microsoft.Storage/storageAccounts', 'Microsoft.Compute/virtualMachines'). If not specified, will attempt to infer from resource name. |
| `--resource` | string | Yes | The name of the Azure resource to query metrics for. |
| `--metric-names` | string | Yes | The names of metrics to query (comma-separated). |
| `--start-time` | string | No | The start time for the query in ISO format (e.g., 2023-01-01T00:00:00Z). Defaults to 24 hours ago. |
| `--end-time` | string | No | The end time for the query in ISO format (e.g., 2023-01-01T00:00:00Z). Defaults to now. |
| `--interval` | string | No | The time interval for data points (e.g., PT1H for 1 hour, PT5M for 5 minutes). |
| `--aggregation` | string | No | The aggregation type to use (Average, Maximum, Minimum, Total, Count). |
| `--filter` | string | No | OData filter to apply to the metrics query. |
| `--metric-namespace` | string | Yes | The metric namespace to query. Obtain this value from the azmcp-monitor-metrics-definitions command. |
| `--max-buckets` | string | No | The maximum number of time buckets to return. Defaults to 50. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Resource log: Query

Query diagnostic and activity logs for a specific Azure resource in a Log Analytics workspace, part of Azure Monitor, by using Kusto Query Language (KQL). The tool runs a KQL query against a table in the workspace and returns results scoped to the specified resource. It supports common shortcuts such as 'recent' and 'errors', or you can provide a custom KQL query.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor resource log query -->

- "Show recent logs for resource 'app-monitor' from table 'AppRequests' in the last 24 hours."
- "Run a KQL query for resource 'my-functionapp' on table 'AzureDiagnostics' to list error events in the past 2 hours, limit 50."
- "Return error-level logs for resource 'storage-prod' from table 'StorageLogs'."

Example prompts include:

- "Show logs with query 'recent' for resource ID '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-prod/providers/Microsoft.Web/sites/app-monitor' from table 'AzureDiagnostics' for the past '1' hour."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Query** |  Required | The KQL query to execute against the Log Analytics workspace. You can use predefined queries by name:
- 'recent': Shows most recent logs ordered by TimeGenerated
- 'errors': Shows error-level logs ordered by TimeGenerated
Otherwise, provide a custom KQL query. |
| **Resource ID** |  Required | The Azure Resource ID to query logs. Example: /subscriptions/&lt;sub&gt;/resourceGroups/&lt;rg&gt;/providers/Microsoft.OperationalInsights/workspaces/&lt;ws&gt;. |
| **Table name** |  Required | The name of the table to query. This is the specific table within the workspace. |
| **Hours** |  Optional | The number of hours to query back from now. |
| **Limit** |  Optional | The maximum number of results to return. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor resource log query \
  --resource-id <resource-id> \
  --table <table> \
  --query <query> \
  [--hours <hours>] \
  [--limit <limit>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-id` | string | Yes | The Azure Resource ID to query logs. Example: /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.OperationalInsights/workspaces/<ws> |
| `--table` | string | Yes | The name of the table to query. This is the specific table within the workspace. |
| `--query` | string | Yes | The KQL query to execute against the Log Analytics workspace. You can use predefined queries by name:
- 'recent': Shows most recent logs ordered by TimeGenerated
- 'errors': Shows error-level logs ordered by TimeGenerated
Otherwise, provide a custom KQL query. |
| `--hours` | string | No | The number of hours to query back from now. |
| `--limit` | string | No | The maximum number of results to return. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Table: List

List all tables in a Log Analytics workspace. Requires a Log Analytics workspace.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor table list -->

Example prompts include:

- "List all tables of type 'CustomLog' in Log Analytics workspace 'prod-law' within resource group 'rg-prod'."
- "What tables of type 'AzureMetrics' are in Log Analytics workspace 'my-workspace' in resource group 'rg-monitoring'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Table type** |  Required | The type of table to query. Options: `CustomLog`, `AzureMetrics`, and more. |
| **Workspace name** |  Required | The Log Analytics workspace ID or name. This can be either the unique identifier (GUID) or the display name of your workspace. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor table list \
  --resource-group <resource-group> \
  --workspace <workspace> \
  --table-type <table-type>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--workspace` | string | Yes | The Log Analytics workspace ID or name. This can be either the unique identifier (GUID) or the display name of your workspace. |
| `--table-type` | string | Yes | The type of table to query. Options: 'CustomLog', 'AzureMetrics', etc. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Table type: List

Lists available table types in a Log Analytics workspace. Returns table type names to help you identify which table types the workspace contains.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor table type list -->

Example prompts include:

- "List all available table types in the Log Analytics workspace 'logworkspace-prod' in resource group 'rg-prod'."
- "Show me the available table types in the Log Analytics workspace 'central-logs' within resource group 'rg-logging'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Workspace name** |  Required | The Log Analytics workspace ID or name. This can be either the unique identifier (GUID) or the display name of your workspace. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor table type list \
  --resource-group <resource-group> \
  --workspace <workspace>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--workspace` | string | Yes | The Log Analytics workspace ID or name. This can be either the unique identifier (GUID) or the display name of your workspace. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Webtests: Createorupdate

<!-- @mcpcli monitor web tests createorupdate -->

Create or update a standard web test in Azure Monitor that monitors endpoint availability. You specify the test URL, frequency, test locations, and expected responses to configure monitoring. If the specified web test doesn't exist, the command creates it; otherwise, it updates the existing test with the provided settings.

Example prompts include:

- "Create a new Standard Web Test named 'web test-prod' in resource group 'rg-prod' and associate it with Application Insights component 'my-appinsights'."
- "Update an existing Standard Web Test named 'web test-prod' in resource group 'rg-prod' with description 'Homepage availability check' and frequency '300'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Web test resource name** |  Required | The name of the Web Test resource to operate on. |
| **Appinsights component** |  Optional | The resource ID of the Application Insights component to associate with the web test. |
| **Description** |  Optional | The description of the web test. |
| **Enabled** |  Optional | Whether the web test is enabled. |
| **Expected status code** |  Optional | Expected HTTP status code. |
| **Follow redirects** |  Optional | Whether to follow redirects. |
| **Frequency** |  Optional | Test frequency in seconds. Supported values 300, 600, 900 seconds. |
| **Headers** |  Optional | HTTP headers to include in the request. Comma-separated KEY=VALUE. |
| **HTTP verb** |  Optional | HTTP method (get, post, and more). |
| **Ignore status code** |  Optional | Whether to ignore the status code validation. |
| **Location** |  Optional | The location where the web test resource is created. This should be the same as the AppInsights component location. |
| **Parse requests** |  Optional | Whether to parse dependent requests. |
| **Request body** |  Optional | The body of the request. |
| **Request URL** |  Optional | The absolute URL to test. |
| **Retry enabled** |  Optional | Whether retries are enabled. |
| **SSL check** |  Optional | Whether to check SSL certificates. |
| **SSL lifetime check** |  Optional | Number of days to check SSL certificate lifetime. |
| **Timeout** |  Optional | Request timeout in seconds (max 2 minutes). Supported values: 30, 60, 90, 120 seconds. |
| **Web test name** |  Optional | The name of the test in web test resource. |
| **Web test locations** |  Optional | List of locations to run the test from (comma-separated values). Location refers to the geo-location population tag specific to Availability Tests. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Webtests: Get

<!-- @mcpcli monitor web tests get -->

Gets details for a specific Azure Monitor web test, or lists all web tests in your subscription. When you specify `webtest-resource`, the tool returns detailed information about that web test. When you omit `webtest-resource`, the tool lists all web tests in the subscription, and you can filter results by using the `resource-group` parameter.

Example prompts include:

- "Get details for Web Test 'my-web test' in my subscription in resource group 'rg-prod'."
- "Show all Web Test resources in my subscription."
- "List all Web Test resources in my subscription in resource group 'rg-test'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Web test resource name** |  Optional | The name of the Web Test resource to operate on. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Workspace: List

Lists Log Analytics workspaces in a subscription.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor workspace list -->

Example prompts include:

- "List Log Analytics workspaces across my subscription."
- "Show my Log Analytics workspaces."
- "Display the Log Analytics workspaces in my subscription."

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor workspace list \
  [--resource-group <resource-group>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Workspace log: Query

Query logs across a Log Analytics workspace using Kusto Query Language (KQL). You run workspace-wide queries that search all tables and resources in the workspace. The `query` command accepts Kusto Query Language (KQL) syntax. You can use the built-in shortcuts `recent` to show the most recent logs, and `errors` to show error-level logs, or provide a custom KQL query.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli monitor workspace log query -->

Example prompts include:

- "Run Query 'recent' against table 'AppLogs' in resource group 'rg-prod' on workspace 'my-workspace' with hours '1'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Query** |  Required | The KQL query to execute against the Log Analytics workspace. You can use predefined queries by name:
- 'recent': Shows most recent logs ordered by TimeGenerated
- 'errors': Shows error-level logs ordered by TimeGenerated
Otherwise, provide a custom KQL query. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Table name** |  Required | The name of the table to query. This is the specific table within the workspace. |
| **Workspace name** |  Required | The Log Analytics workspace ID or name. This can be either the unique identifier (GUID) or the display name of your workspace. |
| **Hours** |  Optional | The number of hours to query back from now. |
| **Limit** |  Optional | The maximum number of results to return. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp monitor workspace log query \
  --resource-group <resource-group> \
  --workspace <workspace> \
  --table <table> \
  --query <query> \
  [--hours <hours>] \
  [--limit <limit>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--workspace` | string | Yes | The Log Analytics workspace ID or name. This can be either the unique identifier (GUID) or the display name of your workspace. |
| `--table` | string | Yes | The name of the table to query. This is the specific table within the workspace. |
| `--query` | string | Yes | The KQL query to execute against the Log Analytics workspace. You can use predefined queries by name:
- 'recent': Shows most recent logs ordered by TimeGenerated
- 'errors': Shows error-level logs ordered by TimeGenerated
Otherwise, provide a custom KQL query. |
| `--hours` | string | No | The number of hours to query back from now. |
| `--limit` | string | No | The maximum number of results to return. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure Monitor documentation](/azure/azure-monitor/)
