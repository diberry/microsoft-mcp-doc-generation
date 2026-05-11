---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
### Example CLI commands

Basic usage:

```azurecli
azmcp monitor metrics query
```

With parameters:

```azurecli
azmcp monitor metrics query --resource-group <resource-group> --resource-type <resource-type> --resource <resource> --metric-names <metric-names> --start-time <start-time> --end-time <end-time> --interval <interval> --aggregation <aggregation> --filter <filter> --metric-namespace <metric-namespace> --max-buckets <max-buckets>
```
