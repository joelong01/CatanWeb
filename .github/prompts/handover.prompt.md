---
name: handover
description: Perform a full session handover while following ai-rules.md.
agent: agent
argument-hint: "Optional: extra context to include in this handover"
---

# files:"./.ai/ai-rules.md"

# files:"./.ai/commands/handover.md"

Load and fully follow the rules defined in `.ai/ai-rules.md`.

Then load the canonical handover instructions from
`.ai/commands/handover.md` and execute them exactly as written.

If the user supplies extra context after invoking `/handover`, treat that
as additional information from the current developer and incorporate it
into the resulting handover.

Begin by summarizing your understanding of the ai-rules, then perform the
handover workflow as defined in the handover command file.
