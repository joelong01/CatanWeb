# Seafarers Reconciliation Review (Branch: seafarers)

Date: 2026-07-17
Scope reviewed: full diff of `origin/main...seafarers`

Commands reviewed (as requested):

- `git diff --name-status origin/main...seafarers`
- `git diff origin/main...seafarers`
- `./catan.ps1 test`
- `./catan.ps1 lint`

## 1) File-by-File Sign-off

| File | Status | Reason |
|---|---|---|
| `.ai/ai-rules.md` | ✅ correct | Documentation/rules update only; no runtime code path changed. |
| `.ai/architecture-invariants.md` | ✅ correct | New constitution document aligns with intended invariant rubric for this review. |
| `.design/implementation-plans/seafarers-step1-plan.md` | ✅ correct | Planning artifact only; no executable behavior introduced. |
| `.design/implementation-plans/seafarers-step1-reconciliation-plan.md` | ✅ correct | Reconciliation plan is consistent with the constitution and branch intent. |
| `.design/old/seafarers-2026-02-draft.md` | ✅ correct | Archived/superseded design content only. |
| `.design/reviews/seafarers-review-copilot-round3.md` | ✅ correct | Review-history artifact only. |
| `.design/reviews/seafarers-review-copilot.md` | ✅ correct | Review-history artifact only. |
| `.design/reviews/seafarers-review-instructions.md` | ✅ correct | Review instruction text only; no production behavior impact. |
| `.design/seafarers.md` | ✅ correct | Design doc updated to reflect as-built reconciliation and invariant routing. |
| `Catan3.GameService/Abstractions/CosmosCatanDb.cs` | ✅ correct | Template persistence now round-trips `Features`; no runtime play-time template coupling added. |
| `Catan3.GameService/Data/DatabaseSeeder.cs` | ✅ correct | Adds file-based template seeding and sets empty features for code-defined templates; creation-time only. |
| `Catan3.GameService/Default Data/SystemTemplates/seafarers.json` | ✅ correct | Seafarers template authored as data; matches creation-time input model. |
| `Catan3.Shared/Models/GameEnums.cs` | ✅ correct | Adds `GameType.Seafarers`, `GameFeature`, `KeyboardShortcut` append-only and typegen-friendly. |
| `Catan3.Shared/Models/GameTemplateData.cs` | ⚠️ concern | Good removal of client metadata fields, but `TemplateEntitlement.Entitlement` remains stringly-typed (drift risk vs enum contract). |
| `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs` | ✅ correct | Registers new shared enums for generation to TypeScript. |
| `Catan3.Shared/TypeScript/TypeGenRunner/Program.cs` | ✅ correct | Description generation extended for new enums; expected generated diff. |
| `cspell.json` | ✅ correct | Dictionary update for project terms; no runtime behavior effect. |
| `react-ui/app/templates/[id]/page.tsx` | ⚠️ concern | `GAME_FEATURE_OPTIONS` and `ENTITLEMENT_OPTIONS` are enum-derived (good), but `RESOURCE_OPTIONS` and `HARBOR_*_OPTIONS` remain hand-authored lists (drift risk). |
| `react-ui/components/new-game/GameNameInput.tsx` | ✅ correct | Adds Seafarers display name mapping only. |
| `react-ui/components/new-game/GameTypeSelector.tsx` | ✅ correct | Uses `Seafarers` type explicitly with creation still gated; behavior aligns with step scope. |
| `react-ui/components/templates/EditorBoard.tsx` | ✅ correct | Harbor filtering now land-overlap aware, preserving coastal sea-harbor rendering for Seafarers templates. |
| `react-ui/lib/hooks/__tests__/useGameKeyboard.test.ts` | ✅ correct | Tests now keyed to generated shortcut descriptions, preserving mapping contract. |
| `react-ui/lib/hooks/useGameKeyboard.ts` | ✅ correct | Shortcut refactor is behavior-preserving for s/c/k/r/d while moving source-of-truth to shared enum descriptions. |
| `react-ui/types/generated/models/enum-descriptions.ts` | ✅ correct | Generated descriptions include `GameFeature` and `KeyboardShortcut` as expected. |
| `react-ui/types/generated/models/game-feature.ts` | ✅ correct | New generated enum type consistent with C# source and registrations. |
| `react-ui/types/generated/models/game-template-data.ts` | ✅ correct | Generated interface now includes typed `features`. |
| `react-ui/types/generated/models/game-type.ts` | ✅ correct | Generated `GameType` includes `Seafarers` value. |
| `react-ui/types/generated/models/index.ts` | ✅ correct | Barrel exports updated for new generated enums. |
| `react-ui/types/generated/models/keyboard-shortcut.ts` | ✅ correct | New generated enum type consistent with C# source and registrations. |

## 2) Findings (Most Severe First)

1. [important] `react-ui/app/templates/[id]/page.tsx:31` — `RESOURCE_OPTIONS` and `HARBOR_SIDE_OPTIONS` / `HARBOR_TYPE_OPTIONS` are still hand-authored literals — derive these from generated enums (with explicit filtering where needed) to satisfy invariant-4 single-definition guarantees and prevent drift.

2. [important] `Catan3.Shared/Models/GameTemplateData.cs:57` — `TemplateEntitlement.Entitlement` remains a plain string despite being a fixed shared vocabulary — change to shared `Entitlement` enum (or add strict conversion/validation) to eliminate string drift and enforce invariant-4 at the schema boundary.

## 3) Closing Verdict

### Invariant-by-invariant

1. Invariant 1 (GameModel is sole runtime source of truth): ✅

- No branch changes introduce play-time template reads for gameplay state; template additions are creation/authoring side.

1. Invariant 2 (GameState service-only): ✅

- No changes move phase/authority logic to client-side derivation.

1. Invariant 3 (client-only render/interaction metadata stays client-side, keyed by enum): ✅

- Template render metadata fields were removed from `TemplateEntitlement`; keyboard shortcuts moved to shared enum + client mapping.

1. Invariant 4 (shared enums defined once and generated to TS): ❌

- Partially met: `GameFeature` and `KeyboardShortcut` are correctly generated and used.
- Not fully met: hand-authored option arrays remain in `react-ui/app/templates/[id]/page.tsx` for resources/harbors; those should be enum-derived.

1. Invariant 5 (template authors only what varies per template): ✅

- Entitlement list and feature list are authored data; client presentation metadata was removed from template payload.

### Required command results observed

- `./catan.ps1 test`: PASS
  - `Tests.GameService`: 35 total, 33 passed, 2 skipped, 0 failed
  - `Tests.Shared`: 95 total, 95 passed, 0 failed
  - Combined observed .NET totals: 130 total, 128 passed, 2 skipped, 0 failed
  - TypeScript test stage reported passed

- `./catan.ps1 lint`: FAIL
  - 1 issue found (Spelling)
  - Markdown lint and JSON validation portions reported clean in the observed run

### Constitution Conformance (one-sentence verdict)

This branch is close and mostly aligned, but it does **not fully conform** to the constitution yet due to remaining hand-authored enum-option lists in the template editor (Invariant 4 gap).
