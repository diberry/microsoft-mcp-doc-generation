---

title: Azure MCP Server tools for Azure Cloud Architect
description: Use Azure MCP Server tools to manage cloud architecture design, generate diagrams and templates, validate architectures against best practices, and estimate costs with natural language prompts from your IDE.
ms.date: 03/26/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 1
mcp-cli.version: 2.0.0-beta.32+2c60db5464260bc365d41f1049a9c1dc5a11281f
author: diberry
ms.author: diberry
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Cloud Architect

The Azure MCP Server lets you manage cloud architecture design and guidance, including design, with natural language prompts.

Azure Cloud Architect helps you generate cloud architecture designs and best-practice recommendations. For more information, see [Azure Cloud Architect documentation](/azure/architecture/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Get cloudarchitect design

<!-- @mcpcli cloudarchitect design -->

This Model Context Protocol (MCP) tool recommends architecture designs for cloud services, apps, and solutions, such as file storage, banking, video streaming, e-commerce, and SaaS. This tool interacts with you through short, focused questions, tracks confidence in requirements, and produces actionable, high-level architecture guidance.

How this tool works:
- This tool asks 1–2 clarifying questions at a time about your role, business goals, constraints, and priorities.
- This tool tracks a confidence score between 0.0 and 1.0 and updates requirements accordingly. When confidence reaches 0.7 or higher, the tool stops asking questions.
- This tool presents architecture recommendations using tables, clear visual organization, and ASCII diagrams. The recommendations follow the Azure Well‑Architected Framework.
- This tool covers all tiers: infrastructure, platform, application, data, security, and operations.
- This tool provides actionable advice and a high-level overview. The state tracks components, requirements by category (explicit, implicit, assumed), and confidence factors. The tool favors conservative suggestions.

Example prompts include:

- "Design an architecture for a large-scale file upload, storage, and retrieval service."
- "Help me design an Azure cloud service that operates as an ATM for users."
- "I want to design a cloud app for ordering groceries and managing deliveries."
- "How can I design an Azure cloud service to store and present video content to users?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Answer** |  Optional | The user's response to the question. |
| **Confidence score** |  Optional | A value between 0.0 and 1.0 representing confidence in understanding requirements. When this reaches 0.7 or higher, nextQuestionNeeded should be set to false. |
| **Next question needed** |  Optional | Whether another question is needed. |
| **Question** |  Optional | The current question being asked. |
| **Question number** |  Optional | Current question number. |
| **State** |  Optional | The complete architecture state from the previous request as JSON, State input schema:
{
&quot;state&quot;:{
&quot;type&quot;:&quot;object&quot;,
&quot;description&quot;:&quot;The complete architecture state from the previous request&quot;,
&quot;properties&quot;:{
&quot;architectureComponents&quot;:{
&quot;type&quot;:&quot;array&quot;,
&quot;description&quot;:&quot;All architecture components suggested so far&quot;,
&quot;items&quot;:{
&quot;type&quot;:&quot;string&quot;
}
},
&quot;architectureTiers&quot;:{
&quot;type&quot;:&quot;object&quot;,
&quot;description&quot;:&quot;Components organized by architecture tier&quot;,
&quot;additionalProperties&quot;:{
&quot;type&quot;:&quot;array&quot;,
&quot;items&quot;:{
&quot;type&quot;:&quot;string&quot;
}
}
},
&quot;thought&quot;:{
&quot;type&quot;:&quot;string&quot;,
&quot;description&quot;:&quot;The calling agent's thoughts on the next question or reasoning process. The calling agent should use the requirements it has gathered to reason about the next question.&quot;
},
&quot;suggestedHint&quot;:{
&quot;type&quot;:&quot;string&quot;,
&quot;description&quot;:&quot;A suggested interaction hint to show the user, such as 'Ask me to create an ASCII art diagram of this architecture' or 'Ask about how this design handles disaster recovery'.&quot;
},
&quot;requirements&quot;:{
&quot;type&quot;:&quot;object&quot;,
&quot;description&quot;:&quot;Tracked requirements organized by type&quot;,
&quot;properties&quot;:{
&quot;explicit&quot;:{
&quot;type&quot;:&quot;array&quot;,
&quot;description&quot;:&quot;Requirements explicitly stated by the user&quot;,
&quot;items&quot;:{
&quot;type&quot;:&quot;object&quot;,
&quot;properties&quot;:{
&quot;category&quot;:{
&quot;type&quot;:&quot;string&quot;
},
&quot;description&quot;:{
&quot;type&quot;:&quot;string&quot;
},
&quot;source&quot;:{
&quot;type&quot;:&quot;string&quot;
},
&quot;importance&quot;:{
&quot;type&quot;:&quot;string&quot;,
&quot;enum&quot;:[
&quot;high&quot;,
&quot;medium&quot;,
&quot;low&quot;
]
},
&quot;confidence&quot;:{
&quot;type&quot;:&quot;number&quot;
}
}
}
},
&quot;implicit&quot;:{
&quot;type&quot;:&quot;array&quot;,
&quot;description&quot;:&quot;Requirements implied by user responses&quot;,
&quot;items&quot;:{
&quot;type&quot;:&quot;object&quot;,
&quot;properties&quot;:{
&quot;category&quot;:{
&quot;type&quot;:&quot;string&quot;
},
&quot;description&quot;:{
&quot;type&quot;:&quot;string&quot;
},
&quot;source&quot;:{
&quot;type&quot;:&quot;string&quot;
},
&quot;importance&quot;:{
&quot;type&quot;:&quot;string&quot;,
&quot;enum&quot;:[
&quot;high&quot;,
&quot;medium&quot;,
&quot;low&quot;
]
},
&quot;confidence&quot;:{
&quot;type&quot;:&quot;number&quot;
}
}
}
},
&quot;assumed&quot;:{
&quot;type&quot;:&quot;array&quot;,
&quot;description&quot;:&quot;Requirements assumed based on context/best practices&quot;,
&quot;items&quot;:{
&quot;type&quot;:&quot;object&quot;,
&quot;properties&quot;:{
&quot;category&quot;:{
&quot;type&quot;:&quot;string&quot;
},
&quot;description&quot;:{
&quot;type&quot;:&quot;string&quot;
},
&quot;source&quot;:{
&quot;type&quot;:&quot;string&quot;
},
&quot;importance&quot;:{
&quot;type&quot;:&quot;string&quot;,
&quot;enum&quot;:[
&quot;high&quot;,
&quot;medium&quot;,
&quot;low&quot;
]
},
&quot;confidence&quot;:{
&quot;type&quot;:&quot;number&quot;
}
}
}
}
}
},
&quot;confidenceFactors&quot;:{
&quot;type&quot;:&quot;object&quot;,
&quot;description&quot;:&quot;Factors that contribute to the overall confidence score&quot;,
&quot;properties&quot;:{
&quot;explicitRequirementsCoverage&quot;:{
&quot;type&quot;:&quot;number&quot;
},
&quot;implicitRequirementsCertainty&quot;:{
&quot;type&quot;:&quot;number&quot;
},
&quot;assumptionRisk&quot;:{
&quot;type&quot;:&quot;number&quot;
}
}
}
}
}
}. |
| **Total questions** |  Optional | Estimated total questions needed. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure Architecture Center documentation](/azure/architecture/)