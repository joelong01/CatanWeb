# GitHub Copilot – Catan Project Instructions

**Last Updated:** 2024-11-24

## Primary Instructions

Read and follow all rules and guidelines in `.ai/ai-rules.md`.

That file contains comprehensive instructions for:
- Code quality standards
- Documentation requirements
- CSS and styling conventions
- Blazor component patterns
- Build and development workflow
- Testing requirements
- Git practices
- Architecture patterns
- Common development patterns

## Quick Reference

For immediate context, the project is a multi-platform Settlers of Catan game with:
- **Desktop App** (WinUI3)
- **WebUI** (Blazor WASM)
- **GameService** (ASP.NET Core with SignalR)

Key principles:
- Make minimal, surgical changes
- Use CSS variables for all styling (defined in `wwwroot/css/app.css`)
- Document all public APIs with XML comments
- Use Segoe MDL2 Assets for icons (HTML entities, not emoji)
- Use `pwsh` for PowerShell scripts, not `powershell`
- Match Desktop XAML patterns when building WebUI features

For comprehensive details, see `.ai/ai-rules.md`.
