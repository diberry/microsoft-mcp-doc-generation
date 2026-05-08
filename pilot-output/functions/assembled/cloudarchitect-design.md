Recommends architecture design for cloud services/apps/solutions, such as: file storage, banking, video streaming, e-commerce, SaaS, and more. Use as follows:
1. Ask about user role, business goals, etc (1-2 questions at a time).
2. Track confidence returned by service and update requirements (explicit/implicit/assumed).
3. Repeat steps 1 and 2 as needed until confidence >= 0.7
4. Present architecture with table format, visual organization, ASCII diagrams.
5. Follow Azure Well-Architected Framework principles.
6. Cover all tiers: infrastructure, platform, application, data, security, operations.
7. Provide actionable advice and high-level overview. Note: State tracks components, requirements by category, and confidence factors. Be conservative with suggestions.

### Example CLI commands

Basic usage:

```azurecli
azmcp cloudarchitect design
```

With parameters:

```azurecli
azmcp cloudarchitect design --question <question> --question-number <question-number> --total-questions <total-questions> --answer <answer> --next-question-needed <next-question-needed> --confidence-score <confidence-score> --state <state>
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
| `--question` | string | The current question being asked |
| `--question-number` | string | Current question number |
| `--total-questions` | string | Estimated total questions needed |
| `--answer` | string | The user's response to the question |
| `--next-question-needed` | string | Whether another question is needed |
| `--confidence-score` | string | A value between 0.0 and 1.0 representing confidence in understanding requirements. When this reaches 0.7 or higher, nextQuestionNeeded should be set to false. |
| `--state` | string | The complete architecture state from the previous request as JSON, State input schema:
{
"state":{
"type":"object",
"description":"The complete architecture state from the previous request",
"properties":{
"architectureComponents":{
"type":"array",
"description":"All architecture components suggested so far",
"items":{
"type":"string"
}
},
"architectureTiers":{
"type":"object",
"description":"Components organized by architecture tier",
"additionalProperties":{
"type":"array",
"items":{
"type":"string"
}
}
},
"thought":{
"type":"string",
"description":"The calling agent's thoughts on the next question or reasoning process. The calling agent should use the requirements it has gathered to reason about the next question."
},
"suggestedHint":{
"type":"string",
"description":"A suggested interaction hint to show the user, such as 'Ask me to create an ASCII art diagram of this architecture' or 'Ask about how this design handles disaster recovery'."
},
"requirements":{
"type":"object",
"description":"Tracked requirements organized by type",
"properties":{
"explicit":{
"type":"array",
"description":"Requirements explicitly stated by the user",
"items":{
"type":"object",
"properties":{
"category":{
"type":"string"
},
"description":{
"type":"string"
},
"source":{
"type":"string"
},
"importance":{
"type":"string",
"enum":[
"high",
"medium",
"low"
]
},
"confidence":{
"type":"number"
}
}
}
},
"implicit":{
"type":"array",
"description":"Requirements implied by user responses",
"items":{
"type":"object",
"properties":{
"category":{
"type":"string"
},
"description":{
"type":"string"
},
"source":{
"type":"string"
},
"importance":{
"type":"string",
"enum":[
"high",
"medium",
"low"
]
},
"confidence":{
"type":"number"
}
}
}
},
"assumed":{
"type":"array",
"description":"Requirements assumed based on context/best practices",
"items":{
"type":"object",
"properties":{
"category":{
"type":"string"
},
"description":{
"type":"string"
},
"source":{
"type":"string"
},
"importance":{
"type":"string",
"enum":[
"high",
"medium",
"low"
]
},
"confidence":{
"type":"number"
}
}
}
}
}
},
"confidenceFactors":{
"type":"object",
"description":"Factors that contribute to the overall confidence score",
"properties":{
"explicitRequirementsCoverage":{
"type":"number"
},
"implicitRequirementsCertainty":{
"type":"number"
},
"assumptionRisk":{
"type":"number"
}
}
}
}
}
} |

