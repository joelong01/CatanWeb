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
- **Thorough documentation** - All findings posted as PR comments for tracking and iteration

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

## Review Process — PR-Centric Workflow

Code reviews happen on **GitHub Pull Requests**. Findings are posted as PR comments,
not separate files. The review cycle iterates until all findings are addressed.

### Prerequisites: Issue and PR Hygiene

Before a PR can be reviewed:

- **Every PR must reference the GitHub issues it addresses** in the description
  (e.g., `Closes #94`, `Fixes #95`). No orphan PRs.
- **Every non-trivial code change should have a GitHub issue** created first.
  The issue describes the problem; the PR describes the solution.
- **PR description must include**: summary of changes, issues closed, and test plan.

### Phase 1: Context Gathering

Before reviewing code, understand what the PR is trying to do:

1. **Read the PR description** — what issues does it close? What's the summary?
2. **Read the linked GitHub issues** — understand the problem being solved
3. **Get the diff** — use `gh pr diff <number>` or `git diff base..head` to see
   all changes. This is what you're reviewing, not the entire codebase.
4. **Identify high-risk files** — security-sensitive code, state management,
   database changes, API contracts

### Phase 2: Review the Diff

Review the PR diff systematically. For each changed file:

1. **Read the full file** (not just the diff) — understand the context around
   the changes. The diff shows what changed; the full file shows if it's correct.
2. **Check the change against the issue** — does the code actually fix the
   reported problem?
3. **Look for**:
   - Security issues (injection, auth bypass, credential exposure)
   - Correctness bugs (logic errors, edge cases, off-by-one)
   - Performance problems (N+1 queries, unnecessary allocations, blocking calls)
   - Missing error handling (what happens when this fails?)
   - Dead code or unnecessary changes (scope creep)
4. **Verify against the codebase** — if the review claims something is missing
   or wrong, READ the actual file first. Do not fabricate findings.

### Phase 3: Post Findings as PR Comments

Post each finding as a **separate comment** on the PR using `gh api`:

```bash
gh api repos/{owner}/{repo}/issues/{pr}/comments -f body="## Finding title

**File:** path/to/file.ts:123
**Severity:** Critical | Important | Suggestion

Description of the issue...

**Fix:**
\`\`\`typescript
// suggested code
\`\`\`
"
```

**Finding format:**

- **Title**: `## Code Review Finding N/M — SEVERITY: Short description`
- **Severity levels**:
  - **Critical** — must fix before merge (security, correctness, data loss)
  - **Important** — should fix (performance, maintainability, error handling)
  - **Suggestion** — consider for improvement (style, DRY, naming)
- **Each finding must include**: file path with line number, description of the
  problem, why it matters, and a concrete fix (code example preferred)
- **No praise findings** — don't waste a finding slot saying "good job". If the
  code is good, the absence of findings IS the praise. Focus on what needs fixing.
- **Final comment**: Summary table of all findings with severity and action

### Phase 4: Iterate Until Clean

After findings are posted:

1. **Author fixes the issues** and pushes new commits to the PR branch
2. **Reviewer re-reads the changed files** to verify each fix
3. **Reviewer responds to each finding** with either:
   - "Fixed" — verified in code
   - "Not fixed" — explain what's still wrong
   - "Won't fix" — accepted with rationale
4. **Repeat until all Critical and Important findings are resolved**
5. **Merge** — only after all Critical issues are fixed and Important issues
   are either fixed or explicitly accepted

### Phase 5: Post-Merge — Close Issues

After the PR is merged, close each referenced GitHub issue with a comment
linking to the merge commit:

```bash
gh issue close <number> -c "Fixed in commit <sha> (PR #<pr-number>)"
```

This creates a traceable chain: **Issue → PR → Commit → Code**. Anyone
looking at the issue can follow the links to see exactly what changed.

If the PR only partially addresses an issue, add a comment instead of closing:

```bash
gh issue comment <number> -c "Partially addressed in PR #<pr-number> (commit <sha>). Remaining: <what's left>"
```

### What NOT to Do

- **Do not create `.code-reviews/` files** — findings go on the PR as comments
- **Do not fabricate findings** — if you haven't read the file, don't claim
  something is wrong. "No findings" is better than false findings.
- **Do not review code you haven't read** — use the Read tool on every file
  you comment on
- **Do not nitpick style** in areas the PR didn't touch — review the diff,
  not the entire codebase

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

#### 4. Follow the PR Review Workflow

##### Step 1: Get the PR diff and linked issues

```bash
gh pr view <number> --json title,body,files
gh pr diff <number>
gh issue view <issue-number>
```

##### Step 2: Read each changed file in full

For every file in the diff, use the Read tool to read the **entire file**, not
just the changed lines. The diff shows what changed; the full file tells you if
it's correct in context.

##### Step 3: Post findings as PR comments

Use `gh api` to post each finding as a comment on the PR. Number them
sequentially (Finding 1/N, 2/N, etc.). Include a final summary comment with
a table of all findings.

##### Step 4: Verify fixes

When the author pushes fixes, re-read the changed files and respond to each
finding confirming it's resolved or explaining what's still wrong.

#### 5. Quality Checkpoints

Before posting findings, verify:

- [ ] Read every changed file in full (not just the diff)
- [ ] Every finding references a specific file:line
- [ ] Every finding has a concrete fix (code example preferred)
- [ ] Every finding explains WHY the change matters
- [ ] No fabricated findings — you read the code before commenting
- [ ] Security implications checked (injection, auth, credential exposure)
- [ ] Error handling paths reviewed
- [ ] Final summary comment with severity table posted

#### 6. Output Format

Post each finding as a PR comment in this format:

```markdown
## Code Review Finding N/M — SEVERITY: Short Title

**File:** `path/to/file.ts:123`
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

4. **Review the PR Comments**
   - Verify AI findings are accurate (check the actual code)
   - Filter out false positives or overly pedantic issues
   - Respond to each finding: fix, won't fix, or needs discussion

## Resources

### Project Documentation

- **Coding Standards**: `.ai/ai-rules.md` - Comprehensive coding standards and conventions
- **Project State**: `.ai/project-summary.md` - Current architecture and status
- **Code Reviews**: Posted as PR comments on GitHub (searchable via PR history)
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

## PR Comment Format Reference

### Individual Finding

```markdown
## Code Review Finding 1/N — CRITICAL: Short Title

**File:** `path/to/file.ts:123`

Description of the problem and why it matters.

**Fix:**

\`\`\`typescript
// suggested fix code
\`\`\`

**Severity:** Critical — must fix before merge.
```

### Summary Comment (posted last)

```markdown
## Code Review Summary

| # | Finding | Severity | Action |
|---|---------|----------|--------|
| 1 | Command injection in execSync | Critical | **FIX** |
| 2 | Missing error handling | Important | **FIX** |
| 3 | Variable naming | Suggestion | Consider |
| 3 | Variable naming | Suggestion | Consider |

All N fixes will be in the next commit.
```

### Response Comment (after fixes)

```markdown
### Re: Finding 1/N — Command Injection

**Action: FIX** ✅

Description of what was fixed and how. Reference the commit if helpful.
```

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
- Focuses on actionable feedback, not flattery
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
