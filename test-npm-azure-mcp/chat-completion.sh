#!/usr/bin/env bash
set -Eeuo pipefail
IFS=$'\n\t'

log_error() {
	local exit_code=$?
	echo "Error: command failed (exit ${exit_code}) at line $1: ${BASH_COMMAND}" >&2
}

trap 'log_error $LINENO' ERR

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/.env"

if [[ -f "${ENV_FILE}" ]]; then
	set -a
	# shellcheck disable=SC1090
	source "${ENV_FILE}"
	set +a
else
	echo "Warning: .env not found at ${ENV_FILE}" >&2
fi

# azmcp foundry openai chat-completions-create --subscription aa94d689-ef39-45c8-9434-0d9efb62b456 --tenant 888d76fa-54b2-4ced-8ee5-aac1585adee7

echo "Running command:" >&2
printf '%q ' azmcp foundry openai chat-completions-create \
  --subscription "${AZURE_SUBSCRIPTION_ID}" \
  --tenant "${AZURE_TENANT_ID}" \
  --auth-method "${AUTH_METHOD}" \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --resource-name "${AZURE_FOUNDRY_NAME}" \
  --deployment "${AZURE_OPENAI_DEPLOYMENT_NAME}" \
  --message-array "${MESSAGE_ARRAY}" \
  --max-tokens 100 \
  --temperature 0.7
printf '\n' >&2

azmcp foundry openai chat-completions-create \
  --subscription "${AZURE_SUBSCRIPTION_ID}" \
  --tenant "${AZURE_TENANT_ID}" \
  --auth-method "${AUTH_METHOD}" \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --resource-name "${AZURE_FOUNDRY_NAME}" \
  --deployment "${AZURE_OPENAI_DEPLOYMENT_NAME}" \
  --message-array "${MESSAGE_ARRAY}" \
  --max-tokens 100 \
  --temperature 0.7