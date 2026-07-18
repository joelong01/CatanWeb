# Architecture Invariants (the Constitution)

**Last Updated:** 2026-07-17

These are the load-bearing laws of the Catan codebase. Unlike the many
feature-level design docs in `.design/` — which are per-feature, evolving, and
peer-level — this document is **authoritative and rarely changes**. Every design
doc, implementation plan, and code change must conform to it. When a design doc
and an invariant here disagree, **the invariant wins** and the design doc is the
thing that must change.

Keep this file short. If it grows past what you can hold in your head, it has
stopped being a constitution.

## How to use this document

- **Read it first**, before any design or implementation work.
- Each invariant is stated as a **law**, a **why**, and a **routing test** you
  can apply to a concrete piece of data or state to decide where it belongs.
- Amending the constitution is a deliberate act: propose the change, get explicit
  approval, and update this file — do not let a feature quietly erode an invariant.

## The invariants

### 1. GameModel is the single runtime source of truth

**Law:** All state required to render or play a game lives in `GameModel`. The
template is a **factory input**, consumed exactly once at game creation to build
the `GameModel`; nothing reads the template at play time.

**Why:** The client's only job is to render `GameModel` and collect actions. If
rendering needed both the `GameModel` and its template, the template would become
live runtime state — a second source of truth — and the client's contract would
break.

**Routing test:** *Could two players holding the same `GameModel`, with no access
to the template, render and play identically?* If the answer must be yes and it
is not, the missing data has to be baked into `GameModel` at creation.

### 2. GameState is service-only

**Law:** The service owns and advances `GameState`. The client only **reads** it;
it never authors, computes, or derives it.

**Why:** The game engine is authoritative. A client that infers state locally can
disagree with the server, and there is then no single answer to "whose turn / what
phase is it."

**Routing test:** *Does this value determine what the engine allows next?* If yes,
it is `GameState` (or engine-owned model state) and the client must not synthesize
it.

### 3. Client-only render and interaction options live in the client

**Law:** Anything that is purely presentation or local interaction — glyphs,
labels, keyboard shortcuts, interaction flows — lives on the client and is
**keyed by a shared enum**. It never enters `GameModel`.

**Why:** These options do not affect the authoritative game. Threading them
through the data model bloats every saved game and hash, and couples the engine to
presentation concerns it should not know about.

**Worked example — keyboard shortcuts.** Shortcuts are a single
`KeyboardShortcut` enum defined in `Catan3.Shared` (so all shortcuts are visible
in one place and flow to TypeScript via the type-gen pipeline — see invariant 4).
The key value is carried as the browser `KeyboardEvent.key` string on a
`[Description]` attribute, because that is exactly what the browser hands the
client and it covers named keys (`Escape`, `Enter`, arrows) that raw character
codes cannot:

```csharp
public enum KeyboardShortcut
{
    [Description("7")]       RollSeven,
    [Description("p")]       PurchaseShip,
    [Description("Escape")]  CancelInteraction,
}
```

The client matches against the event directly, normalizing case so a single
canonical lowercase value handles both cases:

```typescript
if (event.key.toLowerCase() === KeyboardShortcutDescriptions[KeyboardShortcut.PurchaseShip]) { /* ... */ }
```

The enum lives in `Catan3.Shared` because that is the home of the enum pipeline —
**not** because it is game state. It is consumed by the client and never placed in
`GameModel`.

**Routing test:** *Does the service enforce this, or is it purely how the client
looks and feels?* If it is look-and-feel, it belongs to the client, keyed by an
enum.

### 4. Enums are defined once in Catan3.Shared and generated to TypeScript

**Law:** Every enum shared between service and client is defined in
`Catan3.Shared` and flows to TypeScript via the type-gen pipeline
(`./catan.ps1 generate-types`). Enums are **never** hand-authored in `react-ui`.
Human-facing strings attached to enum values use `[Description]` attributes, which
the pipeline emits to the generated descriptions file.

**Why:** One definition means the service and client can never drift on the set of
values or their spelling. The enum is the **join key** between server authority
and client rendering — the mechanism that makes invariant 3 safe.

**Routing test:** *Is this a fixed vocabulary used on both sides?* If yes, it is a
`Catan3.Shared` enum plus a type-gen registration — not a TypeScript literal.

### 5. The template authors only what varies per-template

**Law:** A template carries only the fields that legitimately differ between
templates. At game creation, each authored field **routes** to its rightful home:
into `GameModel` if it is authoritative game state, or it is a no-op at runtime if
the client already knows it from a shared enum.

**Why:** The template is an authoring surface, not a runtime store. Keeping it
minimal prevents it from accreting render metadata and quietly becoming the second
source of truth that invariant 1 forbids.

**Routing test:** *At creation, where does this authored field land?* If the answer
is "nowhere — the client reads it from an enum," it should not be on the template
at all. If it is "into `GameModel`," author it and copy it in.

## Routing quick-reference

| Kind of data or state | Authority | Home |
|---|---|---|
| Authoritative game state / rules (service enforces, affects hash) | Service | `GameModel` |
| Phase / turn progression | Service | `GameState` (engine-owned) |
| Render / interaction options (glyph, label, shortcut, flow) | Client | Client, keyed by a shared enum |
| Shared vocabulary of values | Both | `Catan3.Shared` enum → TypeScript via pipeline |
| Per-template authored variation | Author | Template → routes to `GameModel` at creation |

## Operational law: build, test, and lint only through `./catan.ps1`

**Never invoke `dotnet build`, `dotnet test`, `npm test`, `npx`, `vitest`, or any
raw toolchain command directly. Always go through the unified script:**

- `./catan.ps1 build` — build all projects
- `./catan.ps1 test` — build, start the CosmosDB emulator (Docker), seed it, and run
  every .NET and TypeScript test project
- `./catan.ps1 lint` — format, lint, and spell-check

**Why this is a law, not a preference:** `./catan.ps1 test` provisions the
environment the tests require — it starts and health-checks the CosmosDB emulator,
sets connection strings, and builds first. Running `dotnet test` directly skips all
of that and reports **false failures** (e.g. Cosmos `ServiceUnavailable (503)` on
every `CatanDb` test) that look like real breakage and waste a debugging cycle. A
green result only counts if it came from `./catan.ps1`.

## Amending the constitution

1. Propose the change and the invariant it affects.
2. Get explicit approval — an invariant change is not a routine edit.
3. Update this file, and update any design doc or code that the change makes
   inconsistent.
