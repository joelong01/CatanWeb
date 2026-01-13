# Pre-Checkin Command - Catan Project

Prepare the repository for a clean, high-quality check-in by ensuring the
codebase **builds cleanly**, **passes tests**, and follows **project standards**.

This command focuses on *validation and cleanup before committing*.
Actual commit creation and documentation updates are handled by
`checkin.md`.

**Note:** This is a Catan project-specific checklist. Generic language/framework checks have been removed.

---

## Command Purpose

Bring the repo into a **“green” state** where:

- All relevant builds succeed
- All automated tests pass (or failures are explicitly documented)
- All linters/formatters are clean
- Obvious local environment issues are resolved or clearly noted

The result should be a codebase that is safe to commit and push.

---

## Actions to Perform

### 1. Establish Context

1. Determine the project type(s) (e.g., Node/TypeScript, .NET, Rust, Python,
   etc.) by inspecting:
   - Top-level files (`package.json`, `pyproject.toml`, `Cargo.toml`,
     `*.sln`, `*.csproj`, `go.mod`, etc.)
   - `README.md` or other setup docs
2. Identify the **standard dev flows**:
   - How to build
   - How to run tests
   - How to run lint/format
3. If the project uses multiple subprojects or services, list them and
   their build/test commands.

Record the discovered commands mentally; you’ll need them in later steps.

---

### 2. Synchronize and Clean the Working Tree

1. Run `git status` to understand the current state.
2. If appropriate for the workflow:
   - Optionally `git fetch` to update local refs.
   - Do **not** auto-merge or rebase without explicit user direction, but
     note if the branch is behind the remote.
3. Ensure no temporary or generated files are accidentally tracked in `git`
   (e.g., build artifacts, large logs). If found:
   - Suggest adding to `.gitignore`, but **do not** modify ignore rules
     without explicit instruction.
   - Remove or move junk files from the working tree if safe.

The goal is to avoid build/test noise from obvious clutter.

---

### 3. Run a Clean Build

For each relevant project/component:

1. If the project supports a “clean” step, run it first. Examples:
   - Node/TS: `npm run clean` or `rm -rf dist build`
   - .NET: `dotnet clean`
   - Rust: `cargo clean`
   - Python: remove `__pycache__` / `.pytest_cache` / `build` dirs where needed
2. Run the **standard build** command(s). Examples:
   - Node/TS: `npm run build` or `pnpm build`
   - .NET: `dotnet build`
   - Rust: `cargo build --release` or `cargo build`
   - Go: `go build ./...`
   - Others per project docs

3. If the build fails:
   - Capture the **full error output**.
   - Identify the root cause (misconfig, missing dep, code error, etc.).
   - Fix issues when feasible (code errors, missing imports, obvious typos).
   - If the failure is environment-specific (missing local tool, secret,
     or service), document clearly in the final report:
     - What is missing
     - How to install/configure it
     - Any manual steps required

Repeat build until it succeeds or you have a clear explanation of why it
cannot.

---

### 4. Run All Relevant Test Suites

**Test Structure:**

- `Tests/GameService/` - Integration tests with ReplayTest pattern
- `Tests/Shared/` - Shared library tests (45 serialization tests)
- `Tests/Desktop/` - Desktop UI automation tests
- `Tests/Data/` - Test scenario files (`.catan_test`)

**Running Tests:**

1. **Full test suite** (recommended):

   ```powershell
   pwsh ./build.ps1
   ```

   - Builds and runs all tests
   - Takes 2-3 minutes
   - Most comprehensive validation

2. **Specific test projects**:

   ```powershell
   dotnet test Tests/GameService
   dotnet test Tests/Shared
   dotnet test Tests/Desktop
   ```

3. **Specific tests** (for targeted validation):

   ```powershell
   dotnet test Tests/GameService --filter "ReplaySharedExpansionTestFile"
   dotnet test Tests/GameService --filter "TestName"
   ```

4. **Verbose output** (for debugging):

   ```powershell
   dotnet test --verbosity normal
   ```

5. **Recording replay tests** (requires GameService running):

   ```powershell
   pwsh ./catan.ps1 replay
   ```

   - Replays all recorded game sessions
   - Verifies GameHash consistency after each action
   - Tests both GameHub and REST API recording paths

**If tests fail:**

- **Related to your changes**: Fix the issues - your code broke something
- **Pre-existing failures**: Document in final report:
  - Test name(s)
  - Error message
  - Why it's unrelated to your changes
- **Timing/race conditions**: Tests may be brittle - document and note
- **Missing test data**: Ensure files in `Tests/Data/` directory exist
- **UI automation issues**: Desktop tests may need updating after UI changes

**Known test issues:**

- Desktop UI tests can be timing-sensitive
- Some tests may fail if services are running during test execution

Re-run tests after fixes until all pass or remaining failures are documented.

---

### 5. Run Linters and Formatters

For each language or toolchain in the repo:

1. Identify the configured **lint and format** commands. Examples:
   - Node/TS:
     - Lint: `npm run lint`, `eslint .`
     - Format: `npm run format`, `prettier --check .` / `--write`
   - .NET:
     - Analyzers via `dotnet build` / `dotnet format`
   - Python:
     - Lint: `ruff check .`, `flake8`, `pylint`
     - Format: `black .`, `ruff format .`
   - Rust:
     - `cargo fmt --all`
     - `cargo clippy --all-targets --all-features`
   - Go:
     - `gofmt -w ./...`
     - `golangci-lint run`

2. Run **formatters** in “fix” mode where appropriate (e.g. `--write`).
3. Run **linters** in “check” mode and:
   - Fix all reported issues that are code smells, style, or correctness
     problems.
   - If there are warnings that are:
     - Clearly intentional, or
     - Not realistically fixable in this session,
     document them and consider adding targeted ignores with justification
     (only if allowed by the project’s conventions).

4. Repeat lint/format until:
   - Formatters report no changes needed.
   - Linters are clean (or remaining warnings/errors are explicitly known and
     documented).

---

### 6. Re-Validate After Fixes

Because formatting and lint fixes can change code:

1. Re-run the **build** for affected projects.
2. Re-run at least the **main test suite**.
3. Confirm that:
   - Build still passes.
   - Tests still pass (or only known, documented failures remain).

This ensures that cleanup steps didn’t introduce regressions.

---

### 7. Final Working Tree Sanity Check

1. Run `git status`:
   - Note which files changed as a result of formatting, lint fixes, or
     bug fixes.
2. Run a quick `git diff` to ensure:
   - Only intentional changes are present.
   - No generated junk, logs, or editor backups are included.
3. If needed, tidy up:
   - Remove transient files.
   - Move experimental notes out of tracked code files (into a TODO or
     docs location if appropriate).

The repository should now be in a **checkin-ready** state from a quality
perspective.

---

## Output Format

After performing all pre-checkin steps, provide a concise report:

```text
Pre-Checkin Validation Complete ✅

🧱 Build:
- Command(s) run:
  - [build command 1]
  - [build command 2]
- Result: [passed / failed]
- Notes: [short summary of any issues and fixes]

🧪 Tests:
- Command(s) run:
  - [test command 1]
  - [test command 2]
- Result: [all passed / some failing]
- Failing tests (if any):
  - [test name] – [short reason or error summary]
- Notes: [what was fixed, what remains]

🧹 Code Quality:
- Documentation: [all public APIs documented / gaps remain]
- CSS Standards: [using variables / hardcoded colors found]
- Icon Standards: [Segoe MDL2 Assets / emoji found]
- Code Standards: [follows conventions / issues found]
- Architecture: [compliant / deviations noted]
- Notes: [any important decisions or remaining issues]

📂 Working Tree:
- `git status`: [clean / modified files remain]
- Files changed by cleanup:
  - [file1]
  - [file2]
- Known remaining problems (if any):
  - [brief list]

Overall Pre-Checkin State: [Ready for check-in / Not ready – blocking issues described above]
