# AI Assistant Rules for Catan Project

**Last Updated:** 2025-11-24

This document defines the rules, conventions, and best practices for AI assistants working on the Catan project.

## AI Agent Agnosticism

**CRITICAL RULE:** All content in the `.ai/` directory must be **AI agent agnostic**.

### Requirements

- **No agent-specific references**: Files in `.ai/` cannot reference specific AI tools like "Claude", "Copilot", "ChatGPT", etc.
- **Generic terminology only**: Use "AI assistant", "AI agent", "code assistant" instead of brand names
- **Agent bindings separate**: Agent-specific configurations belong in their own directories:
  - `.claude/` - Claude Code specific commands and configurations
  - `.github/` - GitHub Copilot specific configurations
  - Future agents get their own directories as needed

### Rationale

The `.ai/` directory serves as the **single source of truth** for:

- Project rules and standards
- Code quality guidelines
- Architecture patterns
- Development workflows
- Testing requirements

These standards apply to **all AI assistants** regardless of vendor or tool. Agent-specific bindings (like command formats or
workflow files) reference back to `.ai/` content but live in their respective directories.

### Structure

```text
.ai/                          # AI-agnostic rules and documentation
├── ai-rules.md              # This file - comprehensive standards
├── code-review.md           # Code review guidelines
├── project-summary.md       # Current project state
├── sessions/                # Session summaries
└── commands/                # Generic command templates
    ├── pre-checkin.md
    ├── handover.md
    └── start-session.md

.claude/                      # Claude-specific bindings
├── commands/                # Reference .ai/commands/ content

.github/                      # GitHub-specific configurations
├── copilot-instructions.md  # References .ai/ content
└── workflows/               # GitHub Actions
```

### Enforcement

When creating or updating content in `.ai/`:

1. Review for agent-specific terminology
2. Replace with generic terms
3. Move agent-specific content to appropriate directory
4. Ensure agent bindings reference `.ai/` as source of truth

## Table of Contents

1. [AI Agent Agnosticism](#ai-agent-agnosticism)
2. [General Principles](#general-principles)
3. [Code Quality Standards](#code-quality-standards)
4. [Markdown Documentation Rules](#markdown-documentation-rules)
5. [File and Directory Conventions](#file-and-directory-conventions)
6. [Build and Development Workflow](#build-and-development-workflow)
7. [Architecture and Design Patterns](#architecture-and-design-patterns)
8. [Testing Requirements](#testing-requirements)
9. [Git and Version Control](#git-and-version-control)
10. [Design, Planning, and Review Workflow](#design-planning-and-review-workflow)

## General Principles

### React Port: Check Existing Implementations First

**CRITICAL RULE:** Before inventing something new when doing the React port, follow this hierarchy:

1. **Check the Blazor/Razor app first** - How did we implement this feature in `WebUI/`?
2. **Check the Desktop app second** - How did we implement it in `DesktopApp/`?
3. **Check the design documents** - Is it documented in `.design/ui/react/` or `.design/`?
4. **Ask the developer** - If not found in the above, ask how to proceed

This is the **most important rule for the port** because:

- It prevents inventing new patterns that conflict with existing mechanisms
- It ensures consistency across platforms (Blazor, Desktop, React)
- Data structures, API patterns, and state management already exist - use them
- Player colors, profiles, and other data come from existing database collections

**Example:** If you need player colors in React:

1. Check `WebUI/Pages/Game.razor` - How does Blazor get `PlayerColorMap`?
2. Check `WebUI/Components/Players/PlayersPanel.razor` - How are colors passed and used?
3. Trace the data flow back to its source (database, API, SignalR)
4. Implement the same pattern in React

### Minimize Changes

- Make **surgical, minimal modifications** - change as few lines as possible to achieve goals
- **Never delete or modify working code** unless absolutely necessary
- If there are existing build or test failures unrelated to your task, ignore them
- Focus only on the specific task at hand

### Validate Changes

- Always validate that changes don't break existing behavior
- Run builds and tests to verify no regressions
- Update documentation only if directly related to your changes

### Tool Usage Efficiency

- **Use parallel tool calling** - Make multiple independent tool calls in a single response
- Chain related bash/PowerShell commands with `&&` instead of separate calls
- Suppress verbose output (use `--quiet`, `--no-pager`, pipe to `grep`/`head` when appropriate)
- Stay in current working directory or child directories unless absolutely necessary

### Refactoring with Visual Studio

**IMPORTANT:** When code changes involve renaming symbols (classes, methods, properties, namespaces):

- **Ask the developer to use Visual Studio's Rename Symbol feature** (Ctrl+R, Ctrl+R)
- This is **faster, more efficient, and less error-prone** than manual refactoring
- Visual Studio updates all references across the entire solution automatically
- AI assistants should:
  1. Identify that a rename is needed
  2. Specify exactly what symbol to rename and to what
  3. Let the developer execute the rename in Visual Studio
  4. Continue with remaining implementation after rename is complete

**Example:**

```text
"I need to rename the PlayerData class to PlayerProfile. Please use Visual Studio's
Rename Symbol feature:
1. Open PlayerData.cs
2. Right-click on 'PlayerData' class name
3. Select 'Rename...' (or Ctrl+R, Ctrl+R)
4. Enter 'PlayerProfile'
5. Check 'Rename file' option
6. Let me know when complete and I'll continue with the next steps."
```

## Code Quality Standards

### C# Coding Standards

- Follow existing code style and patterns in the codebase
- Use modern C# features (pattern matching, null-coalescing, collection expressions)
- Prefer `var` for local variables when type is obvious
- Use expression-bodied members for simple properties and methods
- Always use meaningful variable and method names

### Code Documentation Standards

All code files must follow .NET and C# best practices for documentation:

**XML Documentation Comments (Required):**

- **Public classes, interfaces, enums**: Must have `/// <summary>` describing purpose
- **Public methods and properties**: Must have `/// <summary>` explaining what they do
- **Method parameters**: Use `/// <param name="paramName">` for each parameter
- **Return values**: Use `/// <returns>` to describe what method returns
- **Complex private methods**: Should have `/// <summary>` if logic is non-obvious

**Example:**

```csharp
/// <summary>
/// Renders a resource card component displaying resource type and count.
/// </summary>
/// <param name="resource">The type of resource to display (Wheat, Wood, etc.)</param>
/// <param name="count">The number of tiles with this resource on the board</param>
public class ResourceCard : ComponentBase
{
    /// <summary>
    /// The type of resource this card represents.
    /// </summary>
    [Parameter]
    public ResourceType Resource { get; set; }

    /// <summary>
    /// The count of this resource type on the game board.
    /// </summary>
    [Parameter]
    public int Count { get; set; }
}
```

**Inline Comments (Use Sparingly):**

- Only comment code that needs clarification (complex logic, non-obvious workarounds)
- Prefer self-documenting code with clear variable/method names
- Avoid obvious comments like `// Set x to 5` for `x = 5;`
- Do comment "why" not "what" for complex algorithms

**File-Level Comments:**

- Not required but recommended for complex components
- Should explain the component's role in the larger system
- Can include usage examples or important behavioral notes

**Blazor-Specific Documentation:**

- Document `[Parameter]` properties with XML comments
- Document `EventCallback` parameters explaining when/why they fire
- Add comments explaining component lifecycle usage (OnInitialized, OnParametersSet, etc.)
- Document any JavaScript interop with clear explanations

**When to Add Comments:**

- ✅ All new public APIs (classes, methods, properties)
- ✅ Complex business logic or algorithms
- ✅ Workarounds for known issues (include issue reference)
- ✅ Performance-critical code explaining optimizations
- ✅ Security-sensitive code explaining safeguards
- ❌ Trivial getters/setters with obvious purpose
- ❌ Self-evident code with clear naming

### CSS and Styling

- **Use CSS custom properties (variables)** defined in `:root` for all theming
- Define reusable variables in `wwwroot/css/app.css`:
  - Background colors: `--game-bg-primary`, `--game-bg-secondary`, `--game-bg-panel`
  - Text colors: `--text-primary`, `--text-secondary`, `--text-muted`
  - Accent colors: `--accent-primary`, `--accent-hover`, `--accent-success`, `--accent-error`
  - Overlays: `--overlay-dark`, `--overlay-darker`, `--overlay-light`, `--overlay-lighter`
  - Icons: `--icon-font-family`, `--icon-font-size`
- Never hardcode colors except in variable definitions
- Use Blazor scoped CSS (`.razor.css` files) for component-specific styles

### Icon Standards

- Use **Segoe MDL2 Assets** font for icons (matches Windows desktop app)
- Icon codes in HTML entity format: `&#xE710;` (not Unicode literals)
- Reference icon codes from Desktop XAML files for consistency
- Default icon size: `20px` (via `--icon-font-size` variable)
- Icons must be white/monochrome (no colored emoji)

### Naming Conventions

- **Components**: PascalCase (e.g., `ResourceCard.razor`, `StarCounter.razor`)
- **CSS classes**: kebab-case (e.g., `.nav-menu-item`, `.star-counter`)
- **JavaScript/TypeScript**: camelCase for variables, PascalCase for types
- **Files**: Match component/class names exactly

## Markdown Documentation Rules

All markdown files must be **Markdown Lint error-free**. Pay special attention to:

### Required Rules

- **MD003/heading-style**: Use ATX-style headings (`# Heading`)
- **MD013/line-length**: Maximum 150 characters per line
- **MD022/blanks-around-headings**: Headings must be surrounded by blank lines
- **MD031/blanks-around-fences**: Fenced code blocks must be surrounded by blank lines
- **MD036/no-emphasis-as-heading**: Don't use emphasis (`**bold**`) as headings
- **MD040/fenced-code-language**: Always specify language for code blocks

### Code Block Language Specifications

```csharp
// Use "csharp" for C# code
```

```css
/* Use "css" for stylesheets */
```

```bash
# Use "bash" for shell commands
```

```json
// Use "json" for JSON data
```

```text
Use "text" for plain output or when no language applies
```

### Linting Workflow

After writing or modifying any markdown file:

1. Run `npx markdownlint-cli "path/to/file.md" --fix` to auto-correct formatting.
2. Run `npx markdownlint-cli "path/to/file.md"` to report remaining issues.
3. Manually fix all reported issues before committing.

### Line Length Management

- Hard wrap at 150 characters
- Exception: Code blocks, URLs, and tables can exceed limit
- Use line breaks in lists and paragraphs to stay under limit

## File and Directory Conventions

### Project Structure

```text
Catan/
├── .ai/                    # AI assistant rules and documentation
├── .claude/                # Claude-specific commands and configurations
│   └── commands/          # Reusable command scripts
├── .design/               # Verified design documentation (30 docs)
│   ├── plans/            # Implementation plans awaiting approval
│   └── old/              # Legacy/superseded docs for reference
├── WebUI/
│   ├── Components/        # Reusable Blazor components
│   │   ├── Board/        # Board-related components
│   │   ├── Resources/    # Resource display components
│   │   └── Shared/       # Shared/utility components
│   ├── Layout/           # Layout components (MainLayout, NavMenu)
│   ├── Pages/            # Routable pages
│   └── wwwroot/
│       └── css/          # Global styles (app.css with CSS variables)
```

### File Naming

- **Design docs**: `kebab-case-design.md` (e.g., `board-measurement-design.md`)
- **Session summaries**: `SESSION_SUMMARY-YYYY-MM-DD-HHMM.md`
- **Blazor components**: `PascalCase.razor` with optional `PascalCase.razor.css`
- **Test images**: Store in `.test_images/` with descriptive names

### Ignored Files

Check `.gitignore` for excluded files:

- `.webui-pids.json` - WebUI process tracking
- `code-reviews/` - AI-generated code reviews
- `*.db`, `*.db-shm`, `*.db-wal` - Database files
- `.test-images/` - Test images for AI analysis (see Image Analysis section)

### Image Analysis

The `.test-images/` directory (excluded from git) contains images for AI assistant analysis:

- **Location**: `.test-images/` in project root
- **Purpose**: Store screenshots, UI mockups, and reference images for AI analysis
- **Usage pattern**: When user says "checkout foo.jpg", the file is located at `.test-images/foo.jpg`
- **File types**: Screenshots (.png, .jpg), design mockups, UI reference images
- **Git status**: Directory is in `.gitignore` - images are not committed to repository

**Common use cases:**

- UI comparison screenshots (Desktop vs WebUI)
- Design mockups for new features
- Bug reproduction images
- Visual regression testing references
- Architecture diagrams and flowcharts

## Build and Development Workflow

### Build Commands

Use `./catan.ps1` as the unified entry point for all development tasks:

- **PowerShell scripts**: Always use `pwsh` (PowerShell 7+), not `powershell` (legacy)
- **Build only**: `pwsh ./catan.ps1 build` (no tests)
- **Build with tests**: `pwsh ./catan.ps1 test`
- **Clean build**: `pwsh ./catan.ps1 clean` (preserves database)
- **Start development**: `pwsh ./catan.ps1 run` (build, start services, launch browser)
- **Check setup**: `pwsh ./catan.ps1 doctor`

### Development Workflow

1. Check build status: `pwsh ./catan.ps1 build`
2. Make minimal, surgical changes
3. Verify build: `pwsh ./catan.ps1 build`
4. Run relevant tests if applicable
5. Commit with clear message

### Hot Reload Considerations

- **Browser caching**: Hard refresh (Ctrl+Shift+R) after code changes
- **SVG caching**: Create new game to bypass cache or restart GameService
- **GameService restart required**: For SVG generation code changes
- **Blazor hot reload**: Some changes require full rebuild (`pwsh ./catan.ps1 update`)

### Testing Commands

- **All tests**: `pwsh ./catan.ps1 test`
- **Specific tests**: `dotnet test Tests/GameService --filter "TestName"`
- **With verbose**: Add `--verbosity normal` to see detailed output

## Architecture and Design Patterns

### Architectural Invariants (read first)

The load-bearing laws of this codebase live in
[`architecture-invariants.md`](./architecture-invariants.md) — the "constitution."
**Read it before any design or implementation work.** It is authoritative: when a
design doc or any subsection below disagrees with an invariant, the invariant wins.
In brief:

1. `GameModel` is the single runtime source of truth; the template is a
   creation-time factory input, never read at play time.
2. `GameState` is service-only; the client reads it, never derives it.
3. Client-only render/interaction options (glyphs, labels, keyboard shortcuts)
   live in the client, keyed by a shared enum — never in `GameModel`.
4. Enums are defined once in `Catan3.Shared` and generated to TypeScript via the
   type-gen pipeline; never hand-authored in `react-ui`.
5. Templates author only what varies per-template; each field routes at creation
   to `GameModel` (if authoritative) or nowhere (if the client knows it by enum).

### Blazor Component Model

- **Parameters**: Use `[Parameter]` attribute for component props
- **Events**: Use `EventCallback` for parent-child communication
- **Two-way binding**: Use `@bind` directive with `@bind:event`
- **Dependency injection**: Use `@inject` directive at top of `.razor` file
- **Scoped CSS**: Create `.razor.css` file with same name as component

### WebUI to Desktop Mapping

| Desktop (XAML) | WebUI (Blazor) | Notes |
|----------------|----------------|-------|
| UserControl | Component (.razor) | Reusable UI pieces |
| Binding `{x:Bind}` | `@bind` or `@` expressions | Data binding |
| Command | EventCallback | User interactions |
| Style/Resource | CSS variables | Theming |
| Grid/StackPanel | CSS Grid/Flexbox | Layout |

### State Management

> **Note:** The bullets below are legacy Blazor/desktop-era guidance. For the
> authoritative rules on where state lives, defer to
> [`architecture-invariants.md`](./architecture-invariants.md) (invariants 1–3).

- **SignalR for game state**: Real-time updates from GameService
- **Component state**: Local state in `@code` blocks
- **Query parameters**: For navigation context (e.g., `returnUrl`)
- **CSS variables**: For visual theming

### API Design

- **RESTful endpoints**: Use standard HTTP methods (GET, POST, PUT, DELETE)
- **Query parameters**: For filtering, pagination, optional features
- **Consistent caching**: Use `GameHash` for cache busting
- **Error handling**: Return meaningful error messages with appropriate status codes

## Testing Requirements

### Test Organization

```text
Tests/
├── Desktop/           # Desktop UI automation tests
├── GameService/       # GameService integration tests
├── Shared/           # Shared library tests
└── Data/             # Test scenario files (.catan_test)
```

### Testing Best Practices

- Use **ReplayTest pattern** for game scenario validation
- Test files stored in `Tests/Data/` directory (not embedded resources)
- Run tests before committing significant changes
- Document any failing tests with explanations
- Don't add new test tools unless necessary

## Git and Version Control

### Issue Tracking

- **File an issue before fixing**: Before starting work on a bug fix or feature, create a
  GitHub issue to track the work. Use the GitHub MCP tools to create the issue.
- **Branch name in title**: Prefix the issue title with the current branch name in brackets,
  e.g., `[longgame-perf] database install fails when project hasn't been built yet`
- **Keep issues focused**: One issue per bug or feature. Include steps to reproduce for bugs.
- **Reference issues in commits**: When committing, list the issues being fixed (see below).

### Branch Strategy

- **Main branch**: `main` - Production-ready code
- **Feature branches**: Named descriptively (e.g., `WebUI`, `board-measurement`)
- Check current branch: `git status`

### Commit Guidelines

- **ALWAYS ask permission**: AI assistants must ask the user for permission before creating any commit
- **Commit frequently**: Small, logical commits
- **Clear messages**: Describe what and why, not how
- **Reference issues**: Include `Fixes #<number>` or `Relates to #<number>` in the commit message
  body for each issue addressed by the commit
- **Group related changes**: Stage files that belong together
- **Test before commit**: Ensure build passes
- **Lint before commit**: Run `./catan.ps1 lint` to check all changed files:
  - This single command checks: C#, TypeScript/ESLint, Markdown, JSON, PowerShell, and spelling
  - Auto-fixes are applied where possible (use `-NoFix` to disable)
  - **Fix ALL lint errors before committing — including pre-existing ones.** The repo must be lint-clean
    after every PR. Do not ignore errors because they were there before your changes.
  - Use `./catan.ps1 lint all` to check entire codebase (slower)
  - Use `./catan.ps1 lint ts` to check only TypeScript, etc. (cs, ts, md, json, ps1, spell)
- **Format before every PR**: Run `./catan.ps1 format -All` to ensure consistent formatting.
  This avoids strange diffs caused by formatting inconsistencies between developers and AI agents.

### Git Best Practices

```bash
# Always check status first
git status

# Review changes before staging
git diff

# Stage related files
git add <files>

# Commit with clear message
git commit -m "Add board measurement design document"

# Use --no-pager for non-interactive output
git --no-pager status
git --no-pager log --oneline -10
```

### Pull Request Workflow

Before creating or merging a pull request, AI assistants must perform a code review:

1. **Review the diff**: Examine all changes between the feature branch and the target branch
2. **Run the build and tests**: Verify everything passes before the PR
3. **Self-review**: Check for issues listed in the project's code review guidelines
   (see `.ai/commands/code-review.md`)
4. **Add PR comments**: Post findings as comments on the pull request using GitHub CLI.
   Include both positive observations and any issues found
5. **List fixed issues**: Ensure the PR description lists all GitHub issues addressed
   (e.g., "Closes #100, Fixes #101")

### After Creating a PR

After creating a PR, **always** provide:

1. The PR link
2. AI code review instructions the developer can paste into another AI agent (Copilot,
   ChatGPT, another Claude session, etc.) to get an independent review

**Template for AI review instructions:**

````text
Review PR #{number}: {title}
Repository: {owner}/{repo}
URL: {pr_url}

Instructions:
1. Read `.ai/commands/code-review.md` for the review process
2. Run `gh pr diff {number}` to get the diff
3. For each changed file, read the FULL file (not just the diff)
4. Post each finding as a separate comment on PR #{number} using:
   `gh api repos/{owner}/{repo}/issues/{number}/comments -f body="..."`
5. Number findings sequentially (Finding 1/N, 2/N, etc.)
6. End with a summary table of all findings with severity
7. Follow the iterative cycle: review → comment → fix → verify
````

### Post-Merge Cleanup

After a PR is merged:

1. **Close referenced issues** with a comment linking to the merge commit:
   `gh issue close <number> -c "Fixed in commit <sha> (PR #<pr>)"`
2. **Partially addressed issues** get a comment instead of closing:
   `gh issue comment <number> -c "Partially addressed in PR #<pr>. Remaining: ..."`

This creates a traceable chain: **Issue → PR → Commit → Code**.

## Project-Specific Context

### Technology Stack

- **.NET 9.0**: Core framework (pinned via `global.json`)
- **Blazor WebAssembly**: WebUI frontend
- **ASP.NET Core**: GameService backend with SignalR
- **CosmosDB**: Database (local emulator + Azure)
- **SVG**: Dynamic board rendering
- **PowerShell 7+**: Build and automation scripts (use `pwsh` command, not `powershell`)

### Key Architectural Decisions

1. **Service Mode**: Desktop app can delegate to GameService or run locally
2. **CSS Variables**: All theming through CSS custom properties
3. **Component Reusability**: Shared components in `Components/` directory
4. **SVG Generation**: Server-side for consistency and caching
5. **Player Colors**: Gradient backgrounds (primary → secondary) with foreground text

### Performance Considerations

- **SVG caching**: Use `GameHash` for cache keys
- **Debouncing**: 150ms for slider interactions
- **Lazy loading**: Load player profiles on-demand in game page
- **Parallel operations**: Batch API calls when possible

### Security Notes

- **No credentials in code**: Use configuration files (not committed)
- **Database location**: CosmosDB emulator (local) or Azure CosmosDB (production)
- **API keys**: Document where needed, never commit actual keys

## Design, Planning, and Review Workflow

All design artifacts live under `.design/` in purpose-specific directories:

```text
.design/
├── *.md              # Verified as-built docs (source of truth)
├── plans/            # Implementation plans awaiting approval
├── reviews/          # Code and design reviews
└── old/              # Legacy/superseded docs for reference
```

### Design Documents

When documenting a system, feature, or architecture decision, write it to
`.design/` as a verified as-built doc:

- **Format**: Markdown that passes lint rules (see [Markdown Documentation Rules](#markdown-documentation-rules))
- **Naming**: `kebab-case.md` matching the system or feature
- **Update after changes**: When code behavior changes, update the relevant doc
- **Code is truth**: When a doc disagrees with code, the code wins -- fix the doc

### Design Documents and Implementation Plans

Non-trivial tasks require a **two-stage** approval workflow before any code is written.

#### Stage 1: Design Doc

Write a design document to `.design/<feature>.md` describing the architecture,
key decisions, data flow, and high-level approach. **STOP and wait for developer
approval before proceeding.**

#### Stage 2: Implementation Plan

After design approval, write a detailed implementation plan to
`.design/implementation-plans/`:

```text
.design/implementation-plans/
├── winner-overlay-plan.md
├── portrait-mode-plan.md
└── ...
```

**Plan file requirements:**

- **Format**: Markdown that passes lint rules
- **Naming**: `<feature-name>-plan.md` -- the developer provides the feature name
- **Scope**: One plan per task -- don't combine unrelated work

**Plan structure:**

1. **Goal** -- one sentence describing what the plan accomplishes
2. **Changes** -- per-file breakdown of what will be added/removed/modified
3. **Files Modified** -- summary table of all files touched
4. **Verification** -- how to confirm the changes work (build, test, manual steps)

**STOP and wait for developer approval before writing any code.**

#### Stage 3: Implementation

After plan approval, implement the plan precisely.

**Approval process:**

1. AI writes design doc to `.design/<feature>.md` -- waits for approval
2. AI writes implementation plan to `.design/implementation-plans/` -- waits for approval
3. Only after both approvals does implementation begin
4. Delete the plan file after the work is committed (plans are transient)

**When to skip this workflow:**

- Single-line fixes (typos, obvious bugs)
- Adding a comment or adjusting a constant
- Tasks where the developer gives exact instructions with no ambiguity

### Reviews

Code reviews and design reviews go in `.design/reviews/`:

```text
.design/reviews/
├── winner-overlay-review-claude.md
├── winner-overlay-review-copilot.md
├── doc-audit-review-gemini.md
└── ...
```

- **Naming**: `<feature-name>-review-<ai>.md` where `<ai>` identifies the
  reviewer (e.g., `claude`, `copilot`, `gemini`)
- **Format**: Markdown that passes lint rules
- **Content**: Findings, recommendations, file-specific feedback

## Session Workflow

### Starting a Session

1. Run `start-session.md` command to load context
2. Check `git status` and recent commits
3. Review `.ai/project-summary.md` for latest state
4. Read the latest session summary in `.ai/sessions/` for recent work context
5. **Discovery phase**: Consult `.design/` directory for current architecture
   - Start with `.design/README.md` for the document index
   - Reference relevant design documents as needed
   - Legacy docs are in `.design/old/` for historical reference
6. Identify current task and next priorities

### During a Session

1. Make minimal, focused changes
2. Test frequently (build, run, verify)
3. Document decisions in code comments or design docs
4. Keep `.ai/project-summary.md` context in mind

### Ending a Session

1. Run `handover.md` command
2. Create/update `.ai/sessions/SESSION_SUMMARY-{date}.md`
3. Update `.ai/project-summary.md` with session highlights
4. Commit all valuable work
5. Document clear next steps

## Common Patterns

### Adding a New Reusable Component

1. Create `WebUI/Components/{Category}/{ComponentName}.razor`
2. Create optional `{ComponentName}.razor.css` for scoped styles
3. Define `[Parameter]` properties for configuration
4. Use CSS variables for theming
5. Add XML comments for documentation
6. Create example usage in design doc

### Matching Desktop UI Element

1. Find corresponding XAML file in `DesktopApp/`
2. Note colors, sizes, fonts, spacing
3. Use Unicode glyphs (in black and white) instead of Segoe MDL2 Assets because the app has to run on iOS, Mac, Windows, and Android
4. Use CSS variables for colors
5. Test visual match with screenshots
6. Document any differences

### Updating SVG Generation

1. Modify `BoardSvgGenerator.cs` constants or methods
2. Restart GameService for changes to take effect
3. Create new game to bypass cache
4. Compare with Desktop app screenshot
5. Adjust constants iteratively

## Troubleshooting

### Build Failures

- **File locked by process**: Run `pwsh ./catan.ps1 stop` then rebuild
- **Missing dependencies**: Run `pwsh ./catan.ps1 install`
- **Cache issues**: Run `pwsh ./catan.ps1 clean`

### Runtime Issues

- **SVG not updating**: Restart GameService or create new game
- **Styles not applying**: Check CSS variable definitions in `app.css`
- **Hot reload not working**: Run `pwsh ./catan.ps1 update`

### Test Failures

- **Timing issues**: Tests may be brittle, check for race conditions
- **Environment dependencies**: Ensure test data files in correct location
- **UI automation failures**: Desktop tests may need updating after UI changes

## Resources

- **Current Architecture**: `.design/` directory for verified design documentation
  - `.design/README.md` - Complete document index (30 verified docs)
  - `.design/old/` - Legacy/superseded docs for historical reference
- **Desktop Reference**: `DesktopApp/` for XAML patterns and styling
- **Session History**: `.ai/sessions/` for past work context
- **Project Context**: `.ai/project-summary.md` for current state and priorities

### 7. Evidence Integrity (Anti-Hallucination Protocol)

**CRITICAL**: AI reviewers must strictly adhere to evidence-based reporting.

1. **Zero Fabrication**: Never quote code or file contents that you have not explicitly read using a tool in the current session.
2. **Verify "Missing" Items**: Before claiming an item needs to be added or removed (e.g., "Add export to index.ts" or "Remove export from index.ts"), you MUST verify the current state of that file.
    - *Failure Mode*: "The plan missed cleaning up index.ts" (when index.ts never had the export).
    - *Correction*: Read `index.ts` first. If the export isn't there, the plan is correct.
3. **No "Value-Add" Bias**: Do not invent minor issues just to avoid giving a "perfect" review. If a plan is flawless, explicit approval with verification evidence is the highest value output. "No findings" is better than "False findings".
