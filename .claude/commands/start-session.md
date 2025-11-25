# You are executing the session-start command

1. Open and read the file `.ai/commands/start-session.md`.
2. Treat that file as the authoritative specification for how to begin
   a new development session.
3. Follow every instruction in that file exactly.

If the user supplies additional arguments or context after
`/start-session`, treat that as extra incoming developer context and
incorporate it into the session setup output.

Before doing anything else, read the contents of
`.ai/commands/start-session.md`, summarize it in one short paragraph
(confirming that you understand the spec), and then execute it using the
available tools (shell, git, files, etc.).
