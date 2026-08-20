import { lookup } from "node:dns/promises";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

import { DefaultAzureCredential } from "@azure/identity";
import dotenv from "dotenv";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const defaultEnvPath = path.resolve(scriptDirectory, "..", "..", "mcp-tools", ".env");
const credentialScope = "https://cognitiveservices.azure.com/.default";

function parseBoolean(value) {
  return ["1", "true", "yes"].includes(String(value ?? "").trim().toLowerCase());
}

export function loadConfiguration(environment) {
  const endpoint = environment.FOUNDRY_ENDPOINT;
  const deployment = environment.FOUNDRY_MODEL_NAME
    ?? environment.FOUNDRY_MODEL
    ?? environment.FOUNDRY_INSTANCE;
  const apiVersion = environment.FOUNDRY_MODEL_API_VERSION;
  const apiKey = environment.FOUNDRY_API_KEY;
  const useDefaultCredential = parseBoolean(environment.FOUNDRY_USE_DEFAULT_CREDENTIAL);

  const missing = [];
  if (!endpoint) missing.push("FOUNDRY_ENDPOINT");
  if (!deployment) missing.push("FOUNDRY_MODEL_NAME");
  if (!apiVersion) missing.push("FOUNDRY_MODEL_API_VERSION");
  if (!apiKey && !useDefaultCredential) {
    missing.push("FOUNDRY_API_KEY or FOUNDRY_USE_DEFAULT_CREDENTIAL=true");
  }

  if (missing.length > 0) {
    throw new Error(`Missing required configuration: ${missing.join(", ")}`);
  }

  const endpointUrl = new URL(endpoint);
  if (endpointUrl.protocol !== "https:") {
    throw new Error("FOUNDRY_ENDPOINT must use HTTPS.");
  }

  return {
    endpoint,
    deployment,
    apiVersion,
    apiKey,
    useDefaultCredential,
  };
}

export function buildChatCompletionsUrl({ endpoint, deployment, apiVersion }) {
  const url = new URL(endpoint);
  url.pathname = `/openai/deployments/${encodeURIComponent(deployment)}/chat/completions`;
  url.search = new URLSearchParams({ "api-version": apiVersion }).toString();
  return url;
}

async function buildAuthorizationHeaders(configuration) {
  if (configuration.apiKey) {
    return { "api-key": configuration.apiKey };
  }

  const credential = new DefaultAzureCredential();
  const accessToken = await credential.getToken(credentialScope);
  return { Authorization: `Bearer ${accessToken.token}` };
}

export async function testEndpoint(configuration) {
  const requestUrl = buildChatCompletionsUrl(configuration);
  const addresses = await lookup(requestUrl.hostname, { all: true });
  console.log(`✓ DNS resolved ${requestUrl.hostname}: ${addresses.map(({ address }) => address).join(", ")}`);

  const authorizationHeaders = await buildAuthorizationHeaders(configuration);
  console.log(`✓ Authentication ready (${configuration.apiKey ? "API key" : "DefaultAzureCredential"})`);

  const response = await fetch(requestUrl, {
    method: "POST",
    headers: {
      ...authorizationHeaders,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      messages: [
        { role: "user", content: "Reply with exactly: endpoint-ok" },
      ],
    }),
    signal: AbortSignal.timeout(30_000),
  });

  const responseText = await response.text();
  if (!response.ok) {
    throw new Error(
      `Azure OpenAI returned HTTP ${response.status} ${response.statusText}: `
      + responseText.slice(0, 2_000),
    );
  }

  const responseBody = JSON.parse(responseText);
  const reply = responseBody.choices?.[0]?.message?.content;
  if (!reply) {
    throw new Error("Azure OpenAI returned a successful response without assistant content.");
  }

  console.log(`✓ Azure OpenAI request succeeded using deployment '${configuration.deployment}'`);
  console.log(`  Response: ${reply.trim()}`);
}

async function main() {
  const envArgumentIndex = process.argv.indexOf("--env");
  const envPath = envArgumentIndex >= 0 && process.argv[envArgumentIndex + 1]
    ? path.resolve(process.argv[envArgumentIndex + 1])
    : defaultEnvPath;

  const envResult = dotenv.config({ path: envPath, override: false, quiet: true });
  if (envResult.error) {
    throw new Error(`Unable to load environment file '${envPath}': ${envResult.error.message}`);
  }

  console.log(`Testing Azure OpenAI configuration from ${envPath}`);
  await testEndpoint(loadConfiguration(process.env));
}

const isMainModule = process.argv[1]
  && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);

if (isMainModule) {
  main().catch((error) => {
    console.error(`✗ Azure OpenAI endpoint test failed: ${error.message}`);
    process.exitCode = 1;
  });
}
