# Handover Workflow

**Purpose:** Complete session handover by documenting work, validating quality,
creating commits, and opening a pull request.

Before running this workflow, you MUST load and follow the shared rules
defined in `./.ai/ai-rules.md`. These rules govern behavior, output
format, tool usage, and all other expectations.

---

## Workflow Steps

This workflow orchestrates multiple steps in sequence. Some steps delegate to
command files - **load and execute each command file** rather than
re-implementing the logic here.

Execute the following steps in order:

### Step 0: Branch Safety Check

**Action:** Verify we are NOT on the `main` branch. Direct pushes to main are
not allowed.

**Commands to run:**

```bash
# Get current branch name
git branch --show-current
```

**If on `main` branch:**

1. Check if there are uncommitted changes: `git status --porcelain`
2. If changes exist, create a new branch and switch to it:
   - Generate branch name from today's date and brief description of work
   - Format: `{category}/{description}` (e.g., `feat/font-awesome-migration`,
     `fix/ios-connection-starvation`)
   - Ask the user for a branch name suggestion, or propose one based on the
     session's work
3. Run: `git checkout -b {branch-name}`
4. Confirm the branch switch succeeded

**If already on a feature branch:** Continue to Step 1.

**Stop condition:** If branch creation fails or user declines to create a
branch, stop the workflow. Cannot proceed on main.

---

### Step 1: Create Session Summary

**File:** `.ai/commands/session-summary.md`

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

### Step 4: PR Code Review

**Action:** Create a thorough code review of all changes before creating the PR.
This gives us an opportunity to catch and fix issues before external review.

**File:** `.ai/commands/code-review.md`

**Output Location:** `.code-reviews/prs/PR-{branch-name}-{date}.md`

**Process:**

1. Read `.ai/commands/code-review.md` for review guidelines
2. Get the full diff of changes on this branch vs main:

   ```bash
   git diff main...HEAD
   git diff main...HEAD --stat
   ```

3. Review all changes following the code-review.md guidelines:
   - Architecture and design
   - Code quality and standards compliance
   - Security considerations
   - Performance implications
   - Documentation completeness

4. Create the review file at `.code-reviews/prs/PR-{branch-name}-{date}.md`

5. If critical issues are found:
   - Fix them before proceeding to PR creation
   - Update commits as needed (amend or new commit)
   - Re-run validation (Step 2) if significant changes made

6. **REQUIRED: Show review to user and get approval**
   - Display the full code review to the user
   - Summarize: Critical issues, Important issues, Suggestions
   - Ask: "Do you want me to fix any of these issues before creating the PR?"
   - Wait for user response before proceeding to Step 5
   - If user requests fixes, implement them and update the review

**Review File Format:**

```markdown
# PR Code Review: {branch-name}

**Branch:** {branch-name}
**Base:** main
**Reviewed:** {YYYY-MM-DD}
**Reviewer:** {AI Model Name}

## Summary

{2-3 sentences about the PR scope and purpose}

## Changes Overview

{List of commits and their purposes}

## Files Changed

| File | Changes | Risk |
|------|---------|------|
| path/to/file | Description | Low/Medium/High |

## Critical Issues

{Issues that MUST be fixed before merge - or "None" if clean}

## Important Issues

{Issues that SHOULD be fixed - or "None"}

## Suggestions

{Nice-to-have improvements}

## Security Review

{Security considerations and findings}

## Testing Verification

{Test coverage and manual testing notes}

## Approval Status

- [ ] No critical issues
- [ ] Build passes
- [ ] Tests pass
- [ ] Ready for PR
```

**Prerequisites:** Step 3 must have completed with at least one commit.

---

### Step 5: Create Pull Request

**Action:** Push the branch and create a pull request for review.

**Commands to run:**

```bash
# Push branch to remote (with upstream tracking)
git push -u origin HEAD

# Create PR using GitHub CLI
gh pr create --title "{title}" --body "{body}"
```

**PR Title:** Generate from the commit messages or session summary. Should be
concise and descriptive (e.g., "Add Font Awesome icons for cross-browser
compatibility").

**PR Body Format:**

```markdown
## Summary

{2-4 bullet points describing the changes}

## Changes

- {List of key files/areas changed}

## Testing

- [ ] Build passes
- [ ] Tests pass
- [ ] Manual verification (if applicable)

## Session Summary

{Link to or excerpt from the session summary file}

---
Generated with [Claude Code](https://claude.ai/code)
```

**Prerequisites:** Step 4 must have completed with a clean code review.

**Skip condition:** If the user explicitly says they don't want a PR yet, skip
this step and note it in the final report.

### Post-PR: Wait for CI

After creating the PR, remind the user:

1. **Do NOT merge until GitHub Actions CI passes** - The PR will run automated
   checks (build, tests, linting, security scans)
2. **Check CI status:** `gh pr checks` or view the PR page on GitHub
3. **If CI fails:** Fix the issues, push new commits, wait for CI to pass again
4. **Only merge after all checks are green**

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

```text
0. Branch Safety Check
   → Run: git branch --show-current
   → If on main: Create feature branch (ask user for name)
   → If branch creation fails: STOP
   → Otherwise: Continue

1. Load .ai/commands/session-summary.md
   → Execute all steps in that file
   → Confirm session summary created in .ai/sessions/

2. Load .ai/commands/pre-checkin.md
   → Execute all steps in that file
   → If critical failures: STOP and report
   → Otherwise: Continue

3. Load .ai/commands/checkin.md
   → Execute all steps in that file
   → Session summary will be committed here
   → Confirm commits created

4. PR Code Review
   → Get diff: git diff main...HEAD
   → Review all changes per code-review.md
   → Create .code-reviews/prs/PR-{branch}-{date}.md
   → **Show review to user and summarize findings**
   → **Ask user if they want fixes before PR**
   → Fix any issues user requests

5. Create Pull Request
   → Push branch: git push -u origin HEAD
   → Create PR: gh pr create
   → Return PR URL to user
```

---

## Final Workflow Report

After completing all steps (or stopping early), provide this summary:

```text
Handover Workflow Complete
===========================

✅ Step 0: Branch Safety
   Branch: {branch-name}
   Status: Already on feature branch / Created new branch / BLOCKED (on main)
   Notes: {brief notes}

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

✅ Step 4: PR Code Review
   File: .code-reviews/prs/PR-{branch}-{date}.md
   Critical Issues: {N} found / None
   Status: Clean / Issues fixed / Issues remain
   Notes: {brief notes}

✅ Step 5: Pull Request
   PR URL: {url}
   Title: {pr-title}
   Status: Created / Skipped / Failed
   Notes: {brief notes}

Overall Status: {Success / Partial / Failed}

⏳ CI Status: PENDING - Do NOT merge until all checks pass
   Check with: gh pr checks

Next Steps:
- Wait for GitHub Actions CI to pass
- {action item 1}
- {action item 2}
```

---

## Notes

- Each command file is the **canonical source** for that step
- This workflow file only **orchestrates** - it doesn't duplicate logic
- If command files are updated, the workflow automatically uses new logic
- Keep this file focused on flow control, not implementation details
