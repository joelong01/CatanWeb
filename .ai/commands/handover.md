# Handover Command

Before running this command, you MUST load and follow the shared rules
defined in `./.ai/ai-rules.md`. These rules govern behavior, output
format, tool usage, and all other expectations for this workflow.

This command orchestrates the two core workflow steps

1. **pre-checkin.md**  
   Validate and prepare the repository for check-in.

2. **checkin.md**  
   Perform the actual commit(s), documentation updates, and final reporting.

---

Perform a complete session handover by running the two core preparation
commands in sequence:

1. **pre-checkin.md**  
   Validate and prepare the repository for commit by running the full
   pre-checkin workflow:
   - Clean build
   - Execute all tests
   - Run linters and formatters
   - Fix issues or document failures
   - Ensure the repository is in a check-in–ready state

2. **checkin.md**  
   Once the repository is verified to be clean and stable, perform the
   check-in workflow:
   - Review diffs
   - Organize changes into logical commits
   - Update CLAUDE.md and any relevant documentation
   - Create high-quality commits with clear messages
   - Produce a final check-in report

## Instructions

- Run **pre-checkin.md** first.  
  If the repo is *not* ready for check-in, stop and report the issue.

- If pre-checkin passes (or remaining issues are documented and acceptable),
  run **checkin.md** to produce the final commit(s) and documentation updates.

- After both commands complete, provide a concise summary including:
  - Whether pre-checkin succeeded
  - What commits were created
  - Which files changed
  - Any remaining issues for the next session
  - Clear next steps

This command should not re-implement any logic; it simply orchestrates the
two workflows and returns a unified final report.
