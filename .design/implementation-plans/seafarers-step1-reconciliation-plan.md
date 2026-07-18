# Implementation Plan — Step 1 Reconciliation: general seams, not Seafarers special-cases

**Design:** [`.design/seafarers.md`](../seafarers.md),
[`.ai/architecture-invariants.md`](../../.ai/architecture-invariants.md) (the constitution).
**Goal:** bring the in-flight Step-1 changeset into conformance with the
constitution by making its two new mechanisms **general seams that Regular and
Expansion also ride**, not Seafarers-only additions:

1. **Keyboard shortcuts** move from hardcoded letters in `useGameKeyboard.ts` to a
   single Shared `KeyboardShortcut` enum (invariant 3 + 4). One place lists every
   shortcut; the client keys off it; nothing is in `GameModel` or the template.
2. **The feature set becomes template-driven and correctly typed**
   (`List<GameFeature>`), authored via the editor from the generated enum. Regular
   and Expansion carry `Features: []` through the *same* field — a plain board is
   just the empty-feature case.
3. **Render/interaction metadata is removed from the template** (invariant 3): it
   belongs to the client keyed by enum, not on `TemplateEntitlement`.

**STOP for approval before any code.**

## 1. Model & template changes review (the per-step gate)

Per the design's per-step gate — every underlying enum/field change this step
makes, each with its GameHash treatment:

| Area | Change | GameHash treatment |
|---|---|---|
| **`GameModel`** | **NONE** | No gameplay-state field added this step. |
| **`GameHash`** | **NONE — step is hash-neutral** | Nothing added to `GameModel`, so no field to classify. Regular/Expansion and every existing `.catan_test` replay hash are unchanged. |
| **New enum `KeyboardShortcut`** (Shared) | `[Description]` = the browser `event.key` string | **Client-only, never in `GameModel`** ⇒ not hashed. |
| **`GameFeature`** (Shared) | add `[Description]` friendly labels | No hash impact (descriptions are display text). |
| **`GameTemplateData.Features`** | `List<string>` → **`List<GameFeature>`** | Template field, **not** in `GameModel` ⇒ not hashed this step. The GameModel/scenario routing + its hash classification is **step 6**. |
| **`TemplateEntitlement`** | **remove** `Title/Description/Icon/PurchaseType/KeyboardShortcut`; **remove** `TemplateResourceCost` type | Template-only removals; no hash impact. `Cost` stays deferred. |

**Result: this step adds no `GameModel` field and nothing to the hash.** It is a
pure conformance/generalization pass over enums, the template schema, the editor,
and the keyboard hook. Low risk.

## 2. Framework vs Seafarers split (Prime rule)

- **Reusable framework:** the `KeyboardShortcut` enum + hook refactor, the typed
  `Features` set + enum-derived editor UI, and the `TemplateEntitlement` cleanup —
  all benefit *every* template. Regular and Expansion ride the identical seams
  (empty `Features`, the same shortcut enum).
- **Seafarers config:** `seafarers.json` gains `Features` values (e.g. `["Ships"]`)
  as data. No Seafarers name appears in any control flow.

## 3. Keyboard shortcuts → Shared `KeyboardShortcut` enum

### 3a. `Catan3.Shared/Models/GameEnums.cs` — new enum

Add a single enum listing the **fixed named** shortcuts; the `[Description]` holds
the exact browser `KeyboardEvent.key` value (canonical lowercase):

```csharp
// Single source of truth for fixed keyboard shortcuts. [Description] is the
// browser event.key value; the client matches against it (see architecture
// invariant 3). Append-only. Positional keys (roll digits, road/settlement/city
// index) are algorithmic, not fixed shortcuts, and are NOT modelled here.
public enum KeyboardShortcut
{
    [Description("s")] PurchaseSettlement,
    [Description("c")] PurchaseCity,
    [Description("r")] PurchaseRoad,
    [Description("k")] PlaySoldier,
    [Description("d")] PurchaseDevCard,
}
```

`PurchaseShip = [Description("p")]` is **added in step 7** with the ship-purchase
handler — the enum is the place it will land, but wiring a dead 'p' now buys
nothing. (Note in the enum comment so it isn't re-litigated.)

### 3b. `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs` — register it

`AddEnum<KeyboardShortcut>();` so the pipeline emits `keyboard-shortcut.ts` and
`KeyboardShortcutDescriptions` into `enum-descriptions.ts`. Run
`pwsh ./catan.ps1 generate-types`.

### 3c. `react-ui/lib/hooks/useGameKeyboard.ts` — read the enum

Replace the hardcoded `switch (lower) { case 's' … case 'd' }`
([:278](../../react-ui/lib/hooks/useGameKeyboard.ts#L278)) with matches against
`KeyboardShortcutDescriptions[KeyboardShortcut.*]` (normalizing with
`key.toLowerCase()`). The purchase→`Entitlement` mapping stays in the client (a
small `KeyboardShortcut → Entitlement` map local to the hook). **Unchanged:** the
Enter/Backspace/Escape capture-phase behavior and all positional digit/letter
placement logic — those are not fixed shortcuts.

### 3d. `react-ui/lib/hooks/__tests__/useGameKeyboard.test.ts` — update

The tests currently assert on literal `'s'/'c'/'r'/'k'/'d'`. Point them at the
enum descriptions so the test and the code share the one source of truth (a typo
in the enum then fails the test, not silently ships).

## 4. Template-driven feature set (authoring side)

### 4a. `Catan3.Shared/Models/GameTemplateData.cs` — type the field

`public List<GameFeature> Features { get; set; } = [];` (was `List<string>`). The
strong type is the invariant-4 win: an invalid feature name fails to deserialize
instead of lurking as a bad string.

### 4b. `Catan3.Shared/Models/GameEnums.cs` — label `GameFeature`

Add `[Description]` to each `GameFeature` value (e.g. `[Description("Ship
Movement")] ShipMovement`) so the friendly labels the editor needs come from the
enum, not a hand-authored TS list.

### 4c. `react-ui/app/templates/[id]/page.tsx` — derive options from the enum

Delete the hardcoded `GAME_FEATURE_OPTIONS` literal; build it from
`Object.values(GameFeature)` + `GameFeatureDescriptions` — mirroring the sibling
`ENTITLEMENT_OPTIONS = Object.values(Entitlement)` that already does the right
thing. The Features multi-select UI is otherwise unchanged.

### 4d. Regular/Expansion carry `Features: []` through the same field

- `Catan3.GameService/Data/DatabaseSeeder.cs` — `BuildTemplateFromMetadata` sets
  `Features = []` (Regular/Expansion are plain boards).
- `seafarers.json` (`SystemTemplates/`) — add `"features": ["Ships"]` as authored
  data (value list finalized when step 6/7 defines what New Shores advertises; a
  starter set is fine now since nothing reads it yet).

*(No `IGameMetadata`/`BoardInfoJsonAdapter`/`GameModel` change: routing `Features`
into `GameModel` is **step 6**. Constitution invariant 1 is satisfied because no
runtime code reads `Features` from the template yet — this step only establishes
the typed, enum-driven authoring surface.)*

## 5. Remove render/interaction metadata from the template

### 5a. `Catan3.Shared/Models/GameTemplateData.cs`

Remove from `TemplateEntitlement`: `Title`, `Description`, `Icon`, `PurchaseType`,
`KeyboardShortcut`. Remove the `TemplateResourceCost` class. `TemplateEntitlement`
returns to `{ Entitlement }`. These are client render/interaction concerns
(invariant 3); `KeyboardShortcut` specifically now lives in the enum (§3).

### 5b. `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs`

Remove `AddInterface<TemplateResourceCost>()`. Regenerate types (drops
`template-resource-cost.ts`, trims `template-entitlement.ts`).

### 5c. `Catan3.GameService/Abstractions/CosmosCatanDb.cs`

The `Features = data.Features` copy stays (template round-trip); it compiles
unchanged against `List<GameFeature>`.

## 6. Files-modified table

| File | Change | Kind |
|---|---|---|
| `Catan3.Shared/Models/GameEnums.cs` | add `KeyboardShortcut` enum; add `[Description]` to `GameFeature` | Framework |
| `Catan3.Shared/Models/GameTemplateData.cs` | `Features` → `List<GameFeature>`; strip `TemplateEntitlement` render fields; delete `TemplateResourceCost` | Framework |
| `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs` | `AddEnum<KeyboardShortcut>()`; drop `AddInterface<TemplateResourceCost>()` | Framework |
| `react-ui/types/generated/models/**` | regenerated (new `keyboard-shortcut.ts`, updated `enum-descriptions.ts`, trimmed `template-entitlement.ts`, removed `template-resource-cost.ts`) | Generated |
| `react-ui/lib/hooks/useGameKeyboard.ts` | read `KeyboardShortcutDescriptions` instead of hardcoded letters | Framework |
| `react-ui/lib/hooks/__tests__/useGameKeyboard.test.ts` | assert via enum descriptions | Test |
| `react-ui/app/templates/[id]/page.tsx` | derive feature options from `GameFeature` + descriptions; delete hardcoded list | Framework |
| `Catan3.GameService/Data/DatabaseSeeder.cs` | `BuildTemplateFromMetadata` sets `Features = []` | Framework |
| `Catan3.GameService/.../SystemTemplates/seafarers.json` | add `"features"` data | Seafarers data |

## 7. Verification (local)

1. `pwsh ./catan.ps1 generate-types` — regenerates cleanly; `keyboard-shortcut.ts`
   and `KeyboardShortcutDescriptions` appear; `template-resource-cost.ts` gone.
2. `./catan.ps1 build` — Shared + GameService + react-ui compile (the
   `List<GameFeature>` change and the trimmed `TemplateEntitlement` ripple cleanly).
3. `./catan.ps1 test` — **keyboard hook tests pass** (the #181 subsystem), and
   **all `.catan_test` replay tests pass with identical hashes** (proves the step
   is hash-neutral).
4. **Manual keyboard smoke:** in a running game, `s/c/r/k/d` still trigger the same
   purchases they did before the refactor.
5. **Editor smoke:** the Features multi-select still lists all features with
   friendly labels (now sourced from the enum); toggling persists into
   `template.features`.
6. `./catan.ps1 lint` — clean.

## 8. Out of scope (explicitly deferred, separate tracked steps)

- **Routing `Features` into `GameModel`** (scenario profile) + its GameHash
  classification — **step 6** (module framework).
- **`PurchaseShip` shortcut + handler** — **step 7** (ship purchase).
- **Migrating Regular/Expansion authoring from C# to JSON** (retire
  `RegularBoardInfo`/`ExpansionBoardInfo` + `BuildTemplateFromMetadata` + the
  create-path fallback) — its own step; requires a byte-identical
  `GameTemplateData` diff + full replay pass as its gate.
- **Generating a `GameTemplateData` JSON Schema** from the type pipeline (the "XSD"
  gate) — prerequisite for the C#→JSON migration above.
- **Per-entitlement `Cost`** (author-defined costs) — engine still hardcodes costs.

## 9. Risks / watch-items

- **Touching the #181 keyboard subsystem.** It is deliberately the single owner of
  shortcuts and has test coverage. The refactor is a like-for-like substitution
  (hardcoded letter → enum description of the same letter); tests must stay green.
- **`Features` type migration.** Any already-seeded template doc storing
  `features` as raw strings round-trips fine (same JSON), but confirm the seeded
  `seafarers.json` uses valid `GameFeature` names so it deserializes.
- **Append-only enums.** `KeyboardShortcut` and `GameFeature` are serialized in
  templates; never reorder/remove values.
- **Generated-file churn.** Regen is mechanical but review the diff so no unrelated
  generated file moves.
