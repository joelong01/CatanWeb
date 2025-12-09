# Handover Workflow

**Purpose:** Complete session handover by documenting work, validating quality, and creating commits.

Before running this workflow, you MUST load and follow the shared rules
defined in `./.ai/ai-rules.md`. These rules govern behavior, output
format, tool usage, and all other expectations.

---

## Workflow Steps

This workflow orchestrates three commands in sequence. Each command is
defined in its own file - **load and execute each command file** rather
than re-implementing the logic here.

Execute the following steps in order:

### Step 1: Create Session Summary

**File:** `.ai/commands/sessions.md`

**Action:** Read the file and follow all instructions to create a comprehensive
session summary.

**Purpose:** Documents work completed, decisions made, blockers, and next steps.
The summary file will be committed with code changes in step 3.

**Skip condition:** If a session summary for today already exists, ask the user:
- Skip (keep existing)
- Regenerate (overwrite existing)
- Review (show existing, then decide)

---

### Step 2: Validate Repository

**File:** `.ai/commands/pre-checkin.md`

**Action:** Read the file and follow all instructions to validate the repository
is ready for check-in.

**Purpose:** Ensures builds pass, tests pass, linters are clean. Any issues must
be fixed or documented.

**Stop condition:** If critical failures occur that cannot be fixed, stop the
workflow and report to the user. Do not proceed to step 3.

---

### Step 3: Create Commits

**File:** `.ai/commands/checkin.md`

**Action:** Read the file and follow all instructions to create commits.

**Purpose:** Reviews diffs, organizes changes into logical commits, updates
documentation, creates high-quality commit messages. The session summary from
step 1 will be included when staging files.

**Prerequisites:** Step 2 must have passed (or documented acceptable issues).

---

## Execution Instructions for AI

**IMPORTANT:** This is a workflow orchestrator. Do NOT re-implement the logic
of individual commands. Instead:

1. **Read each command file** using the Read tool
2. **Follow the instructions** in that file exactly
3. **Use the tools** (Bash, Edit, Write, etc.) as directed by each command
4. **Report progress** after each step completes
5. **Stop if directed** by skip/stop conditions

### Execution Flow

```
1. Load .ai/commands/sessions.md
   → Execute all steps in that file
   → Confirm session summary created

2. Load .ai/commands/pre-checkin.md
   → Execute all steps in that file
   → If critical failures: STOP and report
   → Otherwise: Continue

3. Load .ai/commands/checkin.md
   → Execute all steps in that file
   → Session summary will be committed here
   → Confirm commits created
```

---

## Final Workflow Report

After completing all three steps (or stopping early), provide this summary:

```text
Handover Workflow Complete
===========================

✅ Step 1: Session Summary
   File: .ai/sessions/SESSION_SUMMARY-{date}-{hhmm}.md
   Status: Created / Skipped / Failed
   Notes: {brief notes}

✅ Step 2: Pre-Checkin Validation
   Build: Pass / Fail
   Tests: {X} passed, {Y} failed
   Linters: Clean / Issues documented
   Status: Ready for commit / Blocked
   Notes: {brief notes}

✅ Step 3: Check-In
   Commits created: {N}
   Files changed: {N}
   Commits:
   - {hash}: {message}
   - {hash}: {message}
   Status: Complete / Skipped / Failed
   Notes: {brief notes}

Overall Status: {Success / Partial / Failed}

Next Steps:
- {action item 1}
- {action item 2}
```

---

## Notes

- Each command file is the **canonical source** for that step
- This workflow file only **orchestrates** - it doesn't duplicate logic
- If command files are updated, the workflow automatically uses new logic
- Keep this file focused on flow control, not implementation details
