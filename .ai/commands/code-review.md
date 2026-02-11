# Code Review Guidelines for Catan Project

**Last Updated:** 2025-11-27

This document provides guidelines for conducting thorough, constructive code reviews in the Catan project. These guidelines apply to both human reviewers and AI-assisted code reviews.

## Purpose

Code reviews ensure:

- **Code quality and maintainability** - Code is well-structured, readable, and follows best practices
- **Adherence to project standards** - Consistent with `.ai/ai-rules.md` and project conventions
- **Early detection of bugs** - Identify logic errors, edge cases, and potential issues before they reach production
- **Knowledge sharing** - Document decisions and patterns for future reference
- **Security best practices** - No vulnerabilities or data exposure
- **Thorough documentation** - All findings documented in `.code-reviews/` directory for tracking and follow-up

## Review Checklist

### 1. Code Quality

**Standards Compliance:**

- [ ] Follows C# coding standards (modern features, proper naming)
- [ ] Uses CSS variables instead of hardcoded colors
- [ ] Icons use Segoe MDL2 Assets (HTML entities, not emoji)
- [ ] XML documentation on all public APIs
- [ ] Blazor components use `[Parameter]` with documentation
- [ ] Meaningful variable/method names (self-documenting)

**Style and Formatting:**

- [ ] Consistent with existing codebase style
- [ ] Proper indentation and spacing
- [ ] No commented-out code (unless with explanation)
- [ ] No debug code or console.log statements
- [ ] Follows naming conventions (PascalCase, camelCase, kebab-case)

**Comments and Documentation:**

- [ ] XML comments on public APIs (`/// <summary>`, `/// <param>`, `/// <returns>`)
- [ ] Complex logic has explanatory comments
- [ ] Comments explain "why" not "what"
- [ ] No obvious or redundant comments
- [ ] Workarounds reference issue numbers

### 2. Architecture and Design

**Component Design:**

- [ ] Components are properly scoped (not doing too much)
- [ ] Reusable components in appropriate `Components/` subdirectory
- [ ] CSS scoped to components (`.razor.css` files)
- [ ] Parameters properly defined with `[Parameter]`
- [ ] EventCallbacks used for parent-child communication

**State Management:**

- [ ] State properly managed (SignalR, component state, query params)
- [ ] No unnecessary state duplication
- [ ] Props vs state used appropriately
- [ ] Two-way binding used correctly (`@bind`)

**Pattern Compliance:**

- [ ] Follows established patterns in codebase
- [ ] Desktop UI patterns replicated correctly (references XAML)
- [ ] SVG generation follows existing patterns
- [ ] API endpoints follow RESTful conventions

### 3. Functionality and Logic

**Correctness:**

- [ ] Code does what it claims to do
- [ ] Edge cases handled appropriately
- [ ] Null/undefined checks where needed
- [ ] Error handling implemented properly
- [ ] No logic errors or off-by-one errors

**Performance:**

- [ ] No unnecessary loops or computations
- [ ] Efficient data structures used
- [ ] No memory leaks (proper cleanup in Dispose)
- [ ] Debouncing used for frequent events (150ms guideline)
- [ ] Lazy loading used where appropriate

**Minimal Changes:**

- [ ] Changes are surgical and minimal
- [ ] No unnecessary modifications to working code
- [ ] Focused on specific task/feature
- [ ] No scope creep or unrelated changes

### 4. Testing

**Test Coverage:**

- [ ] New code has appropriate tests
- [ ] Tests follow ReplayTest pattern
- [ ] Test files in `Tests/Data/` directory
- [ ] Tests are meaningful (not just for coverage)
- [ ] Edge cases tested

**Test Quality:**

- [ ] Tests are clear and understandable
- [ ] Test names describe what they test
- [ ] No flaky tests
- [ ] Tests run successfully (`pwsh ./catan.ps1 test`)

### 5. Security

**Security Practices:**

- [ ] No hardcoded credentials or secrets
- [ ] Input validation implemented
- [ ] No SQL injection vulnerabilities
- [ ] No XSS vulnerabilities
- [ ] Proper authentication/authorization checks
- [ ] Sensitive data not logged

**Data Handling:**

- [ ] Personal data handled appropriately
- [ ] Database files gitignored
- [ ] API keys in configuration (not code)
- [ ] No sensitive data in error messages

### 6. Dependencies and Integration

**Dependencies:**

- [ ] No unnecessary new dependencies
- [ ] Dependencies are up-to-date and secure
- [ ] Dependency changes documented
- [ ] Package lock files updated if needed

**Integration:**

- [ ] Integrates well with existing code
- [ ] APIs match existing patterns
- [ ] SignalR messages follow conventions
- [ ] GameService/WebUI communication correct

### 7. UI/UX (for WebUI changes)

**Visual Quality:**

- [ ] Matches Desktop app appearance
- [ ] Responsive design (works at different sizes)
- [ ] Colors use CSS variables
- [ ] Icons are correct size and style
- [ ] Consistent spacing and alignment

**User Experience:**

- [ ] Intuitive and easy to use
- [ ] Proper loading states
- [ ] Error messages are helpful
- [ ] Accessibility considerations
- [ ] Keyboard navigation works

**Hot Reload Considerations:**

- [ ] Changes support hot reload when possible
- [ ] GameService restart documented if needed
- [ ] Browser cache considerations noted

### 8. Documentation

**Code Documentation:**

- [ ] README.md updated if needed
- [ ] `.ai/project-summary.md` updated for significant changes
- [ ] Design docs created/updated
- [ ] Inline comments where necessary
- [ ] Session summary created (if end of session)

**Commit Quality:**

- [ ] Clear, descriptive commit messages
- [ ] Logical commit organization
- [ ] No merge commits (rebase preferred)
- [ ] Commits reference issues when applicable

## Review Process

### Phase 1: Context Gathering

**Before reviewing code, understand the full context:**

1. **Review Project Documentation**
   - Read `.ai/project-summary.md` for current project state and architecture
   - Check `.ai/ai-rules.md` for coding standards and conventions
   - Review recent `.ai/sessions/*.md` files to understand recent changes
   - Check `design_docs/` for architectural decisions

2. **Understand the Change Scope**
   - Read PR description and linked issues (if applicable)
   - Identify files changed and their purpose
   - Understand the business logic being implemented
   - Note any dependencies or integration points

3. **Identify Review Targets**
   - Determine which files need thorough review
   - Prioritize critical paths and complex logic
   - Note files that integrate with existing systems

### Phase 2: Thorough Code Review

**Review each target file systematically:**

1. **Architecture and Design**
   - Evaluate high-level structure and patterns
   - Check for proper separation of concerns
   - Verify follows established project patterns
   - Compare with Desktop app implementation (for WebUI)
   - Identify over-engineering or under-engineering

2. **Implementation Details**
   - Review logic correctness line-by-line
   - Check edge cases and error handling
   - Verify null/undefined safety
   - Examine performance implications
   - Look for code duplication or inconsistencies

3. **Standards Compliance**
   - Verify XML documentation on all public APIs
   - Check naming conventions (PascalCase, camelCase, etc.)
   - Ensure modern C# features used appropriately
   - Validate CSS uses variables (not hardcoded colors)
   - Confirm icons use Segoe MDL2 Assets (not emoji)

4. **Testing and Verification**
   - Check test coverage and quality
   - Verify tests follow ReplayTest pattern
   - Ensure tests are in correct directory structure
   - Build and run tests if possible
   - Test edge cases manually if needed

### Phase 3: Documentation

**Document all findings in `.code-reviews/` directory:**

1. **Create Review Files**
   - One file per reviewed source: `.code-reviews/<file-name>-cr-<ai>.md`
   - Use consistent format (see Findings Format below)
   - Include file path, review date, and reviewer

2. **Categorize Findings**
   - **Critical:** Must be fixed (security, correctness, breaking changes)
   - **Important:** Should be fixed (bugs, performance, maintainability)
   - **Suggestion:** Consider for improvement (style, minor optimizations)
   - **Question:** Clarification needed (intent, design decisions)
   - **Praise:** Call out excellent solutions or clever implementations

3. **Provide Actionable Feedback**
   - Be specific about what needs to change
   - Explain why the change is needed
   - Provide examples or alternatives when possible
   - Reference related Desktop code if applicable
   - Link to relevant documentation or standards

4. **Cross-Reference Issues**
   - Note redundant or duplicate code
   - Identify inconsistencies across files
   - Recommend which implementation to keep
   - Document technical debt discovered

### For Code Authors

1. **Before Requesting Review**
   - Self-review using this checklist
   - Run full build: `pwsh ./catan.ps1 build`
   - Run all tests: `pwsh ./catan.ps1 test`
   - Update `.ai/project-summary.md` if architecture changed
   - Create/update design docs if needed
   - Ensure code follows `.ai/ai-rules.md`

2. **During Review Process**
   - Read all review findings thoroughly
   - Respond to all comments and questions
   - Ask for clarification if feedback is unclear
   - Fix critical and important issues
   - Consider suggestions carefully
   - Update code review files when issues are resolved
   - Push fixes in logical, reviewable commits

3. **After Review Complete**
   - Verify all critical/important issues addressed
   - Update documentation if design changed during review
   - Squash/rebase commits if needed
   - Create session summary if end of work session
   - Merge when all checks pass and reviewer approves

## Common Issues to Watch For

### C# Specific

- Missing XML documentation on public APIs
- Not using modern C# features
- Improper async/await usage
- Not disposing resources properly
- Catching generic exceptions without re-throwing

### Blazor Specific

- Missing `[Parameter]` documentation
- Incorrect component lifecycle usage
- Memory leaks (event handlers not unsubscribed)
- Not using scoped CSS
- Hardcoded colors instead of CSS variables

### CSS/Styling

- Hardcoded colors instead of CSS variables
- Not using Segoe MDL2 Assets for icons
- Emoji instead of proper icon font
- Inline styles instead of CSS classes
- Missing hover/focus states

### Architecture

- Components doing too much (not single responsibility)
- State management done incorrectly
- Not following existing patterns
- Over-engineering simple solutions
- Tight coupling between components

### Testing

- Tests not using ReplayTest pattern
- Test files not in `Tests/Data/`
- Tests not covering edge cases
- Flaky or non-deterministic tests
- Tests testing implementation not behavior

## Best Practices

### Do

- ✅ Be constructive and helpful
- ✅ Explain your reasoning
- ✅ Provide examples and alternatives
- ✅ Acknowledge good solutions
- ✅ Ask questions to understand intent
- ✅ Focus on important issues
- ✅ Review promptly

### Don't

- ❌ Be overly critical or harsh
- ❌ Nitpick trivial style issues
- ❌ Rewrite the entire solution
- ❌ Request changes outside scope
- ❌ Ignore reviewer feedback
- ❌ Rush through the review
- ❌ Approve without understanding

## AI-Assisted Code Reviews

### When to Use AI Assistance

AI-assisted code reviews are valuable for:

- **Systematic analysis** - Reviewing every line of code thoroughly
- **Pattern detection** - Identifying anti-patterns and inconsistencies
- **Standards enforcement** - Checking adherence to `.ai/ai-rules.md`
- **Cross-referencing** - Comparing WebUI with Desktop implementations
- **Documentation** - Generating comprehensive review files
- **Knowledge capture** - Documenting decisions and rationale

### AI Review Limitations

AI cannot replace human judgment for:

- **Business logic validation** - Understanding domain requirements
- **User experience evaluation** - Assessing usability and design
- **Strategic decisions** - Architecture trade-offs and long-term maintainability
- **Team collaboration** - Knowledge sharing and mentoring
- **Context-specific decisions** - When to break rules for good reasons

### Instructions for AI Reviewers

**IMPORTANT: If you are an AI conducting a code review, follow these operational instructions:**

#### 1. Use Deep Reasoning

- **Activate your deepest thinking/reasoning mode** before starting the review
- Take time to analyze thoroughly - don't rush through code
- Consider edge cases, implications, and interactions
- Reason through complex logic step-by-step
- Question assumptions and validate correctness

#### 2. Read Files Systematically

- **Read the entire file** using the Read tool - don't make assumptions about code you haven't seen
- Review line-by-line, not just a quick scan
- Note line numbers for all findings (use `file.cs:123` format)
- Check all public methods have XML documentation
- Verify all constants are named and not magic numbers

#### 3. Cross-Reference Context

Before reviewing code, read these files using the Read tool:

- `.ai/project-summary.md` - Understand current architecture
- `.ai/ai-rules.md` - Know the coding standards
- Recent `.ai/sessions/*.md` - Understand recent changes
- Related `design_docs/` - Understand design decisions
- Desktop implementation (for WebUI) - Compare patterns

#### 4. Follow the Three-Phase Process

##### Phase 1: Context Gathering (15-20% of time)

- Read all context documents
- Understand the change scope and purpose
- Identify files to review and their relationships
- Note specific concerns or areas of focus

##### Phase 2: Thorough Review (60-70% of time)

- Read each target file completely
- Analyze architecture, logic, standards compliance
- Check for bugs, performance issues, security concerns
- Compare with Desktop implementation (for WebUI)
- Document findings as you go

##### Phase 3: Documentation (15-20% of time)

- Create one `.code-reviews/<file>-cr-<ai>.md` per reviewed file
- Use the standard template (see Review File Template section)
- Organize findings by severity
- Provide actionable recommendations with examples
- Create summary report for multi-file reviews

#### 5. Quality Checkpoints

Before finishing a review, verify:

- [ ] Read entire file(s), not just skimmed
- [ ] Checked ALL public APIs for XML documentation
- [ ] Verified ALL magic numbers have named constants
- [ ] Reviewed ALL error handling paths
- [ ] Cross-referenced with Desktop implementation (WebUI)
- [ ] Documented ALL findings in standard format
- [ ] Provided specific file:line references
- [ ] Included code examples for recommendations
- [ ] Explained WHY changes are needed
- [ ] Created actionable follow-up tasks

#### 6. Output Format

Generate review files in this exact format:

```markdown
# Code Review: <FileName>

**File:** `<full/path/to/file.cs>`
**Reviewed:** <YYYY-MM-DD>
**Reviewer:** <AI Model Name>

## Summary
[2-3 sentences about file purpose and what was reviewed]

## Critical Issues
[Issues that MUST be fixed - security, correctness, breaking bugs]

## Important Issues
[Issues that SHOULD be fixed - performance, maintainability, bugs]

## Suggestions
[Nice-to-have improvements - style, minor optimizations]

## Questions
[Clarifications needed about intent or design]

## Praise
[Good solutions worth calling out]

## Desktop App Comparison
[For WebUI: Compare with Desktop XAML, note divergences]

## Follow-Up Actions
- [ ] Specific actionable tasks
```

### AI Review Best Practices

1. **Provide Full Context (for humans requesting AI review)**
   - Share `.ai/project-summary.md` and `.ai/ai-rules.md`
   - Reference recent session summaries
   - Explain the change's purpose and scope
   - Identify specific concerns or areas of focus

2. **Request Thorough Analysis**
   - Ask for line-by-line review of critical code
   - Request comparison with Desktop implementation
   - Ask to verify all public APIs have XML docs
   - Request performance and security analysis
   - Emphasize use of deep reasoning mode

3. **Review AI Output (for humans)**
   - Verify findings are accurate and relevant
   - Filter out false positives or overly pedantic issues
   - Add context or clarification to findings
   - Consolidate duplicate or related issues

4. **Document in Standard Format**
   - Save AI findings to `.code-reviews/<file>-cr-<ai>.md`
   - Organize by severity (Critical → Praise)
   - Include file location references (file:line)
   - Add follow-up action items

## Resources

### Project Documentation

- **Coding Standards**: `.ai/ai-rules.md` - Comprehensive coding standards and conventions
- **Project State**: `.ai/project-summary.md` - Current architecture and status
- **Code Reviews**: `.code-reviews/` - Past code review findings (gitignored)
- **Design Docs**: `design_docs/` - Architecture decisions and rationale
- **Session History**: `.ai/sessions/` - Past work context and decisions

### Reference Implementations

- **Desktop App**: `DesktopApp/` - XAML patterns and ViewModels to match
- **Shared Logic**: `Catan3.Shared/` - Game logic and models
- **Game Service**: `Catan3.GameService/` - Server-side logic and API

### Testing Resources

- **Test Projects**: `Tests/` - Unit and integration tests
- **Test Data**: `Tests/Data/` - ReplayTest data files
- **Build Script**: `build.ps1` - Build and test automation
- **WebUI Script**: `webui.ps1` - Development workflow automation

## Review Documentation Format

### File Structure

All code review findings must be documented in the `.code-reviews/` directory (note the leading dot - this directory is gitignored):

```text
.code-reviews/
├── BoardSvgGenerator-cr-claude.md      # Claude review
├── BoardSvgGenerator-cr-cp.md          # GitHub Copilot review
├── BoardSvgGenerator-cr-cline.md       # Cline review
├── BoardSvgGenerator-cr-gpt.md         # ChatGPT/GPT review
├── portrait-mode-cr-claude.md          # Feature review by Claude
├── portrait-cr-recco-claude.md         # Recommendations file
└── ...
```

### File Naming Convention

Review files must follow this naming pattern:

```text
<subject>-cr-<ai-suffix>.md
```

**Components:**

- `<subject>`: The file name (without extension) or feature being reviewed
- `-cr-`: Code review marker (always present)
- `<ai-suffix>`: Identifier for the AI that performed the review

**AI Suffixes:**

| AI Tool | Suffix | Example |
|---------|--------|---------|
| Claude (Anthropic) | `-claude` | `Game.razor-cr-claude.md` |
| GitHub Copilot | `-cp` | `Game.razor-cr-cp.md` |
| Cline | `-cline` | `Game.razor-cr-cline.md` |
| ChatGPT/GPT | `-gpt` | `Game.razor-cr-gpt.md` |
| Gemini | `-gemini` | `Game.razor-cr-gemini.md` |
| Human reviewer | `-<initials>` | `Game.razor-cr-jl.md` |

**Recommendation Files:**

For prioritized fix recommendations, use `-recco-` before the AI suffix:

```text
<subject>-cr-recco-<ai-suffix>.md
```

Example: `portrait-cr-recco-claude.md`

### Review File Template

Each `.code-reviews/<file-name>-cr-<ai>.md` should follow this structure:

```markdown
# Code Review: <FileName>

**File:** `<full/path/to/file.cs>`
**Reviewed:** <YYYY-MM-DD>
**Reviewer:** <Human/AI name>

## Summary

[2-3 sentence overview of file purpose and review scope]

## Critical Issues

### 1. [Issue Title]
**Location:** `file.cs:123`
**Severity:** Critical

[Detailed description of the issue]

**Recommendation:**
[Specific fix or change needed]

**Example:**
```csharp
// Current (problematic)
public void DoSomething() { ... }

// Recommended
public async Task DoSomethingAsync() { ... }
```

## Important Issues

[Same format as Critical]

## Suggestions

[Same format as Critical, but lower priority]

## Questions

### 1. [Question about design/intent]

**Location:** `file.cs:456`

[What needs clarification]

## Praise

### 1. [Good solution]

**Location:** `file.cs:789`

[What was done well and why]

## Desktop App Comparison

[For WebUI files: Compare with Desktop XAML implementation]

- **Desktop:** `DesktopApp/Views/BoardView.xaml:45`
- **WebUI:** `WebUI/Services/BoardSvgGenerator.cs:123`
- **Divergence:** [Explain difference and justify if acceptable]

## Follow-Up Actions

- [ ] Fix critical issue #1
- [ ] Address important issue #2
- [ ] Consider suggestion #3
- [ ] Clarify question #4

```text
on 

### Key Requirements

1. **One File Per Review**
   - Each source file gets its own review document
   - Keep reviews focused and organized
   - Update review files as issues are resolved

2. **Severity Ordering**
   - Always order sections: Critical → Important → Suggestion → Question → Praise
   - Within each section, order by file location (top to bottom)
   - Use file:line references for all findings

3. **Actionable Feedback**
   - Every finding must have a clear recommendation
   - Provide code examples when possible
   - Explain why the change is needed
   - Reference standards or patterns

4. **Cross-References**
   - Link to related Desktop code for WebUI reviews
   - Reference `.ai/ai-rules.md` for standards violations
   - Link to `design_docs/` for architecture decisions
   - Note duplicate code across files

5. **Completeness**
   - Review ALL public APIs for XML documentation
   - Check ALL magic numbers for constants
   - Verify ALL error paths are handled
   - Test ALL edge cases are considered
   - Ensure ALL assumptions are documented

### Verification Checklist

Before completing a review, verify:

- [ ] Cross-checked constants with Desktop XAML (for WebUI)
- [ ] All public APIs have XML documentation
- [ ] Modern C# patterns used (async/await, records, etc.)
- [ ] Performance implications noted (even if small scale)
- [ ] Security concerns addressed
- [ ] Test coverage evaluated
- [ ] Error handling reviewed
- [ ] Code duplication identified
- [ ] Naming conventions verified
- [ ] CSS uses variables (not hardcoded colors)
- [ ] Icons use Segoe MDL2 Assets (not emoji)

## Summary Report

After reviewing all target files, create a summary response to the user:

### Summary Template

```markdown
## Code Review Summary

**Files Reviewed:** X files
**Review Date:** YYYY-MM-DD

### Overview
[High-level assessment of code quality and compliance]

### Key Findings
- **Critical Issues:** X found (list files)
- **Important Issues:** X found (list files)
- **Suggestions:** X offered
- **Questions:** X raised

### Top Risks
1. [Critical risk requiring immediate attention]
2. [Important risk that should be addressed]
3. [Medium risk or technical debt]

### Recommendations
1. [Primary recommendation with rationale]
2. [Secondary recommendation]
3. [Nice-to-have improvement]

### Next Steps
- [ ] Address critical issues in [files]
- [ ] Fix important issues in [files]
- [ ] Review and respond to questions
- [ ] Consider suggestions for improvement
- [ ] Update tests if needed
- [ ] Verify fixes with build and test run

### Detailed Reviews
- See `.code-reviews/File1-cr-claude.md`
- See `.code-reviews/File2-cr-claude.md`
- [etc.]
```

## Review Quality Standards

### What Makes a Good Code Review?

**Thorough and Systematic:**

- Reviews every line of code, not just a quick scan
- Checks all public APIs for documentation
- Verifies all edge cases are handled
- Cross-references with Desktop implementation
- Documents ALL findings (not just major issues)

**Constructive and Helpful:**

- Explains why changes are needed
- Provides examples and alternatives
- Balances criticism with praise
- Focuses on important issues
- Distinguishes must-fix from nice-to-have

**Well-Documented:**

- Uses standard format consistently
- Includes file:line references
- Provides clear recommendations
- Links to relevant standards and docs
- Creates actionable follow-up tasks

**Context-Aware:**

- Understands project history and decisions
- Considers trade-offs and constraints
- Evaluates when to break rules
- Respects existing patterns
- Identifies opportunities for improvement

### Review Anti-Patterns (Avoid These)

❌ **Surface-Level Review** - Only checking obvious issues
❌ **Pedantic Nitpicking** - Focusing on trivial style preferences
❌ **Rewriting from Scratch** - Suggesting complete rewrites
❌ **Scope Creep** - Requesting unrelated changes
❌ **Missing Documentation** - Not recording findings properly
❌ **Vague Feedback** - "This could be better" without specifics
❌ **Ignoring Context** - Not understanding why code exists
❌ **Approval Without Review** - Rubber-stamping without reading

## Questions or Feedback

If you have questions about these guidelines or suggestions for improvement:

1. Update this document via PR with rationale
2. Reference specific sections in code reviews
3. Document exceptions in review files
4. Add examples to improve clarity
5. Keep guidelines current with project evolution
