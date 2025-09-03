# Debugging UI Tests in Visual Studio

## 🎯 **Simple Approach: Named Test Methods**

The easiest way to debug specific test scenarios is to use the dedicated test methods:

### **Available Tests:**

- **`Expansion_End_To_End_Test`** - Uses `Expansion-Test.catan_test`
- **`Regular_End_To_End_Test`** - Uses `Regular Game Test.catan_test`

### **How to Debug:**

1. **Open Test Explorer** (`Test → Test Explorer`)
2. **Find the test you want**: `Expansion_End_To_End_Test` or `Regular_End_To_End_Test`
3. **Right-click → Debug Selected Tests**

That's it! No configuration files, no environment variables - just right-click and debug.

## 🔄 **Two-Process Debugging (Test + Desktop App)**

### **Step 1: Start Test Debugging**

1. **Set breakpoints** in your test code (`FullCyclePackagedUiTests.cs`)
2. **Right-click test in Test Explorer → Debug Selected Tests**
3. **Wait for**: `"⏸️ WAITING FOR DEBUGGER ATTACHMENT"` message (10 seconds)

### **Step 2: Attach to Desktop App**

1. **Debug → Attach to Process** (or `Ctrl+Alt+P`)
2. **Find**: `Catan Desktop.exe`
3. **Click Attach**
4. **Set breakpoints** in desktop app code (e.g., `GameController.cs`)

### **Step 3: Debug Both**

- Test continues after 10 seconds
- Hit breakpoints in both processes:
  - **Test process**: UI automation code
  - **Desktop app**: Game logic, MVVM, etc.

## 📁 **Test File Locations**

Test files are in: `Tests.DesktopApp.UI\ScriptedTestData\`

- `Expansion-Test.catan_test` - 5-player expansion game
- `Regular Game Test.catan_test` - Regular game scenario

## 🎯 **Which Test Should I Use?**

| Use Case | Test Method | Test File |
|----------|-------------|-----------|
| Debug new regular game features | `Regular_End_To_End_Test` | `Regular Game Test.catan_test` |
| Debug expansion features | `Expansion_End_To_End_Test` | `Expansion-Test.catan_test` |
| Backwards compatibility | `Full_Stateful_Flow_PackagedApp_Expansion_FivePlayers` | Auto-detects (deprecated) |

## 🛠️ **Pro Tips**

### **Setting Breakpoints:**

- **Test code**: Set breakpoints in `FullCyclePackagedUiTests.cs` for UI automation logic
- **App code**: Set breakpoints in `GameController.cs`, `GameRecorder.cs`, etc. for game logic

### **Common Debugging Scenarios:**

- **Purchase button not found**: Check `UIAutomationHelper.FindPurchaseButtonDynamically()`
- **Game state assertion fails**: Check `UIAutomationHelper.VerifyGameState()`
- **Action execution fails**: Check `ActionExecutor.ExecuteAction()`
- **App launch issues**: Check `LaunchAppWithTestFile()`

### **Visual Studio Features:**

- **Multiple debug sessions**: Test Explorer + Attach to Process
- **Conditional breakpoints**: Right-click breakpoint → Conditions
- **Call stack navigation**: See call flow between test and app
- **Immediate window**: Execute code during debugging (`Ctrl+Alt+I`)

### **Test Output:**

- **Debug Output**: Shows detailed automation logs
- **Test Output**: Shows test progress and results
- **Output Window**: Shows build and test execution details

## ⚠️ **Troubleshooting**

### **"App not found" error:**

- Build the solution first (`Ctrl+Shift+B`)
- Ensure MSIX package is deployed

### **"Purchase button not found":**

- Buttons may be face-down in FlipperCtrl
- Dynamic search will find them automatically
- Check debug output for search details

### **Test hangs:**

- Look for "WAITING FOR DEBUGGER ATTACHMENT" message
- Attach debugger within 10 seconds
- Check if app launched successfully

This approach eliminates the need for configuration files while providing full debugging capabilities for both test automation and desktop app code!
