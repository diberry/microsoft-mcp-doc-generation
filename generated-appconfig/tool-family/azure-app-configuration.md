---

title: Azure MCP Server tools for Azure App Configuration
description: Use Azure MCP Server tools to manage Azure App Configuration stores and key-value settings with natural language prompts from your IDE.
ms.date: 05/31/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 5
mcp-cli.version: 3.0.0-beta.13+cd8d1e8f9924440b33e3e908c390c1599700ccba
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure App Configuration

The Azure MCP Server lets you manage Azure App Configuration resources, including: delete, get, list, and set, with natural language prompts.

Azure App Configuration is an Azure service that provides cloud-based capabilities for your applications. For more information, see [Azure App Configuration documentation](/azure/appconfig/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Account: List

Lists all Azure App Configuration stores in a subscription. Returns a JSON array that includes each store's name and resource ID.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli appconfig account list -->

Example prompts include:

- "List App Configuration stores in my subscription."
- "Show the App Configuration stores in my subscription."
- "Display my App Configuration stores."

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp appconfig account list \
  [--resource-group <resource-group>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Kv: Delete

Deletes a key-value pair from an Azure App Configuration store. If a `label` is specified, deletes only the key-value pair that matches the `key` and `label`. If no `label` is specified, deletes the key-value pair with the default label. Removes feature flags, settings, or connection strings that are no longer needed.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli appconfig kv delete -->

Example prompts include:

- "Delete the key 'my-key' in App Configuration store 'my-appconfig'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the App Configuration store (for example, `my-appconfig`). |
| **Key name** |  Required | The name of the key to access within the App Configuration store. |
| **Content type** |  Optional | The content type of the configuration value. This is used to indicate how the value should be interpreted or parsed. |
| **Label** |  Optional | The label to apply to the configuration key. Labels are used to group and organize settings. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp appconfig kv delete \
  --account <account> \
  --key <key> \
  [--label <label>] \
  [--content-type <content-type>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--account` | string | Yes | The name of the App Configuration store (e.g., my-appconfig). |
| `--key` | string | Yes | The name of the key to access within the App Configuration store. |
| `--label` | string | No | The label to apply to the configuration key. Labels are used to group and organize settings. |
| `--content-type` | string | No | The content type of the configuration value. This is used to indicate how the value should be interpreted or parsed. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Kv: Get

Retrieves key-values from an Azure App Configuration store. Get a single key-value by specifying the key and an optional label, or list key-values when no key is provided. When listing, filter results by key filter or label filter to narrow the output. Each entry includes the key, value, label, content type, ETag, last modified time, and lock status.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli appconfig kv get -->

Example prompts include:

- "List all key-value settings in App Configuration store 'my-appconfig'."
- "Show me the key-value settings with key filter 'prod-*' in App Configuration store 'my-appconfig'."
- "Get the content for key 'connection-string' with label 'Prod' in App Configuration store 'my-appconfig'."
- "Show the content for the key 'feature-toggle' in App Configuration store 'my-appconfig'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the App Configuration store (for example, `my-appconfig`). |
| **Key filter** |  Optional | Specifies the key filter, if any, to be used when retrieving key-values. The filter can be an exact match, for example a filter of 'foo' would get all key-values with a key of 'foo', or the filter can include a '*' character at the end of the string for wildcard searches (for example, `'App*'`). If omitted all keys are retrieved. |
| **Key name** |  Optional | The name of the key to access within the App Configuration store. |
| **Label** |  Optional | The label to apply to the configuration key. Labels are used to group and organize settings. |
| **Label filter** |  Optional | Specifies the label filter, if any, to be used when retrieving key-values. The filter can be an exact match, for example a filter of 'foo' would get all key-values with a label of 'foo', or the filter can include a '*' character at the end of the string for wildcard searches (for example, `'Prod*'`). This filter is case-sensitive. If omitted, all labels are retrieved. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp appconfig kv get \
  --account <account> \
  [--key <key>] \
  [--label <label>] \
  [--key-filter <key-filter>] \
  [--label-filter <label-filter>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--account` | string | Yes | The name of the App Configuration store (e.g., my-appconfig). |
| `--key` | string | No | The name of the key to access within the App Configuration store. |
| `--label` | string | No | The label to apply to the configuration key. Labels are used to group and organize settings. |
| `--key-filter` | string | No | Specifies the key filter, if any, to be used when retrieving key-values. The filter can be an exact match, for example a filter of 'foo' would get all key-values with a key of 'foo', or the filter can include a '*' character at the end of the string for wildcard searches (e.g., 'App*'). If omitted all keys will be retrieved. |
| `--label-filter` | string | No | Specifies the label filter, if any, to be used when retrieving key-values. The filter can be an exact match, for example a filter of 'foo' would get all key-values with a label of 'foo', or the filter can include a '*' character at the end of the string for wildcard searches (e.g., 'Prod*'). This filter is case-sensitive. If omitted, all labels will be retrieved. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Kv lock: Set

Sets the lock state of a key-value in an Azure App Configuration store. Locking makes the key-value read-only, preventing changes. Unlocking removes read-only mode, allowing changes. Specify the `Account name` and `Key name`. Optionally, specify a `Label` to target a specific labeled version of the key-value. By default, the command unlocks the key-value.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli appconfig kv lock set -->

Example prompts include:

- "Lock key 'feature-flag' in App Configuration store 'my-appconfig'."
- "Unlock key 'db-connection-string' with label 'staging' in App Configuration store 'prod-config'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the App Configuration store (for example, `my-appconfig`). |
| **Key name** |  Required | The name of the key to access within the App Configuration store. |
| **Label** |  Optional | The label to apply to the configuration key. Labels are used to group and organize settings. |
| **Lock** |  Optional | Whether a key-value is locked (set to read-only) or unlocked (read-only removed). |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp appconfig kv lock set \
  --account <account> \
  --key <key> \
  [--lock <lock>] \
  [--label <label>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--lock` | string | No | Whether a key-value will be locked (set to read-only) or unlocked (read-only removed). |
| `--account` | string | Yes | The name of the App Configuration store (e.g., my-appconfig). |
| `--key` | string | Yes | The name of the key to access within the App Configuration store. |
| `--label` | string | No | The label to apply to the configuration key. Labels are used to group and organize settings. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Kv: Set

Sets a key-value setting in Azure App Configuration. Creates or updates the setting with the specified account name, key, and value. Specify an account name, a key, and a value. Optionally, specify a `label`; if you don't, the default label applies. Specify a content type to indicate how the store interprets the value. Add tags in the `key=value` format to associate metadata with the setting.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli appconfig kv set -->

Example prompts include:

- "Set key 'feature-flag-beta' in App Configuration account 'my-appconfig' to value 'true'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the App Configuration store (for example, `my-appconfig`). |
| **Key name** |  Required | The name of the key to access within the App Configuration store. |
| **Value** |  Required | The value to set for the configuration key. |
| **Content type** |  Optional | The content type of the configuration value. This is used to indicate how the value should be interpreted or parsed. |
| **Label** |  Optional | The label to apply to the configuration key. Labels are used to group and organize settings. |
| **Tags** |  Optional | The tags to associate with the configuration key. Tags should be in the format 'key=value'. Multiple tags can be specified. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp appconfig kv set \
  --account <account> \
  --key <key> \
  --value <value> \
  [--label <label>] \
  [--content-type <content-type>] \
  [--tags <tags>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--account` | string | Yes | The name of the App Configuration store (e.g., my-appconfig). |
| `--key` | string | Yes | The name of the key to access within the App Configuration store. |
| `--label` | string | No | The label to apply to the configuration key. Labels are used to group and organize settings. |
| `--content-type` | string | No | The content type of the configuration value. This is used to indicate how the value should be interpreted or parsed. |
| `--value` | string | Yes | The value to set for the configuration key. |
| `--tags` | string | No | The tags to associate with the configuration key. Tags should be in the format 'key=value'. Multiple tags can be specified. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure App Configuration documentation](/azure/azure-app-configuration/)
