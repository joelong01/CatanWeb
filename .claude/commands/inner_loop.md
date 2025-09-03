# Inner Loop Command

Execute this sequence of work from the project root:

1. Run `./build.ps1 -NoTest`
2. Analyze and fix any build errors or warnings
3. Ensure code is lint clean, including markdown linting
4. If anything is fixed, run `./build.ps1` again
5. Continue until error, warning, and linter clean