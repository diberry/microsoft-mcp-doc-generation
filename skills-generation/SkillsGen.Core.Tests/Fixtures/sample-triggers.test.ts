import { describe, it, expect } from 'vitest';

const shouldTriggerPrompts: string[] = [
  'How do I create a storage account?',
  'List all my blob containers',
  "Upload a file to Azure Blob Storage",
  `Show me the storage metrics for my account`,
  'Configure CORS rules for my storage account',
  'What are my storage access keys?',
  'Create a shared access signature for a container',
  'How do I set up geo-redundant storage?',
];

const shouldNotTriggerPrompts: string[] = [
  'How do I deploy a web app?',
  'Create a virtual machine',
  "What's the weather today?",
  `How do I write a Python function?`,
  'Set up a Cosmos DB database',
];

describe('azure-storage triggers', () => {
  it.each(shouldTriggerPrompts)('should trigger for: %s', (prompt) => {
    expect(prompt).toBeTruthy();
  });

  it.each(shouldNotTriggerPrompts)('should not trigger for: %s', (prompt) => {
    expect(prompt).toBeTruthy();
  });
});
