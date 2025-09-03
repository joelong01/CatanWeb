# VS Code Extension Setup for Catan WinUI3 Project

This directory contains scripts and configurations to set up VS Code extensions for the Catan WinUI3 project.

## Files

- **`extensions.json`** - Standard VS Code workspace extension recommendations
- **`extension-config.json`** - Detailed extension configuration for the PowerShell installer
- **`install_extensions.ps1`** - PowerShell script to automatically install extensions
- **`settings.json`** - VS Code workspace settings for C# development
- **`tasks.json`** - Build tasks for the project

## Quick Setup

### Option 1: Automatic Installation (Recommended)

Run the PowerShell script to install all required extensions:

```powershell
# Install only required extensions
.\.vscode\install_extensions.ps1

# Install all extensions (required + optional)
.\.vscode\install_extensions.ps1 -All

# Force reinstall all extensions
.\.vscode\install_extensions.ps1 -All -Force
```

### Option 2: Manual Installation

1. Open VS Code
2. Go to Extensions view (`Ctrl+Shift+X`)
3. VS Code will automatically prompt you to install recommended extensions
4. Click "Install All" when prompted

### Option 3: Command Line

Install the essential extensions manually:

```bash
code --install-extension ms-dotnettools.csdevkit
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.vscode-dotnet-runtime
```

## Essential Extensions

### Required for C# Development

- **C# Dev Kit** - Official Microsoft C# extension with IntelliSense and debugging
- **C#** - Base C# language support (auto-installed with C# Dev Kit)
- **.NET Install Tool** - Manages .NET runtime installations

### Recommended for Enhanced Development

- **PowerShell** - PowerShell scripting support
- **GitLens** - Enhanced Git capabilities
- **NuGet Package Manager** - GUI for NuGet packages
- **.NET Core Test Explorer** - Test discovery and execution

## Troubleshooting

### VS Code Can't Find Dependencies

If you see errors like "The type or namespace name 'Shared' does not exist":

1. **Restart Language Server**:
   - `Ctrl+Shift+P` → `.NET: Restart Language Server`
   - Or `Ctrl+Shift+P` → `Developer: Reload Window`

2. **Check Solution File**:
   - Ensure VS Code opened the workspace at the root level
   - The `Catan.sln` file should be in the workspace root

3. **Clean and Rebuild**:

   ```bash
   dotnet clean
   dotnet restore  
   dotnet build
   ```

4. **Check Extension Installation**:
   - Verify C# Dev Kit is installed and enabled
   - Check the Output panel (View → Output → C#) for errors

### Extension Installation Issues

- Ensure VS Code is in your PATH: `code --version`
- Try installing extensions individually in VS Code UI
- Check your internet connection for marketplace access
- Try running VS Code as administrator (Windows)

## VS Code Configuration

The workspace is configured with:

- **Solution File**: `Catan.sln` (automatically detected)
- **Target Framework**: .NET 9.0
- **Platform**: x64 (for WinUI3 compatibility)
- **IntelliSense**: Enabled with full project analysis
- **Debugging**: Configured for WinUI3 applications

## After Installation

1. **Restart VS Code** to ensure all extensions are loaded
2. **Open** the `Catan.sln` solution file
3. **Verify** that C# IntelliSense is working in `.cs` files
4. **Check** that you can build the project: `Ctrl+Shift+P` → `Tasks: Run Task` → `build`

If you continue to have issues, please check the [Troubleshooting](#troubleshooting) section above.
