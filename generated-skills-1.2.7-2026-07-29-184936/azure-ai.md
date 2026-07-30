---
title: Azure skill for Azure AI Services
description: This skill connects your app to Azure AI services: Search, Speech, OpenAI, and Document Intelligence. It's for building semantic or vector search, hybrid queries, speech-to-text and text-to-speech, transcription, and OCR, so you can add AI features quickly.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure AI Services

This skill connects your app to Azure AI services: Search, Speech, OpenAI, and Document Intelligence. It's for building semantic or vector search, hybrid queries, speech-to-text and text-to-speech, transcription, and OCR, so you can add AI features quickly. Use `azure-ai` when you need to quickly add AI-powered capabilities—such as semantic or vector-based (including hybrid) search across your content. Speech or document processing (transcription, text-to-speech, OCR)—to make your app searchable, conversational, and accessible.

**Skill** `azure-ai` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-AI/SKILL.md)

## What it provides

Add powerful search, speech, and document intelligence to your app—including semantic, vector, hybrid, and query search; speech-to-text, streaming transcription, and text-to-speech; and OCR-based extraction and structured data from documents. Use Azure AI Search, Speech, OpenAI, and Document Intelligence to power relevance with embeddings, generate conversational responses, and automate document understanding.

### Azure services knowledge

| Service | When to use |
|---------|------------|
| AI Search | Full-text, vector, hybrid search |
| Speech | Speech-to-text, text-to-speech |
| OpenAI | GPT models, embeddings, DALL-E |
| Document Intelligence | Form extraction, OCR |

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`

## When to use this skill

Use the **Azure AI Services** skill when you need to:

- Manage and configure AI Search in Azure
- Query search in Azure
- Manage and configure vector search, hybrid search, semantic search, and speech-to-text in Azure
- Manage and configure text-to-speech, transcribe, and convert text to speech in Azure

## Example prompts

Try these prompts to activate this skill:

- "How do I work with AI Search?"
- "How do I query search?"
- "How do I work with vector search?"
- "How do I work with hybrid search?"
- "How do I work with semantic search?"
- "How do I work with speech-to-text?"
- "How do I work with text-to-speech?"
- "How do I work with transcribe?"

## Related content

- [Azure AI Services overview](/azure/AI-services/what-are-AI-services)
- [Azure AI Services quickstart](/azure/AI-services/reference)
- [Azure AI Services pricing](https://azure.microsoft.com/pricing/details/cognitive-services/)
- [Azure AI Services documentation](/azure/AI-services/)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-AI/SKILL.md)