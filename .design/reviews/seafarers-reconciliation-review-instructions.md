# Seafarers reconciliation — full AI review instructions

**You are a reviewing AI.** Review the `seafarers` branch against the project's
architecture constitution and produce a file-by-file sign-off. Follow these
instructions exactly, including where to write your output and what to name it.

## 0. Where to put your output (required)

Write your review to a **new file**:

```text
.design/reviews/seafarers-reconciliation-review-<your-tool>.md
```

Replace `<your-tool>` with your identity in lowercase kebab-case — `codex`,
`copilot`, `gpt5`, or `claude` (e.g. `.design/reviews/seafarers-reconciliation-review-codex.md`).
This matches the existing convention (`.design/reviews/seafarers-review-copilot.md`).
Do **not** overwrite any existing file. Do **not** edit source files — this is a
read-only review.

## 1. Scope

- **Branch:** `seafarers`. **Base:** `origin/main` (the branch is unpushed, so
  "changed since the last push" = the entire diff vs `origin/main`).
- **Exact diff to review:** `git diff origin/main...seafarers` and
  `git diff --name-status origin/main...seafarers` (29 files, listed in §5).
- **Commit range:** `origin/main..seafarers` (19 commits, `e3a909c`…`1a7cb34`).

## 2. Context to load first (in this order)

1. `.ai/architecture-invariants.md` — the **constitution**; this is your rubric.
2. `.ai/ai-rules.md` — project standards.
3. `.design/seafarers.md` — the design (esp. the top "As-built status" section and D13).
4. `.design/implementation-plans/seafarers-step1-reconciliation-plan.md` — the plan
   the code was built to.

## 3. What you are verifying

The in-flight Seafarers work must (a) conform to the five architecture invariants,
and (b) make its mechanisms — keyboard shortcuts and the template feature set —
**general seams that Regular/Expansion also ride**, not Seafarers special-cases.

Scrutinize specifically:

1. **Invariant 1 (GameModel is the only runtime truth).** Does anything read a
   template at play time? Confirm `GameTemplateData.Features` has no runtime reader
   (only DB round-trip). Confirm nothing resurrects live template resolution
   (the design's old D13d).
2. **Invariant 2 (GameState is service-only).** No client-derived game state.
3. **Invariant 3 (render/interaction options client-side, keyed by enum).** Is any
   glyph/label/keyboard/tooltip metadata on a template or in `GameModel`
   (`TemplateEntitlement`, `EntitlementPurchaseModel`)? It must be client config keyed
   by the `Entitlement`/`KeyboardShortcut` enums.
4. **Invariant 4 (enums defined once in `Catan3.Shared`, generated to TS).** Any
   hand-authored enum vocabulary in `react-ui` that should derive from a generated
   enum? (Check `GAME_FEATURE_OPTIONS`, `ENTITLEMENT_OPTIONS`, and the pre-existing
   `RESOURCE_OPTIONS`/`HARBOR_TYPE_OPTIONS`/`HARBOR_SIDE_OPTIONS`.)
5. **Invariant 5 (template authors only per-template variation).**
6. **`useGameKeyboard.ts` refactor** — behavior-preserving vs the old `s/c/k/r/d`
   switch? Check the interaction with the letter-key (A–Z) road/city-index branch that
   runs before the purchase lookup.
7. **Hash-neutrality** — the changeset claims "no `GameModel` change, hashes
   unchanged." Verify it. Note that the `ReplayRegular/ExpansionTest` tests are
   **skipped** (deprecated), so there is no live replay-hash guard.
8. **Generated files** — the six `react-ui/types/generated/models/*` files must match
   what the type-gen pipeline produces from the C# source; flag any hand-editing.
9. **Design ↔ code consistency** — does `.design/seafarers.md` match the code, with no
   lingering contradiction (esp. D13a/b/d and the `ResourceRules.MaxShips` vs
   per-entitlement `Max` note)?

## 4. Verification (mandatory — do not skip)

Run tests and lint **only** through the unified script (this is an operational law in
the constitution — the script provisions the CosmosDB emulator; raw `dotnet test` /
`npm test` produce false failures):

```bash
./catan.ps1 test      # build + Cosmos emulator + all .NET and TS tests
./catan.ps1 lint      # format, lint, spell-check
```

State the actual pass/fail counts you observed. If Docker isn't running,
`./catan.ps1 test` starts the emulator itself.

## 5. File-by-file sign-off (required — every changed file)

For **each** of the 29 files below, record a verdict and a one-line sign-off. Use:

- **✅ correct as written** — no change needed
- **⚠️ concern** — works but has an issue worth raising (explain in Findings §6)
- **❌ defect** — incorrect as written (explain + fix in Findings §6)

Copy this table into your output file and fill the last two columns.

| # | File | Verdict | Sign-off note |
|---|---|---|---|
| 1 | `Catan3.Shared/Models/GameEnums.cs` | | |
| 2 | `Catan3.Shared/Models/GameTemplateData.cs` | | |
| 3 | `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs` | | |
| 4 | `Catan3.Shared/TypeScript/TypeGenRunner/Program.cs` | | |
| 5 | `Catan3.GameService/Abstractions/CosmosCatanDb.cs` | | |
| 6 | `Catan3.GameService/Data/DatabaseSeeder.cs` | | |
| 7 | `Catan3.GameService/Default Data/SystemTemplates/seafarers.json` | | |
| 8 | `react-ui/app/templates/[id]/page.tsx` | | |
| 9 | `react-ui/components/new-game/GameNameInput.tsx` | | |
| 10 | `react-ui/components/new-game/GameTypeSelector.tsx` | | |
| 11 | `react-ui/components/templates/EditorBoard.tsx` | | |
| 12 | `react-ui/lib/hooks/useGameKeyboard.ts` | | |
| 13 | `react-ui/lib/hooks/__tests__/useGameKeyboard.test.ts` | | |
| 14 | `react-ui/types/generated/models/enum-descriptions.ts` | | |
| 15 | `react-ui/types/generated/models/game-feature.ts` | | |
| 16 | `react-ui/types/generated/models/game-template-data.ts` | | |
| 17 | `react-ui/types/generated/models/game-type.ts` | | |
| 18 | `react-ui/types/generated/models/index.ts` | | |
| 19 | `react-ui/types/generated/models/keyboard-shortcut.ts` | | |
| 20 | `cspell.json` | | |
| 21 | `.ai/ai-rules.md` | | |
| 22 | `.ai/architecture-invariants.md` | | |
| 23 | `.design/seafarers.md` | | |
| 24 | `.design/implementation-plans/seafarers-step1-plan.md` | | |
| 25 | `.design/implementation-plans/seafarers-step1-reconciliation-plan.md` | | |
| 26 | `.design/old/seafarers-2026-02-draft.md` | | |
| 27 | `.design/reviews/seafarers-review-copilot.md` | | |
| 28 | `.design/reviews/seafarers-review-copilot-round3.md` | | |
| 29 | `.design/reviews/seafarers-review-instructions.md` | | |

**Notes on categories:**

- **Files 14–19 are generated** — "correct as written" means *matches the pipeline
  output from the current C# source and is not hand-edited*. Regenerate with
  `./catan.ps1 generate-types` and diff if unsure.
- **Files 26–29 are prior-session artifacts** (a superseded draft and earlier review
  docs). For these, "correct as written" = *does not contradict the current
  constitution/design*; deep correctness was reviewed previously.

## 6. Findings (ranked)

List every ⚠️/❌ from §5, most-severe first, each as:

```text
[severity] file:line — problem — suggested fix
```

## 7. Required closing statement

End your output file with an explicit, signed verdict:

- **Invariant-by-invariant result:** 1 ✅/❌, 2 ✅/❌, 3 ✅/❌, 4 ✅/❌, 5 ✅/❌.
- **Test/lint result:** the counts you observed from `./catan.ps1 test` / `lint`.
- **Overall sign-off sentence:** e.g. *"Reviewed by `<tool>` on `<date>`; all 29
  changed files are correct as written except {list}; the branch does / does not
  conform to the architecture constitution."*
