---
title: Sample tools overview
description: Synthetic tool family article for validator integration tests.
tool_count: 3
---
# Sample tools

This article is a synthetic fixture for validator integration tests.

## Alpha get
<!-- @mcpcli sample alpha get -->
Example prompts include:
- Get the alpha resource named 'alpha-one' in resource group 'rg-app'.
| Parameter | Required | Description |
| --- | --- | --- |
| `Resource Name` | Yes | Name of the alpha resource. |
| `Resource Group` | Yes | Resource group for the alpha resource. |

## Beta update
<!-- @mcpcli sample beta update -->
Example prompts include:
- Update beta name 'beta-main' in region 'eastus'.
| Parameter | Required | Description |
| --- | --- | --- |
| `Beta Name` | Required | Name of the beta item. |
| `Region` | ✅ | Region for the beta item. |

## Gamma list
<!-- @mcpcli sample gamma list -->
Example prompts include:
- List gamma items with filter 'recent'.
| Parameter | Required | Description |
| --- | --- | --- |
| `Filter` | No | Optional gamma filter. |

## Related content
- [Sample reference](https://example.com/sample)
