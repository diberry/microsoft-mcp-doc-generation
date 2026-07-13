---

title: Azure MCP Server tools for Azure Monitor
description: Use Azure MCP Server tools to manage monitoring, metrics, logs, alerts, and diagnostics for Azure resources with natural language prompts from your IDE.
ms.date: 07/13/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 22
mcp-cli.version: 3.0.0-beta.25+42015ad438332594d0db4eaa3c4e0a153e0b6b64
author: diberry
ms.author: diberry
ms.reviewer:
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Monitor

The Azure MCP Server lets you manage Azure Monitor resources, including: create or update, definitions, get, get-learning-resource, list, orchestrator-next, orchestrator-start, query, send-brownfield-analysis, and send-enhancement-select, with natural language prompts.

Azure Monitor is an Azure service that provides cloud-based capabilities for your applications. For more information, see [Azure Monitor documentation](/azure/monitor/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Activitylog: List

<!-- @mcpcli monitor activitylog list -->

Lists activity logs for the specified Azure resource over the prior number of hours.  
Retrieves events that show deployment history, resource modifications, and access patterns.  
Returns event details such as timestamp, operation name, status, and caller.  
Use the output to investigate why a resource failed to deploy or isn't working as expected.

Example prompts include:

- "List activity logs for resource 'webapp-prod' with hours '720'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource name** |  Required | The name of the Azure resource to retrieve activity logs for. |
| **Event level** |  Optional | The level of activity logs to retrieve. Valid levels are: `Critical`, `Error`, `Informational`, `Verbose`, `Warning`. If not provided, returns all levels. |
| **Hours** |  Optional | The number of hours before now to retrieve activity logs for. Default is 24.0. |
| **Resource type** |  Optional | The type of the Azure resource (for example, `'Microsoft.Storage/storageAccounts'`). Only provide this if needed to disambiguate between multiple resources with the same name. |
| **Top** |  Optional | The maximum number of activity logs to retrieve. Default is 10. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Healthmodels: Get

<!-- @mcpcli monitor healthmodels get -->

Gets a specific Azure Monitor health model by name within a resource group and subscription. Returns the model metadata and the health state, such as Healthy, Degraded, Unhealthy, or Unknown. If the health state isn't available, returns null.

Provide the `Health model name` and `Resource group` to identify the model.

For example, show the health model 'app-insights-root' in resource group 'rg-monitoring-eastus'.  
For example, get the health model 'backend-service-health' in resource group 'rg-prod-monitoring'.  

Example prompts include:

- "Show me the health model 'app-health-model' in resource group 'prod-rg'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Health model name** |  Required | The name of the health model to retrieve. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Healthmodels: List

<!-- @mcpcli monitor healthmodels list -->

Lists Azure Monitor Health Models (Microsoft.CloudHealth/health models) in a subscription or in a specific resource group. Returns a summary for each health model, including name, resource group, and location. Scope results to a resource group to narrow the output.

Example prompts include:

- "List the Azure Monitor health models in my subscription."
- "What health models are in resource group 'rg-prod'?"


[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Instrumentation: Get learning resource

<!-- @mcpcli monitor instrumentation get-learning-resource -->

Lists all available learning resources for Azure Monitor instrumentation, or retrieves the full content of a specific resource by path. By default, lists all resource paths. When a path is specified, returns the full content of that resource. Use resource paths to find code samples, configuration steps, and troubleshooting guidance for instrumentation.

Examples: "List all learning resources for Azure Monitor instrumentation", "Get learning resource at path 'instrumentation/app-insights-setup.md'"

Example prompts include:

- "Get the onboarding learning resource at path 'onboarding/get-started.md'."
- "Show me the content of the Azure Monitor onboarding learning resource at path 'instrumentation/onboarding-guide.md'."
- "Get the content of the Azure Monitor learning resource file at path 'learning/azure-monitor-setup.md'."
- "List all available Azure Monitor onboarding learning resources."
- "Show me all learning resource paths for Azure Monitor instrumentation."
- "What learning resources are available for Azure Monitor instrumentation onboarding?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Path** |  Optional | Learning resource path. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ✅ |

## Instrumentation: Orchestrator next

<!-- @mcpcli monitor instrumentation orchestrator-next -->

Retrieves the next instrumentation action after completing the current action. Execute the `instruction` field from the previous response exactly, then call this tool to get the next step. The tool returns the next action to execute, or `complete` when all steps are done.

Expected workflow:
1. Receive an action from `orchestrator-start` or `orchestrator-next`.
2. Execute the `instruction` field exactly as shown.
3. Call this tool to retrieve the next action.

Returns: The next action to execute, or `complete` when all steps are done.

Example prompts include:

- "After completing the previous Azure Monitor instrumentation step, get the next action for session ID '/workspaces/my-app/session-001' with completion note 'Added UseAzureMonitor() to Program.cs'."
- "Can you return the next onboarding action for session ID '/workspaces/proj-alpha/session-42' with completion note 'Ran dotnet add package Microsoft.ApplicationInsights.AspNetCore'?"
- "I finished the previous instrumentation step; return the next step for session ID '/home/dev/myapp/session-789' with completion note 'Updated appsettings.json with ApplicationInsights key'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Completion note** |  Required | One sentence describing what you executed, for example, 'Ran dotnet add package command' or 'Added UseAzureMonitor() to Program.cs'. |
| **Session ID** |  Required | The workspace path returned as sessionId from orchestrator-start. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

## Instrumentation: Orchestrator start

<!-- @mcpcli monitor instrumentation orchestrator-start -->

Start here for Azure Monitor instrumentation. Analyzes an Azure Monitor workspace and returns the first action to execute. After running the returned action, run `orchestrator-next` to get the next action. Follow the `instruction` field exactly.

Example prompts include:

- "Start Azure Monitor instrumentation orchestration for workspace path '/home/azureuser/projects/monitoring-workspace'."
- "Analyze workspace path '/home/ci/workspaces/my-app' and return the first Azure Monitor instrumentation step."
- "Begin guided Azure Monitor onboarding for project at workspace path '/home/dev/projects/acme-monitoring' and give me step one."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Workspace path** |  Required | Absolute path to the workspace folder. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

## Instrumentation: Send brownfield analysis

<!-- @mcpcli monitor instrumentation send-brownfield-analysis -->

Sends brownfield code analysis findings after orchestrator-start returns status `analysis_needed`. Scan the workspace source files and fill in the analysis template before sending findings. For sections that don't exist in the codebase, pass an empty/default object (for example, found: `false`, hasCustomSampling: `false`) rather than null. After this call succeeds, continue with orchestrator-next as usual.

Example prompts include:

- "Send brownfield findings JSON '{"serviceOptions":{"found":true,"method":"AddApplicationInsightsTelemetry","settingsKey":"APPINSIGHTS_INSTRUMENTATIONKEY"},"initializers":{"found":true,"types":["MyTelemetryInitializer"]},"processors":{"found":false},"clientUsage":{"found":true,"methods":["TrackEvent","TrackException"]},"sampling":{"found":false,"hasCustomSampling":false},"telemetryPipeline":{"found":false},"logging":{"found":true,"providers":["Serilog"],"filters":["Microsoft.*=Warning"]}}' to instrumentation session ID '/repos/my-org/frontend'."
- "Continue orchestration by submitting findings JSON '{"serviceOptions":{"found":false},"initializers":{"found":false},"processors":{"found":false},"clientUsage":{"found":false},"sampling":{"found":true,"hasCustomSampling":true,"type":"AdaptiveSampling"},"telemetryPipeline":{"found":false},"logging":{"found":false}}' for session ID '/repos/contoso/service-api'."
- "Submit completed brownfield findings JSON '{"serviceOptions":{"found":true,"method":"ConfigureTelemetry","settingsKey":"APPINSIGHTS_CONNECTIONSTRING"},"initializers":{"found":false},"processors":{"found":true,"types":["MyProcessor"],"order":1},"clientUsage":{"found":false},"sampling":{"found":false,"hasCustomSampling":false},"telemetryPipeline":{"found":true,"channels":["CustomChannel"]},"logging":{"found":true,"providers":["Microsoft.Extensions.Logging"],"filters":[]}}' for session ID '/workspaces/solution-monitoring/session-abc123' to proceed with orchestrator-next."

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

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

## Instrumentation: Send enhancement select

<!-- @mcpcli monitor instrumentation send-enhancement-select -->

Submits selected enhancements for a session after `orchestrator-start` returns `enhancement_available`. Present enhancement options to the user, then submit one or more option keys as a comma-separated list. For example, use `redis,processors` to select Redis and background processors. After submission, continue with `orchestrator-next`.

Example prompts include:

- "Submit enhancement keys 'redis' for instrumentation session ID 'workspace/onboard/session-123'."
- "Send enhancement keys 'redis,processors' to session ID 'workspace/prod/session-42' to continue the instrumentation enhancement flow."
- "Apply enhancement keys 'entityframework,otlp' for session ID 'workspace/myapp/session-7' to complete the enhancement selection."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Enhancement keys** |  Required | One or more enhancement keys, comma-separated (for example 'redis', 'redis,processors', 'entityframework,otlp'). |
| **Session ID** |  Required | The workspace path returned as sessionId from orchestrator-start. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

## Metrics: Definitions

<!-- @mcpcli monitor metrics definitions -->

Lists available metric definitions for an Azure resource, returning metadata for each metric. Helps you find metrics by name, unit, and supported aggregation types, so you can plan monitoring and alerts.

For example, list metric definitions for the virtual machine 'prod-web-vm1' in resource group 'production-rg'.  

Example prompts include:

- "Get metric definitions for resource type 'Microsoft.Storage/storageAccounts' resource 'mystorageacct' from metric namespace 'Microsoft.Storage/storageAccounts'."
- "Show me all available metrics and their definitions for storage account 'companydata2024'."
- "What metric definitions are available for the Application Insights resource 'appinsights-prod' with search string 'request'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource name** |  Required | The name of the Azure resource to query metrics for. |
| **Limit** |  Optional | The maximum number of metric definitions to return. Defaults to 10. |
| **Metric namespace** |  Optional | The metric namespace to query. Obtain this value from the azmcp-monitor-metrics-definitions command. |
| **Resource type** |  Optional | The Azure resource type (for example, `'Microsoft.Storage/storageAccounts'`, `'Microsoft.Compute/virtualMachines'`). If not specified, will attempt to infer from resource name. |
| **Search string** |  Optional | A string to filter the metric definitions by. Helpful for reducing the number of records returned. Performs case-insensitive matching on metric name and description fields. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Metrics: Query

<!-- @mcpcli monitor metrics query -->

Query metrics for a resource in Azure Monitor, and return time series data for the specified metrics. Returns timestamps and aggregated numeric values. It's helpful for charting, alerting, and further analysis. Supports multiple metric names and metric namespaces, and returns data across the requested time range and aggregation types. For example, query the 'Percentage CPU' metric for virtual machine 'vm-prod-01' to get CPU usage time series for the last hour.

Example prompts include:

- "Analyze performance trends for metrics 'requests/count,requests/duration' in metric namespace 'microsoft.insights/components' for resource name 'appinsights-prod' over the last 24 hours."
- "Check availability for metrics 'availabilityResults/availabilityPercentage' in metric namespace 'microsoft.insights/components' for resource name 'appinsights-prod' over the last 7 days."
- "Get the aggregation 'Average' for metrics 'requests/duration' in metric namespace 'microsoft.insights/components' for resource name 'api-staging' with interval 'PT5M' over the last 3 hours."
- "Investigate error rates for metrics 'requests/failed,exceptions/count' in metric namespace 'microsoft.insights/components' for resource name 'appinsights-prod' with interval 'PT1H' over the last 48 hours."
- "Query metric 'requests/count' in metric namespace 'microsoft.insights/components' for resource type 'Microsoft.Insights/components' and resource name 'webapp-prod' with start time '2026-07-12T00:00:00Z' and end time '2026-07-13T00:00:00Z'."
- "What's the requests per second rate for metrics 'requests/count' in metric namespace 'microsoft.insights/components' for resource name 'appinsights-prod' with aggregation 'Count' and interval 'PT1M' over the last 1 hour?"

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

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Resource log: Query

<!-- @mcpcli monitor resource log query -->

Query diagnostic and activity logs for a specific Azure resource in a Log Analytics workspace with Kusto Query Language (KQL). The tool filters results to the specified resource and returns entries from a specified table.

Use the `recent` shortcut to show the most recent logs ordered by TimeGenerated. Use the `errors` shortcut to show error-level logs ordered by TimeGenerated. Provide a custom KQL query to run a tailored query against the chosen table.

Example prompts include:

- "Query 'errors' for resource ID '/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg-prod/providers/Microsoft.OperationalInsights/workspaces/prod-logs' on table 'AzureDiagnostics' for 3 hours."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Query** |  Required | The KQL query to execute against the Log Analytics workspace. You can use predefined queries by name:
- 'recent': Shows most recent logs ordered by TimeGenerated
- 'errors': Shows error-level logs ordered by TimeGenerated
Otherwise, provide a custom KQL query. |
| **Resource ID** |  Required | The Azure Resource ID to query logs. Example: /subscriptions/&lt;sub&gt;/resourceGroups/&lt;rg&gt;/providers/Microsoft.OperationalInsights/workspaces/&lt;ws&gt;. |
| **Table name** |  Required | The name of the table to query. This is the specific table within the workspace. |
| **Hours** |  Optional | The number of hours to query back from now. Defaults to 24 hours. |
| **Limit** |  Optional | The maximum number of results to return. Defaults to 20. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:| 
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Table: List

<!-- @mcpcli monitor table list -->

Lists all tables in a Log Analytics workspace, and returns table names and schemas for constructing Kusto Query Language (KQL) queries. Requires a Log Analytics workspace.

Example prompts include:

- "List all tables with table type 'CustomLog' in Log Analytics workspace 'my-law-workspace' in resource group 'rg-monitoring'."
- "Show me the tables of table type 'AzureMetrics' in Log Analytics workspace 'prod-workspace' in resource group 'prod-rg'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Table type** |  Required | The type of table to query. Options: `CustomLog`, `AzureMetrics`, and more |
| **Workspace name** |  Required | The Log Analytics workspace ID or name. This can be either the unique identifier (GUID) or the display name of your workspace. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Table type: List

<!-- @mcpcli monitor table type list -->

Lists available table types in a Log Analytics workspace, and returns their names.

Example prompts include:

- "List all available table types in Log Analytics workspace 'prod-laworkspace' in resource group 'rg-prod'."
- "What table types are available in Log Analytics workspace 'my-log-ws' in resource group 'rg-monitoring'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Workspace name** |  Required | The Log Analytics workspace ID or name. This can be either the unique identifier (GUID) or the display name of your workspace. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Webtests: Createorupdate

<!-- @mcpcli monitor webtests createorupdate -->

Creates or updates a standard web test in Azure Monitor that monitors endpoint availability.  
Sets up a new web test or modifies an existing test to check the URL, test frequency, test locations, and expected responses.  
If the web test doesn't exist, the tool creates it; otherwise, it updates the test with the provided settings.

Example prompts include:

- "Create or update a Standard Web Test named 'web test-ping' in resource group 'rg-monitor' with AppInsights component '/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/rg-ai/providers/microsoft.insights/components/appinsights-prod'."
- "Update an existing Standard Web Test 'availability-check' in resource group 'prod-rg' to test URL 'https://www.contoso.com/health' with frequency '300' and enabled 'true'."

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

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |

## Webtests: Get

<!-- @mcpcli monitor webtests get -->

Gets details for a specific web test, or lists all web tests.

Returns detailed information for a single web test when the web test resource is provided. Omitting the web test resource returns a list of all web tests in the subscription, optionally filtered by resource group.

Example prompts include:

- "Get Web Test details for web test 'homepage-test' in my subscription in resource group 'rg-prod'."
- "List all Web Test resources in my subscription."
- "List all Web Test resources in my subscription in resource group 'rg-staging'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Web test resource name** |  Optional | The name of the Web Test resource to operate on. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Workspace: List

<!-- @mcpcli monitor workspace list -->

Lists Log Analytics workspaces in a subscription. Returns each workspace's name, resource ID, location, and pricing tier. Identifies workspaces before querying logs or configuring data sources. For example, 'List workspaces in subscription 12345678-9abc-def0-1234-56789abcdef0'.

Example prompts include:

- "List Log Analytics workspaces in my subscription."
- "Display my Log Analytics workspaces."
- "Show Log Analytics workspaces for my subscription."


[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Workspace log: Query

<!-- @mcpcli monitor workspace log query -->

Queries logs across a Log Analytics workspace using Kusto Query Language (KQL). Queries all resources and tables in the workspace. Accepts KQL syntax.

Use to retrieve workspace-wide logs when a specific resource name or resource ID isn't provided.

query accepts KQL syntax.

Example prompts include:

- "Show me the last hour of logs with query 'recent' from table 'AzureDiagnostics' in workspace 'prod-law'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Query** |  Required | The KQL query to execute against the Log Analytics workspace. You can use predefined queries by name:
- 'recent': Shows most recent logs ordered by TimeGenerated
- 'errors': Shows error-level logs ordered by TimeGenerated
Otherwise, provide a custom KQL query. |
| **Table name** |  Required | The name of the table to query. This is the specific table within the workspace. |
| **Workspace name** |  Required | The Log Analytics workspace ID or name. This can be either the unique identifier (GUID) or the display name of your workspace. |
| **Hours** |  Optional | The number of hours to query back from now. Defaults to 24 hours. |
| **Limit** |  Optional | The maximum number of results to return. Defaults to 20. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Create workbook specified resource

<!-- @mcpcli workbooks create -->

Creates a workbook in the specified `Resource group` and subscription. Set the `Display name`, and provide the workbook's `Serialized content` as JSON. Returns the created workbook information on success.

Example prompts include:

- "Create a new workbook with display name 'app-health-workbook', resource group 'rg-monitoring', and serialized content '{"version":"1.0","items":[]}'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Display name** |  Required | The display name of the workbook. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Serialized content** |  Required | The serialized JSON content of the workbook. |
| **Source ID** |  Optional | The linked resource ID for the workbook. By default, this is 'azure monitor'. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |

## Delete one more workbooks

<!-- @mcpcli workbooks delete -->

Deletes one or more Azure Monitor workbooks by resource ID. Soft deletes the workbooks; they're retained for 90 days and can be restored from the Recycle Bin in the Azure portal.

Accepts multiple workbook IDs in a single batch. Partial failures are reported per workbook, and individual failures don't fail the entire batch.

For more information, see /azure/azure-monitor/visualize/workbooks-manage.

Example prompts include:

- "Delete workbook with resource ID \<workbook_resource_id\>."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Workbook IDs** |  Required | The Azure Resource IDs of the workbooks to operate on (supports multiple values for batch operations). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |

## Get Azure workbooks

<!-- @mcpcli workbooks list -->

Searches Azure Workbooks metadata across subscriptions and resource groups, using Azure Resource Graph for fast queries.

USE FOR: Discovery, filtering, and counting workbooks across scopes.  
RETURNS: Workbook metadata, including id, name, location, category, and timestamps.  
DOES NOT RETURN: Full workbook content (serializedData) by default; use `show` or set the output format to `full` to return serializedData.

SCOPE: By default, searches workbooks in the current Azure context (tenant and subscription). Use the subscription and resource group options to explicitly control scope.  
TOTAL COUNT: Returns the server-side total count by default, not just the returned items.  
MAX RESULTS: Default 50, maximum 1000. Use the max results option to adjust.  
OUTPUT FORMAT: Set the output format to `summary` for minimal tokens, or to `full` to include serializedData.

FILTERS: Supports filtering by name contains, category, kind, source id, and modified-after.

Example prompts include:

- "List all workbooks in my resource group 'rg-prod'."
- "What workbooks do I have in resource group 'rg-support'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Category** |  Optional | Filter workbooks by category (for example, `'workbook'`, `'sentinel'`, `'TSG'`). If not specified, all categories are returned. |
| **Include total count** |  Optional | Include total count of all matching workbooks in the response (default: `true`). |
| **Kind** |  Optional | Filter workbooks by kind (for example, `'shared'`, `'user'`). If not specified, all kinds are returned. |
| **Max results** |  Optional | Maximum number of results to return (default: 50, max: 1000). |
| **Modified after** |  Optional | Filter workbooks modified after this date (ISO 8601 format, for example, '2024-01-15'). |
| **Name contains** |  Optional | Filter workbooks where display name contains this text (case-insensitive). |
| **Output format** |  Optional | Output format: 'summary' (ID+name only, minimal tokens), 'standard' (metadata without content, default), 'full' (includes serializedData). |
| **Source ID** |  Optional | Filter workbooks by source resource ID (for example, `Application` Insights resource, `Log` Analytics workspace). If not specified, all workbooks are returned. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Get full workbook via

<!-- @mcpcli workbooks show -->

Retrieves full workbook details, including the `serializedData` property, tags, and `etag`. Use to get a complete workbook definition, including visualization JSON.

Accepts multiple `Workbook IDs` for batch requests, and reports partial failures per workbook. For discovery, run the `list` command first, then use `show` for specific workbooks.

Example prompts include:

- "Get information about workbook ID '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-prod/providers/Microsoft.Insights/workbooks/sales-dashboard'."
- "Show me the workbooks with workbook IDs '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg-monitor/providers/Microsoft.Insights/workbooks/ops-overview' and '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg-monitor/providers/Microsoft.Insights/workbooks/cost-report'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Workbook IDs** |  Required | The Azure Resource IDs of the workbooks to operate on (supports multiple values for batch operations). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Update updates properties Azure

<!-- @mcpcli workbooks update -->

Updates properties of an existing Azure Monitor workbook, adds new steps, modifies content, or changes the display name. Returns the updated workbook details. Requires the `Workbook ID` and either new serialized content or a new display name.

Example prompts include:

- "Update workbook ID '/subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/rg-prod/providers/Microsoft.Insights/workbooks/my-workbook' with serialized content '{"version":"1.0","items":[{"type":"text","text":"New text step"}]}'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Workbook ID** |  Required | The Azure Resource ID of the workbook to retrieve. |
| **Display name** |  Optional | The display name of the workbook. |
| **Serialized content** |  Optional | The JSON serialized content/data of the workbook. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |

## Related content


- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure Monitor documentation](/azure/azure-monitor/)
