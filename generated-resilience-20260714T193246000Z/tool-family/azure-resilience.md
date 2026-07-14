---

title: Azure MCP Server tools for Azure Resilience
description: Use Azure MCP Server tools to manage Azure Resilience resources with natural language prompts from your IDE.
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 9
mcp-cli.version: 3.0.0-beta.25+42015ad438332594d0db4eaa3c4e0a153e0b6b64
author: diberry
ms.author: diberry
ms.reviewer:
ms.date: 07/14/2026
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Resilience

The Azure MCP Server lets you manage Azure Resilience resources, including: get, with natural language prompts.

Azure Resilience is an Azure service that provides cloud-based capabilities for your applications. For more information, see [Azure Resilience documentation](/azure/resilience/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Goal assignment: Get

Lists resilience goal assignments in the specified service group. Specify a goal assignment `name` to return full details for that assignment: id, name, goal assignment type, goal template id, and provisioning state. Omit the `name` to list all goal assignments in the service group, returning only their id and name.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp resilience goal assignment get \
  --service-group <service-group> \
  [--name <name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `service-group` | string | Yes | The name of the service group. |
| `name` | string | No | The name of the goal assignment. Provide this argument to get the details of a particular goal assignment; omit it to list all goal assignments in the service group (id and name only). |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli resilience goal assignment get -->

Example prompts include:

- "List all resilience goal assignments in service group 'resilience-prod'."
- "Get the details of goal assignment 'high-availability-assignment' in service group 'resilience-staging'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Service group** |  Required | The name of the service group. |
| **Name** |  Optional | The name of the goal assignment. Provide this argument to get the details of a particular goal assignment; omit it to list all goal assignments in the service group (ID and name only). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Goal resource: Get

Lists the resources (members) of a resilience goal assignment. Provide a goal resource name to return the resource's full details. Details include id, name, disaster recovery and high availability attestation status, goal participation, exclusion reasons, provisioning state, resource ARM id, and service group memberships. Omit the name to list all resources in the goal assignment, returning only their id and name.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp resilience goal resource get \
  --service-group <service-group> \
  --goal-assignment <goal-assignment> \
  [--name <name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `service-group` | string | Yes | The name of the service group. |
| `goal-assignment` | string | Yes | The name of the goal assignment. |
| `name` | string | No | The name of the goal resource. Provide this argument to get the details of a particular goal resource; omit it to list all resources (members) of the goal assignment (id and name only). |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli resilience goal resource get -->

Example prompts include:

- "List all resources (members) of goal assignment 'backup-goal' in service group 'database-services'."
- "Get the goal resource 'vm-web-01' for goal assignment 'backup-goal' in service group 'database-services'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Goal assignment** |  Required | The name of the goal assignment. |
| **Service group** |  Required | The name of the service group. |
| **Name** |  Optional | The name of the goal resource. Provide this argument to get the details of a particular goal resource; omit it to list all resources (members) of the goal assignment (ID and name only). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Goal template: Get

Retrieves resilience goal templates in the specified service group. Provide a goal template name to retrieve full details, including `id`, `name`, `goal type`, `provisioning state`, `recovery point objective`, `recovery time objective`, and high availability and disaster recovery requirements. Omit the name to list all goal templates in the service group. In that case, the command returns only `id` and `name` for each template.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp resilience goal template get \
  --service-group <service-group> \
  [--name <name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `service-group` | string | Yes | The name of the service group. |
| `name` | string | No | The name of the goal template. Provide this argument to get the details of a particular goal template; omit it to list all goal templates in the service group (id and name only). |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli resilience goal template get -->

Example prompts include:

- "List all resilience goal templates in service group 'web-services'."
- "Get the details of goal template 'web-tier-recovery' in service group 'database-services'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Service group** |  Required | The name of the service group. |
| **Name** |  Optional | The name of the goal template. Provide this argument to get the details of a particular goal template; omit it to list all goal templates in the service group (ID and name only). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Recovery job: Get

Lists recovery jobs for a resilience recovery plan. Provide a recovery job name to retrieve full details for that job. If no name is provided, lists all recovery jobs for the plan and returns only each job's id and name.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp resilience recovery job get \
  --service-group <service-group> \
  --recovery-plan <recovery-plan> \
  [--name <name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `service-group` | string | Yes | The name of the service group. |
| `recovery-plan` | string | Yes | The name of the recovery plan. |
| `name` | string | No | The name of the recovery job. Provide this argument to get the details of a particular recovery job; omit it to list all recovery jobs of the recovery plan (id and name only). |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli resilience recovery job get -->

Example prompts include:

- "List all recovery jobs for recovery plan 'prod-recovery-plan' in service group 'sg-backend'."
- "Get the details of recovery job 'db-failover-job' for recovery plan 'prod-recovery-plan' in service group 'sg-backend'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Recovery plan** |  Required | The name of the recovery plan. |
| **Service group** |  Required | The name of the service group. |
| **Name** |  Optional | The name of the recovery job. Provide this argument to get the details of a particular recovery job; omit it to list all recovery jobs of the recovery plan (ID and name only). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Recovery job resource: Get

Lists the resources (targets) of a resilience recovery job. Include the recovery job resource name to return full details for that resource. Omit the resource name to list all resources for the recovery job. The command returns only each resource's id and name.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp resilience recovery job resource get \
  --service-group <service-group> \
  --recovery-plan <recovery-plan> \
  --recovery-job <recovery-job> \
  [--name <name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `service-group` | string | Yes | The name of the service group. |
| `recovery-plan` | string | Yes | The name of the recovery plan. |
| `recovery-job` | string | Yes | The name of the recovery job. |
| `name` | string | No | The name of the recovery job resource (target). Provide this argument to get the details of a particular recovery job resource; omit it to list all resources (targets) of the recovery job (id and name only). |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli resilience recovery job resource get -->

Example prompts include:

- "List all resources (targets) of recovery job 'rjob-prod-01' for recovery plan 'rplan-dr-east' in service group 'sg-webapps'."
- "Get the recovery job resource 'webserver-target-3' for recovery job 'rjob-prod-01' of recovery plan 'rplan-dr-east' in service group 'sg-webapps'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Recovery job** |  Required | The name of the recovery job. |
| **Recovery plan** |  Required | The name of the recovery plan. |
| **Service group** |  Required | The name of the service group. |
| **Name** |  Optional | The name of the recovery job resource (target). Provide this argument to get the details of a particular recovery job resource; omit it to list all resources (targets) of the recovery job (ID and name only). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Recovery plan: Get

Lists resilience recovery plans in the specified service group. Provide a recovery plan name to get full details, including properties and provisioning state. Omit the name to list all recovery plans in the service group, returning only each plan's id and name.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp resilience recovery plan get \
  --service-group <service-group> \
  [--name <name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `service-group` | string | Yes | The name of the service group. |
| `name` | string | No | The name of the recovery plan. Provide this argument to get the details of a particular recovery plan; omit it to list all recovery plans in the service group (id and name only). |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli resilience recovery plan get -->

Example prompts include:

- "List all resilience recovery plans in service group 'resilience-sg'."
- "Get the details of recovery plan 'business-continuity-plan' in service group 'resilience-sg'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Service group** |  Required | The name of the service group. |
| **Name** |  Optional | The name of the recovery plan. Provide this argument to get the details of a particular recovery plan; omit it to list all recovery plans in the service group (ID and name only). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Recovery plan resource: Get

Gets the resources that are members of a resilience recovery plan. Provide a recovery resource name to get full details for that resource. Omit the name to list all resources in the recovery plan. The tool returns each resource's id and name.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp resilience recovery plan resource get \
  --service-group <service-group> \
  --recovery-plan <recovery-plan> \
  [--name <name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `service-group` | string | Yes | The name of the service group. |
| `recovery-plan` | string | Yes | The name of the recovery plan. |
| `name` | string | No | The name of the recovery resource (member). Provide this argument to get the details of a particular recovery resource; omit it to list all resources (members) of the recovery plan (id and name only). |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli resilience recovery plan resource get -->

Example prompts include:

- "List all resources (members) of recovery plan 'prod-recoveryplan' in service group 'primary-services'."
- "Get the recovery resource 'sql-server-01' for recovery plan 'prod-recoveryplan' in service group 'primary-services'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Recovery plan** |  Required | The name of the recovery plan. |
| **Service group** |  Required | The name of the service group. |
| **Name** |  Optional | The name of the recovery resource (member). Provide this argument to get the details of a particular recovery resource; omit it to list all resources (members) of the recovery plan (ID and name only). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Usageplan enrollment: Get

Lists enrollments for a resilience usage plan. Include an enrollment name to return full details for that enrollment, including ID, name, associated service group ID, provisioning state, and error details. Omit the name to return all enrollments for the usage plan, showing only ID and name for each enrollment.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp resilience usageplan enrollment get \
  --resource-group <resource-group> \
  --usage-plan <usage-plan> \
  [--name <name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `usage-plan` | string | Yes | The name of the usage plan. |
| `name` | string | No | The name of the usage plan enrollment. Provide this argument to get the details of a particular enrollment; omit it to list all enrollments of the usage plan (id and name only). |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli resilience usageplan enrollment get -->

Example prompts include:

- "List all enrollments for usage plan 'prod-usage plan' in resource group 'rg-resilience'."
- "Show details for enrollment 'enrollment-01' in usage plan 'prod-usage plan' within resource group 'rg-resilience'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Usage plan** |  Required | The name of the usage plan. |
| **Name** |  Optional | The name of the usage plan enrollment. Provide this argument to get the details of a particular enrollment; omit it to list all enrollments of the usage plan (ID and name only). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Usageplan: Get

Gets resilience usage plans.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp resilience usageplan get \
  [--resource-group <resource-group>] \
  [--name <name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `resource-group` | string | No | The name of the resource group. If omitted (and no usage plan name is given), all usage plans in the subscription are listed (id and name only). |
| `name` | string | No | The name of the usage plan. Provide this argument to get the details of a particular usage plan (requires a resource group); omit it to list usage plans (id and name only) for the resource group, or for the whole subscription when no resource group is given. |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli resilience usageplan get -->

Example prompts include:

- "Show all resilience usage plans in my subscription."
- "List all resilience usage plans in resource group 'rg-staging'."
- "Get the details of usage plan 'resilience-plan-01' in resource group 'rg-prod'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Name** |  Optional | The name of the usage plan. Provide this argument to get the details of a particular usage plan (requires a resource group); omit it to list usage plans (ID and name only) for the resource group, or for the whole subscription when no resource group is given. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
