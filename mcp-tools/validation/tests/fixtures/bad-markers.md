---
title: Article With Bad Markers
description: "This article has a malformed HTML comment marker."
author: testauthor
ms.author: testauthor
ms.date: 05/13/2025
ms.topic: concept-article
ms.custom: build-2025
ms.reviewer: somereviewer
mcp-cli.version: 1.0.0
---
# Article With Bad Markers

This article has one HTML comment that does not match the valid marker pattern
because there is no space between the opening delimiter and the content.

## Tool: Do Something

<!--nospace-->

Does something useful.

| Parameter | Required or optional | Description |
|-----------|----------------------|-------------|
| **Name** | Required | The resource name. |
