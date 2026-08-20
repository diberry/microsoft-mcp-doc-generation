import assert from "node:assert/strict";
import test from "node:test";

import {
    buildChatCompletionsUrl,
    loadConfiguration,
} from "./test-endpoint.js";

test("builds the Azure OpenAI deployment URL from environment configuration", () => {
    const url = buildChatCompletionsUrl({
        endpoint: "https://example.cognitiveservices.azure.com/",
        deployment: "gpt-test",
        apiVersion: "2025-03-01-preview",
    });

    assert.equal(
        url.href,
        "https://example.cognitiveservices.azure.com/openai/deployments/gpt-test/chat/completions?api-version=2025-03-01-preview",
    );
});

test("loads required values without exposing credentials", () => {
    const configuration = loadConfiguration({
        FOUNDRY_ENDPOINT: "https://example.cognitiveservices.azure.com/",
        FOUNDRY_MODEL_NAME: "gpt-test",
        FOUNDRY_MODEL_API_VERSION: "2025-03-01-preview",
        FOUNDRY_API_KEY: "secret-value",
    });

    assert.deepEqual(configuration, {
        endpoint: "https://example.cognitiveservices.azure.com/",
        deployment: "gpt-test",
        apiVersion: "2025-03-01-preview",
        apiKey: "secret-value",
        useDefaultCredential: false,
    });
});

test("rejects incomplete endpoint configuration", () => {
    assert.throws(
        () => loadConfiguration({ FOUNDRY_ENDPOINT: "https://example.invalid/" }),
        /FOUNDRY_MODEL_NAME.*FOUNDRY_MODEL_API_VERSION/,
    );
});
