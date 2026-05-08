Create or update a standard web test in Azure Monitor to monitor endpoint availability.
Use this to set up new web tests or modify existing ones with monitoring configurations like URL, frequency, locations, and expected responses.
Automatically creates a new test if it doesn't exist, or updates an existing test with new settings.

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor webtests createorupdate
```

With parameters:

```azurecli
azmcp monitor webtests createorupdate --webtest-resource <webtest-resource> --resource-group <resource-group> --appinsights-component <appinsights-component> --location <location> --webtest-locations <webtest-locations> --request-url <request-url> --webtest <webtest> --description <description> --enabled <enabled> --expected-status-code <expected-status-code> --follow-redirects <follow-redirects> --frequency <frequency> --headers <headers> --http-verb <http-verb> --ignore-status-code <ignore-status-code> --parse-requests <parse-requests> --request-body <request-body> --retry-enabled <retry-enabled> --ssl-check <ssl-check> --ssl-lifetime-check <ssl-lifetime-check> --timeout <timeout>
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
| `--subscription` | string | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead. |
| `--webtest-resource` | string | The name of the Web Test resource to operate on. |
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--appinsights-component` | string | The resource id of the Application Insights component to associate with the web test. |
| `--location` | string | The location where the web test resource is created. This should be the same as the AppInsights component location. |
| `--webtest-locations` | string | List of locations to run the test from (comma-separated values). Location refers to the geo-location population tag specific to Availability Tests. |
| `--request-url` | string | The absolute URL to test |
| `--webtest` | string | The name of the test in web test resource |
| `--description` | string | The description of the web test |
| `--enabled` | string | Whether the web test is enabled |
| `--expected-status-code` | string | Expected HTTP status code |
| `--follow-redirects` | string | Whether to follow redirects |
| `--frequency` | string | Test frequency in seconds. Supported values 300, 600, 900 seconds. |
| `--headers` | string | HTTP headers to include in the request. Comma-separated KEY=VALUE |
| `--http-verb` | string | HTTP method (get, post, etc.) |
| `--ignore-status-code` | string | Whether to ignore the status code validation |
| `--parse-requests` | string | Whether to parse dependent requests |
| `--request-body` | string | The body of the request |
| `--retry-enabled` | string | Whether retries are enabled |
| `--ssl-check` | string | Whether to check SSL certificates |
| `--ssl-lifetime-check` | string | Number of days to check SSL certificate lifetime |
| `--timeout` | string | Request timeout in seconds (max 2 minutes). Supported values: 30, 60, 90, 120 seconds |

