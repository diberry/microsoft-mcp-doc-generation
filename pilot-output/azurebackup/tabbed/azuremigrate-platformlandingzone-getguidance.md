### [MCP Server](#tab/mcp-server)

This tool executes `azuremigrate platformlandingzone getguidance` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Get how-to guidance for modifying, configuring, or customizing an existing Platform Landing Zone.
Use this tool when user asks "how do I", "show me how to", "get guidance for", or asks about 
disabling, enabling, turning off, changing, or modifying Landing Zone settings.

**Use this tool for questions about:**
- How to turn off or disable Bastion, DDoS, DNS, gateways, Defender, or monitoring
- How to change IP addresses, CIDR ranges, network topology, or regions
- How to modify policies, enable zero trust, or update management groups
- How to change resource naming patterns or conventions
- Finding or searching for specific policies within a Landing Zone
- Listing all available policies by archetype

**Available scenarios:**
- bastion: Turn off Bastion host
- ddos: Enable or disable DDoS protection plan
- dns: Turn off Private DNS zones and resolvers
- gateways: Turn off Virtual Network Gateways (VPN/ExpressRoute)
- ip-addresses: Adjust CIDR ranges and IP address space
- regions: Add or remove secondary regions
- resource-names: Update resource naming prefixes and suffixes
- management-groups: Customize management group names and IDs
- policy-enforcement: Change policy enforcement mode to DoNotEnforce
- policy-assignment: Remove or disable a policy assignment
- ama: Turn off Azure Monitoring Agent
- amba: Deploy Azure Monitoring Baseline Alerts
- defender: Turn off Defender Plans
- zero-trust: Implement Zero Trust Networking
- slz: Implement Sovereign Landing Zone controls

**For policy searches:**
- Use policy-name to search for a specific policy
- Use list-policies=true to list ALL policies by archetype

### Example CLI commands

Basic usage:

```azurecli
azmcp azuremigrate platformlandingzone getguidance
```

With parameters:

```azurecli
azmcp azuremigrate platformlandingzone getguidance --scenario <scenario> --policy-name <policy-name> --list-policies <list-policies>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--tenant` | string | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| `--auth-method` | string | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| `--retry-delay` | string | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| `--retry-max-delay` | string | Maximum delay in seconds between retries, regardless of the retry strategy. |
| `--retry-max-retries` | string | Maximum number of retry attempts for failed operations before giving up. |
| `--retry-mode` | string | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| `--retry-network-timeout` | string | Network operation timeout in seconds. Operations taking longer than this will be cancelled. |
| `--scenario` | string | The modification scenario key. Valid values: resource-names, management-groups, ddos, bastion, dns, gateways, regions, ip-addresses, policy-enforcement, policy-assignment, ama, amba, defender, zero-trust, slz. |
| `--policy-name` | string | The policy assignment name to look up (e.g., 'Enable-DDoS-VNET'). Used with policy-enforcement or policy-assignment scenarios. |
| `--list-policies` | string | Set to true to list all available policies organized by archetype. Useful for finding the exact policy name. |

---
