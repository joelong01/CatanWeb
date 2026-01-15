# Start Session Command

> **Important:** Before executing this command, read `.ai/ai-rules.md` for project
> conventions, patterns, and workflow standards that govern all development work.

## Quick Start Instructions for New AI Sessions

When starting a new session on the Catan project, follow these steps to get oriented efficiently:

### 1. Review Handover Documentation

Read these files in order to understand the current project state:

- `./design_docs` - a set of files the describe the design of the game.
- `./design_docs/WebUi-Design.md` is the overall design document
- `.ai/sessions/` - Find the most recent timestamped session context file (SESSION_SUMMARY-{date}-{hhmm}.md format)
- Check recent commits with `git log --oneline` to understand latest work

### 2. Check Git Status and Select Working Branch

Verify the current branch and working directory state:

```bash
git branch --show-current
git status
git log --oneline -10
```

**Branch Safety Check (REQUIRED):**

1. **If on `main` branch:** You MUST switch to or create a feature branch before
   making any changes. Direct work on main is not allowed.

2. **To switch to an existing branch:**

   ```bash
   git branch -a                    # List all branches
   git checkout {branch-name}       # Switch to existing branch
   ```

3. **To create a new branch:**

   ```bash
   git checkout -b {category}/{description}
   ```

   Branch naming convention:
   - `feat/` - New features (e.g., `feat/font-awesome-icons`)
   - `fix/` - Bug fixes (e.g., `fix/ios-connection-starvation`)
   - `docs/` - Documentation updates (e.g., `docs/update-readme`)
   - `refactor/` - Code refactoring (e.g., `refactor/simplify-state-machine`)
   - `test/` - Test additions/fixes (e.g., `test/add-replay-tests`)

4. **Ask the user** which branch to work on if unclear. Propose a branch name
   based on the task they describe.

**Current State Information**:

- Confirm you are on a feature branch (NOT main)
- Verify working directory is clean or review any uncommitted changes
- Review recent commits to understand latest work
- Get familiar with all files in the repository for context when files are
  referenced by name

## Notes for AI Sessions

- All PowerShell scripts require PowerShell 7+
- we build with build scripts -- for the WebUI we use `./webui.ps1`.  run the help command to see what is available
- When writing MD files, write Markdown Lint error free markdown. Pay special attendion to these rules:
  - MD003/heading-style: Heading style
  - MD013/line-length: Line length [Expected: 150; Actual: XXX]
  - MD040/fenced-code-language: Fenced code blocks should have a language specified
  - MD031/blanks-around-fences: Fenced code blocks should be surrounded by blank lines
  - MD036/no-emphasis-as-heading: Emphasis used instead of a heading
  - MD022/blanks-around-headings: Headings should be surrounded by blank lines [Expected: 1; Actual: 0; Below]
