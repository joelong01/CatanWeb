---
name: ai-rules
description: Load and follow the shared AI rules defined in .ai/ai-rules.md.
agent: agent
argument-hint: "Optional: extra context to apply with the rules"
---

# files:"./.ai/ai-rules.md"

Load and fully follow the rules contained in the file above.

If the user supplies additional text after invoking /ai-rules,
interpret it as extra context to which the rules should be applied.

Acknowledge that the rules have been loaded, summarize your understanding
in one short paragraph, and then apply them.
