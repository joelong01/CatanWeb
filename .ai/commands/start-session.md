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

### 2. Check Git Status and Project Structure

Verify the current branch and working directory state:

```bash
git status
git log --oneline
```

**Current State Information**:

- Check which branch you're on and its relationship to origin
- Verify working directory is clean or review any uncommitted changes
- Review recent commits to understand latest work
- Get familiar with all files in the repository for context when files are referenced by name

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
