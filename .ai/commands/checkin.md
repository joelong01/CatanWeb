# Check-In Command

Prepare the repository for a clean, intentional commit by organizing work,
reviewing changes, and updating summary/documentation files.

> **Important:** Before executing this command, read `.ai/ai-rules.md` for project
> conventions, commit message formats, and documentation standards.
> **Note:** This command focuses on *what gets committed* and how it is
> documented. Pre-checkin validation (tests, lint, build) is handled by a
> separate command.

---

## Command Purpose

Create one or more high-quality commits that:

- Represent coherent units of work
- Are self-documenting via commit messages
- Are backed by up-to-date project summary files
- Leave the working tree in a clean, understandable state

---

## Actions to Perform

### 1. Assess the Current Working Tree

1. Run `git status` and determine:
   - Which files are modified, added, deleted, or renamed
   - Which changes are staged vs unstaged
2. Identify:
   - Which changes are ready for commit
   - Which are experimental or WIP and should **not** be committed yet
3. If necessary, create or update a short scratch note (e.g. in `CLAUDE.md`
   or a local notes file) summarizing what you think this commit is about.

---

### 2. Review Diffs and Perform Self Code Review

For each file that will be part of this check-in:

1. Run `git diff` (and `git diff --staged` if there is staged work).
2. Carefully review changes for:
   - Accidental debug code (e.g. extra logs, print statements, breakpoints)
   - Temporary flags, hard-coded paths, or feature toggles
   - Comments that were meant as scratch notes and shouldn't be checked in
   - Obvious style issues or small refactors that should be done now
3. Make targeted edits to:
   - Clarify confusing code
   - Rename poorly named variables/functions
   - Split overly long functions/blocks if it improves clarity
4. Re-run `git diff` to confirm:
   - The patch is coherent
   - There are no surprising or unrelated changes bundled in

Goal: by the end of this step, every line in the diff has been consciously
reviewed and justified.

---

### 3. Organize Changes Into Logical Commits

1. Decide on **logical units of work**:
   - Example: "Implement feature X", "Refactor Y", "Fix bug Z", "Docs update"
2. Use `git add -p` (or equivalent) to:
   - Stage changes in **coherent chunks**
   - Avoid mixing unrelated edits in the same commit
3. For experimental or half-baked work:
   - Either:
     - Keep it unstaged for later, **or**
     - Create a separate WIP commit on a feature branch
   - Do **not** mix production-ready code with speculative experiments in the
     same commit

---

### 4. Update Project Summary and Documentation Files

Keep the project's "narrative" in sync with the commit.

#### 4.1 Update `.ai/project-summary.md`

1. Ensure `.ai/project-summary.md` captures the scope of *this* commit:
   - Brief description of the work being committed
   - Any notable architectural or design decisions
   - New or changed dependencies or configuration
   - Known issues related to this change
   - TODOs or follow-up items that won't be handled in this commit
2. Maintain existing style and MD linter rules (line length, headings, etc.).

#### 4.2 Update Other Documentation as Needed

Depending on the nature of the changes:

- **README.md**
  - New features or capabilities
  - Updated setup steps or usage examples
- **CHANGELOG.md** (if present)
  - Add an entry under the appropriate version/release section
- Any relevant docs (e.g. `/docs`, ADRs)
  - Update diagrams, references, or examples to reflect this commit

Documentation should make it clear to a future reader **what changed and why**.

---

### 5. Craft High-Quality Commit Messages

For each logical commit you are about to create:

1. Use a concise, informative subject line, e.g.:

   ```text
   <type>: <short summary>
   ```

   Where `<type>` is one of: `feat`, `fix`, `refactor`, `docs`, `chore`, `test`, `style`

2. Write a detailed body explaining:
   - **What** changed (concise summary)
   - **Why** this change was needed
   - **How** it was implemented (if not obvious from the diff)
   - **Impact** on other parts of the system

3. Reference any related issues or tickets

4. Follow the project's commit message conventions from `.ai/ai-rules.md`

---

### 6. Create Session Summary

After committing your work, create a session summary file to document the work:

1. Create `.ai/sessions/SESSION_SUMMARY-{date}-{hhmm}.md` where:
   - `{date}` is in format `YYYY-MM-DD`
   - `{hhmm}` is the current time in 24-hour format (e.g., `1430` for 2:30 PM)

2. Document the following sections:

   ```markdown
   # Session Summary - {date}

   ## Work Completed
   - Bulleted list of what was accomplished
   - Major features, fixes, or refactorings
   - Infrastructure or tooling improvements

   ## Work in Progress
   - Any incomplete work or partially implemented features
   - Items that need follow-up

   ## Decisions Made
   - Key architectural or design decisions
   - Trade-offs considered and chosen approach
   - Rationale for implementation choices

   ## Blockers & Issues
   - Current blockers preventing progress
   - Known issues discovered but not yet resolved
   - Dependencies or external factors

   ## Next Session Priority
   1. Highest priority items for next session
   2. Logical next steps
   3. Follow-up tasks

   ## Important Context
   - Critical information for next session
   - Gotchas or non-obvious aspects
   - Key files or patterns to be aware of

   ## Environment Notes
   - Build/test status
   - Configuration changes
   - New dependencies or tools

   ## Quick Start for Next Session
   1. Commands to get started
   2. Current focus areas
   3. Files to review first
   ```

3. Be specific and actionable - future sessions depend on this summary

---

### 7. Final Check and Commit

1. Review staged changes one final time: `git diff --staged`
2. Create the commit: `git commit`
3. Verify the working tree is clean: `git status`
4. Add and commit the session summary file
5. Consider whether changes should be pushed to remote

---

## Output

After completing these steps, provide a summary report:

- Number of commits created
- Files changed in each commit
- Brief description of each commit's purpose
- Location of session summary file
- Any remaining uncommitted work
- Clear next steps for future work
