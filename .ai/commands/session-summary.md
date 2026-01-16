# Session Summary Command

Create a comprehensive session summary documenting work completed, decisions made, and context for the next session.

> **Important:** Before executing this command, read `.ai/ai-rules.md` for project conventions and documentation standards.

---

## Command Purpose

Generate a detailed session summary that:

- Documents all work completed during the session
- Captures key decisions and their rationale
- Identifies blockers and open issues
- Provides clear next steps for continuation
- Preserves critical context for future sessions

This ensures continuity between work sessions and creates a searchable history of project evolution.

---

## When to Create Session Summaries

Create a session summary:

- ✅ **At end of significant work session** (2+ hours of focused work)
- ✅ **Before handover to another developer/AI**
- ✅ **After completing a major feature or milestone**
- ✅ **When stopping with incomplete work**
- ✅ **After making important architectural decisions**

Do NOT create trivial summaries for:

- ❌ Quick bug fixes (< 30 minutes)
- ❌ Simple documentation updates
- ❌ Routine maintenance tasks
- ❌ Sessions with no substantial changes

---

## Information Gathering Phase

Before writing the summary, collect the following information:

### 1. Review Git Status and History

```bash
# See what changed
git status

# Review recent commits (if any made this session)
git log --oneline -10

# See uncommitted changes
git diff
git diff --staged

# Count modified files
git status --short | wc -l
```

### 2. Review Build and Test Status

```bash
# Verify build status
pwsh ./catan.ps1 build

# Check test status
pwsh ./catan.ps1 test

# Note any failures or warnings
```

### 3. Check Todo List and Tracking

- Review current todo list (if using TodoWrite tool)
- Identify completed vs. pending items
- Note any new tasks discovered during work

### 4. Recall Key Decisions

Think back through the session:
- What design choices were made?
- What alternatives were considered and rejected?
- What trade-offs were accepted?
- What assumptions were made?
- What patterns or conventions were established?

### 5. Identify Context and Gotchas

- What non-obvious things did you learn?
- What surprised you during implementation?
- What should the next session know immediately?
- What documentation did you rely on?
- What code patterns are important to maintain?

---

## Session Summary File Format

### File Location and Naming

Create: `.ai/sessions/SESSION_SUMMARY-{date}-{hhmm}.md`

Where:
- `{date}` is `YYYY-MM-DD` format (e.g., `2025-11-27`)
- `{hhmm}` is 24-hour time format (e.g., `1430` for 2:30 PM, `0900` for 9:00 AM)

Example: `.ai/sessions/SESSION_SUMMARY-2025-11-27-1430.md`

### Complete Template

```markdown
# Session Summary - {date} {hhmm}

**Session Duration:** ~{X} hours
**Build Status:** ✅ All projects building / ⚠️ Build issues (describe)
**Test Status:** ✅ All tests passing / ⚠️ {N} tests failing (list)
**Branch:** {current-branch-name}

## Work Completed

### Major Features
- [Feature 1]: Brief description of what was implemented
  - Key files: `path/to/file1.cs`, `path/to/file2.cs`
  - Related commits: {commit-hash} (if committed)

### Bug Fixes
- Fixed [issue description]
  - Root cause: [explanation]
  - Solution: [approach taken]

### Refactoring
- Refactored [component/area]
  - Before: [old approach]
  - After: [new approach]
  - Rationale: [why the change was needed]

### Infrastructure/Tooling
- Added/updated [tool/script/workflow]
- Configuration changes: [describe]

### Documentation
- Updated `.ai/project-summary.md` with [changes]
- Created/updated design docs: [list]
- Code review files: [list any created]

## Work in Progress

### Incomplete Features
- [Feature name]: Currently at [stage]
  - What's done: [bullet points]
  - What remains: [bullet points]
  - Blockers: [if any]

### Partially Implemented Changes
- [Description of partial work]
  - Files touched: [list]
  - Next steps to complete: [list]

### Experimental Code
- [Area of experimentation]
  - Purpose: [what you were exploring]
  - Findings: [what you learned]
  - Decision: [keep/discard/refine]

## Decisions Made

### Architecture Decisions
1. **[Decision Title]**
   - **Context:** [Why this decision was needed]
   - **Options Considered:**
     - Option A: [brief description] - Rejected because [reason]
     - Option B: [brief description] - **CHOSEN** because [reason]
   - **Implications:** [How this affects the codebase]
   - **Documentation:** Recorded in `design_docs/[file].md`

### Design Patterns
- Decided to use [pattern] for [use case]
  - Follows Desktop implementation at `DesktopApp/[file]`
  - Rationale: [explanation]

### Trade-offs
- Chose [approach X] over [approach Y]
  - Benefits: [list]
  - Costs: [list]
  - Future considerations: [note]

## Blockers & Issues

### Critical Blockers
- **[Blocker description]**
  - Impact: Cannot proceed with [task/feature]
  - Requires: [what's needed to unblock]
  - Workaround: [if any exists]

### Known Issues
- **[Issue description]**
  - Severity: Critical / Important / Minor
  - Location: `file.cs:123`
  - Impact: [who/what is affected]
  - Plan: [how to address]

### Technical Debt
- [Area with technical debt]
  - Current state: [description]
  - Ideal state: [what should be done]
  - Priority: High / Medium / Low

### External Dependencies
- Waiting on: [external factor]
  - Estimated resolution: [timeframe if known]
  - Impact: [what's blocked]

## Next Session Priority

1. **[Highest Priority Task]**
   - Why: [justification for priority]
   - Approach: [suggested next steps]
   - Files to start with: [list]

2. **[Second Priority]**
   - Depends on: [prerequisite tasks]
   - Estimated effort: [rough guess]

3. **[Third Priority]**
   - Context: [why this is important]
   - Notes: [any special considerations]

### Follow-Up Tasks
- [ ] [Specific actionable task]
- [ ] [Another task]
- [ ] [Check/verify something]

## Important Context

### Critical Information
- **Database Schema:** Updated to use nested `PlayerColors` structure
  - Migration: Run `./webui.ps1 database install` to rebuild
  - Backward compatibility: Old properties marked `[JsonIgnore]`

- **Breaking Changes:** [Any breaking changes made]
  - Affects: [what/who is impacted]
  - Migration path: [how to adapt]

### Gotchas & Non-Obvious Aspects
- Watch out for [specific issue]
  - Symptom: [what you'll see]
  - Cause: [why it happens]
  - Fix: [how to resolve]

- The [X] implementation differs from Desktop because [reason]
  - Desktop: `DesktopApp/[file]:line`
  - WebUI: `WebUI/[file]:line`
  - Justification: [explanation]

### Key Files & Patterns
- **[Area of codebase]:** Key files to know
  - `path/to/core/file.cs` - [role/purpose]
  - `path/to/helper/file.cs` - [role/purpose]

- **Pattern to maintain:** [Description of pattern]
  - Example: `file.cs:123-145`
  - Why: [rationale for pattern]

### Reference Documentation
- Relied heavily on: `design_docs/[file].md`
- Desktop reference: `DesktopApp/[component]`
- Useful resources: [external links or docs]

## Environment Notes

### Build Configuration

- All projects building successfully: Yes / No
- Build command: `pwsh ./catan.ps1 build`
- Build time: ~{N} seconds
- Warnings: [list any warnings]

### Test Status
- Total tests: {N}
- Passing: {N}
- Failing: {N}
- Skipped: {N}

**Failing Tests:**
- `TestName1` - [Reason: pre-existing / caused by this session]
- `TestName2` - [Reason]

### Configuration Changes
- Updated `webui.ps1` with database commands
- Modified `.ai/code-review.md` with AI instructions
- [Other config changes]

### New Dependencies
- Added package: [name@version]
  - Purpose: [why it was added]
  - Impact: [breaking changes or compatibility notes]

### Database Schema
- Current schema version: [if versioned]
- Migration needed: Yes / No
- Data migration: [any special steps]

## Quick Start for Next Session

### Immediate Actions

1. **Start Here:**

   ```bash
   # Pull latest changes (if working with team)
   git pull origin {branch-name}

   # Verify build
   pwsh ./catan.ps1 build

   # Check database is current
   pwsh ./catan.ps1 database doctor
   ```

2. **Review These Files First:**
   - `.ai/project-summary.md` - Current project state
   - `design_docs/[file].md` - Recent design decisions
   - `code-reviews/[file]-cr.md` - Outstanding review items

3. **Current Focus Area:**
   - Working on: [component/feature]
   - Key classes: `ClassName1`, `ClassName2`
   - Next task: [specific next step]

### Commands & Workflows

- **Run services:**

  ```bash
  pwsh ./catan.ps1 run
  ```

- **Database rebuild:**

  ```bash
  pwsh ./catan.ps1 database install
  ```

- **Run tests:**

  ```bash
  pwsh ./catan.ps1 test
  # Or specific project:
  dotnet test Tests/GameService
  ```

### Context to Load

- If continuing [feature], read:
  - `path/to/file1.cs` - [why]
  - `path/to/file2.cs` - [why]

- If addressing [blocker], know:
  - [Critical context]
  - [Relevant background]

### Open Questions

- Should we [decision to make]?
  - Context: [background]
  - Options: [alternatives]
  - Input needed: [from whom]
```

---

## Writing Guidelines

### Be Specific and Actionable

❌ **Vague:** "Updated some rendering code"
✅ **Specific:** "Refactored `BoardSvgGenerator.cs` to use `PlayerViewModel` instead of `PlayerProfile`, extracting colors via `GetRenderColors()` for principle of least privilege"

❌ **Vague:** "Fixed some issues"
✅ **Specific:** "Fixed 500 error in New Game page by rebuilding database with new `PlayerColors` schema via `./webui.ps1 database install`"

### Explain Decisions

Every decision should include:
- **What** was decided
- **Why** it was necessary
- **Alternatives** considered
- **Trade-offs** accepted
- **Documentation** location (if significant)

### Provide File References

Always include file paths and line numbers:
- ✅ `WebUI/Services/GameStateService.cs:36`
- ✅ Updated `BoardSvgGenerator.cs`, `BuildingSvgRenderer.cs`, `RoadSvgRenderer.cs`
- ❌ "Modified some renderer files"

### Make It Scannable

- Use **bold** for emphasis
- Use bullet points liberally
- Keep paragraphs short (2-3 sentences max)
- Use code blocks for commands and examples
- Group related items under headings

### Think About Your Future Self

Write as if explaining to someone who:
- Knows the codebase but hasn't seen your changes
- Needs to pick up exactly where you left off
- Will read this 2 weeks from now
- Might be debugging an issue you introduced

---

## After Writing the Summary

1. **Save the file** to `.ai/sessions/SESSION_SUMMARY-{date}-{hhmm}.md`

2. **Stage and commit it:**
   ```bash
   git add .ai/sessions/SESSION_SUMMARY-*.md
   git commit -m "docs: Add session summary for {date} {hhmm}"
   ```

3. **Update project-summary.md if needed:**
   - Major architectural changes should be reflected
   - New features should be documented
   - Current status should be updated

4. **Create todo list for next session if helpful:**
   - Extract action items from "Next Session Priority"
   - Add to issue tracker or project board if used

---

## Output Format

After creating the session summary, provide this report to the user:

```text
✅ Session Summary Created

📄 File: .ai/sessions/SESSION_SUMMARY-{date}-{hhmm}.md

📊 Summary Statistics:
- Work completed: {N} items
- Decisions made: {N} items
- Blockers: {N} items
- Next session priorities: {N} items

🎯 Top Priority for Next Session:
{Brief description of #1 priority}

📂 Key Files Modified:
- {file1}
- {file2}
- {file3}

🔄 Status:
- Build: {status}
- Tests: {status}
- Ready for handover: Yes/No

💡 Critical Context:
{One-line summary of most important thing to know}

---

To commit this summary:
  git add .ai/sessions/SESSION_SUMMARY-{date}-{hhmm}.md
  git commit -m "docs: Add session summary for {date} {hhmm}"
```

---

## Examples

See existing session summaries in `.ai/sessions/` directory for examples of well-written summaries that follow this format.
