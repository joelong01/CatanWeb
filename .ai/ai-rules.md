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

These standards apply to **all AI assistants** regardless of vendor or tool. Agent-specific bindings (like command formats or workflow files) reference back to `.ai/` content but live in their respective directories.

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

## General Principles

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
│   ├── commands/          # Reusable command scripts
│   └── sessions/          # Session summaries
├── design_docs/           # Architecture and design documentation
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
- `*.db`, `*.db-shm`, `*.db-wal` - SQLite database files

## Build and Development Workflow

### Build Commands

- **PowerShell scripts**: Always use `pwsh` (PowerShell 7+), not `powershell` (legacy)
- **WebUI build**: `pwsh ./webui.ps1 help` to see available commands
- **Full build**: `pwsh ./build.ps1` (includes tests)
- **Quick build**: `dotnet build` (should succeed with no errors)
- **Clean build**: `pwsh ./build.ps1 -NoTest -Clean`

### Development Workflow

1. Check build status: `dotnet build`
2. Make minimal, surgical changes
3. Verify build: `dotnet build`
4. Run relevant tests if applicable
5. Commit with clear message

### Hot Reload Considerations

- **Browser caching**: Hard refresh (Ctrl+Shift+R) after code changes
- **SVG caching**: Create new game to bypass cache or restart GameService
- **GameService restart required**: For SVG generation code changes
- **Blazor hot reload**: Some changes require full rebuild and restart

### Testing Commands

- **All tests**: `./build.ps1` (includes test run)
- **Specific tests**: `dotnet test Tests/GameService --filter "TestName"`
- **With verbose**: Add `--verbosity normal` to see detailed output

## Architecture and Design Patterns

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

### Branch Strategy

- **Main branch**: `main` - Production-ready code
- **Feature branches**: Named descriptively (e.g., `WebUI`, `board-measurement`)
- Check current branch: `git status`

### Commit Guidelines

- **Commit frequently**: Small, logical commits
- **Clear messages**: Describe what and why, not how
- **Group related changes**: Stage files that belong together
- **Test before commit**: Ensure build passes

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

## Project-Specific Context

### Technology Stack

- **.NET 9.0**: Core framework (pinned via `global.json`)
- **Blazor WebAssembly**: WebUI frontend
- **ASP.NET Core**: GameService backend with SignalR
- **SQLite**: Local database (mirrors future CosmosDB schema)
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
- **Database location**: SQLite files in user profile (not repository)
- **API keys**: Document where needed, never commit actual keys

## Session Workflow

### Starting a Session

1. Run `start-session.md` command to load context
2. Check `git status` and recent commits
3. Review `.ai/project-summary.md` for latest state
4. Identify current task and next priorities

### During a Session

1. Make minimal, focused changes
2. Test frequently (build, run, verify)
3. Document decisions in code comments or design docs
4. Keep `.ai/project-summary.md` context in mind

### Ending a Session

1. Run `handover.md` command
2. Create/update `SESSION_SUMMARY-{date}.md`
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
3. Extract Segoe MDL2 Assets icon codes
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

- **File locked by process**: Stop GameService/WebUI before building
- **Missing dependencies**: Run `dotnet restore`
- **Cache issues**: Use `./build.ps1 -Clean`

### Runtime Issues

- **SVG not updating**: Restart GameService or create new game
- **Styles not applying**: Check CSS variable definitions in `app.css`
- **Hot reload not working**: Full rebuild required for some changes

### Test Failures

- **Timing issues**: Tests may be brittle, check for race conditions
- **Environment dependencies**: Ensure test data files in correct location
- **UI automation failures**: Desktop tests may need updating after UI changes

## Resources

- **Desktop Reference**: `DesktopApp/` for XAML patterns and styling
- **Design Docs**: `design_docs/` for architectural decisions
- **Session History**: `.ai/sessions/` for past work context
- **Project Context**: `.ai/project-summary.md` for current state and priorities
