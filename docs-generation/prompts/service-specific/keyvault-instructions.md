# =============================================================================
# KEY VAULT SERVICE-SPECIFIC INSTRUCTIONS
# =============================================================================
# These instructions apply ONLY to Azure Key Vault example prompts.
# They are based on PR review feedback from the Key Vault SDK team.
# Source: https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8229
# =============================================================================

## TERMINOLOGY REQUIREMENTS

### Use lowercase "key vault" in prompts (not "Key Vault")
When the vault name follows, use lowercase "key vault":
- ✅ CORRECT: "Show me all keys in my key vault 'mykeyvault'"
- ❌ WRONG: "Show me all keys in my Key Vault 'mykeyvault'"
- ❌ WRONG: "Show me all keys in my 'mykeyvault' Key Vault"

### Always specify the key vault name explicitly
Every prompt MUST include a specific key vault name:
- ✅ CORRECT: "Get the key 'data-key' from key vault 'mykeyvault'"
- ❌ WRONG: "Get the key 'data-key' from my Key Vault"
- ❌ WRONG: "Get properties of the 'data-key' in my vault"

### Word order for vault references
Place "key vault" BEFORE the vault name, not after:
- ✅ CORRECT: "in key vault 'security-kv'"
- ✅ CORRECT: "in my key vault 'mykeyvault'"
- ❌ WRONG: "in my 'mykeyvault' Key Vault"

## MANAGED HSM vs KEY VAULT DISTINCTION

### Managed HSM operations are NOT for standard key vaults
The `keyvault admin settings get` command ONLY works with Managed HSM, not standard key vaults:
- ✅ CORRECT: "Get the account settings for my managed HSM 'myhsm'"
- ✅ CORRECT: "Show me the account settings for managed HSM 'contoso-hsm'"
- ❌ WRONG: "Get the account settings for my key vault 'mykeyvault'"

Use "managed HSM" terminology for HSM-specific operations.

## KEY OPERATIONS

### Keys cannot be "retrieved" - only key properties/details
You cannot retrieve a whole key from Key Vault. You only retrieve key properties:
- ✅ CORRECT: "Get the properties of key 'data-key' in key vault 'mykeyvault'"
- ✅ CORRECT: "Show me details of the key 'app-encryption-key'"
- ❌ WRONG: "Retrieve key 'data-key' from my key vault" (implies getting the key material)

### Include "key" before the key name for clarity
- ✅ CORRECT: "Get information about the key 'signing-key' in key vault 'security-kv'"
- ❌ WRONG: "Get information about the 'signing-key' in Key Vault 'security-kv'"

### Managed keys filtering
Use natural language for managed key filtering, not parameter syntax:
- ✅ CORRECT: "List all non-managed keys in key vault 'central-keys'"
- ✅ CORRECT: "Show all keys in key vault 'mykeyvault' including managed keys"
- ❌ WRONG: "List keys in Key Vault 'central-keys' with include managed set to false"

## SECRETS

### Don't use secrets for app configuration
Secrets are for sensitive values. Don't suggest storing app configuration in secrets:
- ✅ CORRECT: API keys, passwords, connection strings, credentials
- ❌ WRONG: App configuration (suggest Azure App Configuration service instead)

### Removed examples
Don't generate examples like:
- ❌ "Create a secret named 'app-config' with value '{\"setting\":\"value\"}'"
(Use Azure App Configuration for this use case)

## CERTIFICATES

### Differentiate SSL/TLS use cases
When the prompt mentions SSL or TLS, include that context:
- ✅ CORRECT: "Create a certificate for SSL/TLS on my web server in key vault 'mykeyvault'"
- ❌ WRONG: Generic "Create a certificate" without context

## PROMPT STRUCTURE GUIDELINES

### Preferred prompt patterns for Key Vault

**For keys:**
- "Create a new RSA key named '{key-name}' in my key vault '{vault-name}'"
- "Get the properties of key '{key-name}' in key vault '{vault-name}'"
- "List all non-managed keys in key vault '{vault-name}'"

**For secrets:**
- "Create a secret named '{secret-name}' with value '{value}' in my key vault '{vault-name}'"
- "Retrieve the '{secret-name}' secret from my key vault '{vault-name}'"
- "Show me all secrets in my key vault '{vault-name}'"

**For certificates:**
- "Create a certificate named '{cert-name}' in my key vault '{vault-name}'"
- "Import the certificate in file '{path}' into key vault '{vault-name}'"
- "Get information about the '{cert-name}' certificate in key vault '{vault-name}'"

**For Managed HSM:**
- "Get the account settings for my managed HSM '{hsm-name}'"
- "What's the value of the 'purgeProtection' setting in my managed HSM '{hsm-name}'"

## EXAMPLE CORRECTIONS FROM PR REVIEW

### Original → Corrected

| Original | Corrected |
|----------|-----------|
| "Create a new RSA key named 'app-encryption-key' in my 'mykeyvault' Key Vault." | "Create a new RSA key named 'app-encryption-key' in my key vault 'mykeyvault'" |
| "Get information about the 'signing-key' in Key Vault 'security-kv'" | "Get information about the key 'signing-key' in key vault 'security-kv'" |
| "List keys in Key Vault 'central-keys' with include managed set to false" | "List all non-managed keys in key vault 'central-keys'" |
| "Get the account settings for my key vault 'mykeyvault'" | "Get the account settings for my managed HSM 'myhsm'" |
| "Retrieve key 'data-key' from my Key Vault" | "Get properties of the key 'data-key' in key vault 'mykeyvault'" |
