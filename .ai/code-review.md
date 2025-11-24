# Code Review Guidelines for Catan Project

**Last Updated:** 2024-11-24

This document provides guidelines for conducting thorough, constructive code reviews in the Catan project.

## Purpose

Code reviews ensure:

- Code quality and maintainability
- Adherence to project standards and conventions
- Early detection of bugs and issues
- Knowledge sharing across the team
- Security best practices

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
- [ ] Tests run successfully (`dotnet test`)

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

### For Reviewers

1. **Understand the Context**
   - Read the PR description and linked issues
   - Review `.ai/project-summary.md` for current project state
   - Check `.ai/ai-rules.md` for standards

2. **Review the Code**
   - Start with high-level architecture
   - Then review implementation details
   - Check tests and documentation
   - Run the code locally if possible

3. **Provide Feedback**
   - Be constructive and specific
   - Suggest improvements, don't just criticize
   - Provide examples when possible
   - Explain reasoning behind feedback
   - Distinguish blocking vs. nice-to-have changes

4. **Use This Format**
   - **Critical:** Must be fixed before merge
   - **Important:** Should be fixed before merge
   - **Suggestion:** Consider for improvement
   - **Question:** Clarification needed
   - **Praise:** Call out good solutions

### For Authors

1. **Before Submitting**
   - Self-review using this checklist
   - Run tests and build
   - Update documentation
   - Write clear PR description

2. **During Review**
   - Respond to all comments
   - Ask for clarification if needed
   - Push fixes in logical commits
   - Re-request review when ready

3. **After Approval**
   - Squash/rebase commits if needed
   - Merge when all checks pass
   - Thank reviewers

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

## Automated Review Tools

### GitHub Actions

The project uses GitHub Actions with automated code review:

- Runs on all pull requests
- Reviews code quality and best practices
- Identifies potential bugs and issues
- Checks security concerns
- Comments on PRs with findings

### Manual Review Still Required

Automated reviews complement but don't replace human review:

- Context and business logic understanding
- Design and architecture decisions
- User experience considerations
- Team knowledge sharing

## Resources

- **Project Rules**: `.ai/ai-rules.md` - Comprehensive coding standards
- **Project Context**: `CLAUDE.md` - Current project state
- **Design Docs**: `design_docs/` - Architecture decisions
- **Desktop Reference**: `DesktopApp/` - XAML patterns to match
- **Session History**: `.claude/sessions/` - Past work context

## Questions or Feedback

If you have questions about these guidelines or suggestions for improvement:

1. Discuss in PR comments
2. Update this document with team agreement
3. Reference specific guidelines in reviews
