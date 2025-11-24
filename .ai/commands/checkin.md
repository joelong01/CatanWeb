# Check-In Command

Prepare the repository for a clean, intentional commit by organizing work,
reviewing changes, and updating summary/documentation files.

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
   - Comments that were meant as scratch notes and shouldn’t be checked in
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
   - Example: “Implement feature X”, “Refactor Y”, “Fix bug Z”, “Docs update”
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

Keep the project’s “narrative” in sync with the commit.

#### 4.1 Update `.ai/project-summary.md`

1. Ensure `.ai/project-summary.md` captures the scope of *this* commit:
   - Brief description of the work being committed
   - Any notable architectural or design decisions
   - New or changed dependencies or configuration
   - Known issues related to this change
   - TODOs or follow-up items that won’t be handled in this commit
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
