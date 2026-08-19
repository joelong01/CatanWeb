# Design Review Guidelines for Catan Project

**Last Updated:** 2026-02-13

Guidelines for conducting thorough, constructive design reviews. Applies to
both human reviewers and AI-assisted reviews.

## Purpose

Design reviews ensure:

- **Architectural soundness** -- designs are feasible, scalable, and consistent
  with the existing system
- **Completeness** -- all required aspects are addressed (data model, API,
  state management, UI, testing, migration)
- **Backward compatibility** -- existing functionality is preserved
- **Minimal scope** -- designs solve the stated problem without over-engineering
- **Alignment with project direction** -- designs reference and build on
  existing design docs in `.design/`
- **Implementability** -- a developer can read the design and build it without
  guessing

## Before You Start

### Read the Context

1. **`.ai/ai-rules.md`** -- project standards, conventions, and coding rules.
   **Load this first.** It governs how all work is done in this project.
2. **`.design/README.md`** -- the index of all verified design docs. Understand
   what already exists before reviewing a new design.
3. **The design being reviewed** -- read the entire document, not just the
   summary.
4. **Related design docs** -- follow cross-references. If the design says "see
   `seafarers.md`", read it.
5. **`.design/game-state-machine.md`** -- reference for state machine
   architecture if the design touches game logic.
6. **Relevant source code** -- verify claims made in the design against actual
   code. Designs that say "the current code does X" must be checked.

### Identify the Design Stage

This project uses a two-stage workflow:

- **Stage 1: Design doc** -- architecture, key decisions, high-level approach.
  Written to `.design/<feature>.md`.
- **Stage 2: Implementation plan** -- per-file changes, files-modified table,
  verification steps. Written to `.design/implementation-plans/<feature>-plan.md`.

Know which stage you are reviewing. A design doc review focuses on
**architecture and feasibility**. An implementation plan review focuses on
**completeness and correctness of the specific changes**.

## Review Output Location

**Reviews are GitHub issue comments, not files.** Post one comment per finding on the
issue that tracks the design, using `gh issue comment <number> --body-file <file>`.

See the **Reviews** section of `.ai/ai-rules.md` for the finding format, the response
format, and the resolution rules. That section is authoritative.

`.design/reviews/` is retained read-only for historical reviews written under the old
file-based convention. Do not add to it.

## Review Checklist

### 1. Problem Statement

- [ ] The design clearly states what problem it solves
- [ ] Goals and non-goals are explicit
- [ ] The current state is accurately described (verify against code)
- [ ] The motivation is justified -- why is this change needed now?

### 2. Architecture

- [ ] The design is consistent with existing architecture (check
  `.design/README.md` for related docs)
- [ ] Data model changes are backward compatible or have a migration plan
- [ ] API changes are backward compatible or document breaking changes
- [ ] State management approach is consistent with existing patterns
- [ ] The design doesn't duplicate existing functionality
- [ ] Cross-cutting concerns are addressed (auth, logging, error handling)

### 3. Feasibility

- [ ] All referenced code/interfaces/classes actually exist (verify in source)
- [ ] Proposed changes are technically possible with the current stack
- [ ] Performance implications are considered (especially for hot paths)
- [ ] Database changes are compatible with the document-style storage pattern
- [ ] The design accounts for the undo/redo snapshot system if touching
  game logic
- [ ] The design accounts for the recording/replay system if touching
  game logic

### 4. Completeness

- [ ] All layers are covered: data model, service layer, API, client
- [ ] Testing strategy is defined
- [ ] Migration/seed data plan exists for data model changes
- [ ] TypeGen updates are noted for new C# types exposed to React
- [ ] Deployment considerations are addressed (staging, production)
- [ ] Milestones are ordered logically with clear verification points

### 5. Scope and Simplicity

- [ ] The design solves the stated problem without scope creep
- [ ] Abstractions are justified (no premature abstraction)
- [ ] Future-proofing is limited to low-cost decisions (like an enum field)
  not speculative architecture
- [ ] The design doesn't refactor code that doesn't need refactoring
- [ ] Phase boundaries are clear -- what ships now vs. what ships later

### 6. Risk Assessment

- [ ] Risks are identified and have mitigations
- [ ] The highest-risk changes are isolated and testable independently
- [ ] Backward compatibility risks are explicitly called out
- [ ] Open questions are listed, not hidden

### 7. Implementation Plan Specifics (Stage 2 Only)

- [ ] Every file to be modified/created is listed
- [ ] Changes per file are specific enough to implement without guessing
- [ ] The order of changes makes sense (dependencies before dependents)
- [ ] Verification steps are concrete and runnable (`./catan.ps1 test`,
  specific curl commands, etc.)
- [ ] The plan accounts for existing tests that might break

## Comment Templates

### One comment per finding

```markdown
### Finding N: <short title>

**Severity:** Critical | Important | Suggestion | Question
**Section:** <section of the design doc, or `path/to/file.cs:123`>
**Issue:** What is wrong, and why it matters.
**Recommendation:** The specific change to make.
**Evidence:** What you read or ran to confirm this.
```

Severity means:

- **Critical** -- must be resolved before proceeding; blocks approval
- **Important** -- should be addressed, but does not block
- **Suggestion** -- nice to have
- **Question** -- clarification needed from the author

### Final summary comment

```markdown
### Review Summary

**Reviewer:** <name or AI identifier>
**Stage:** Design Doc | Implementation Plan

<2-3 sentences: what the design proposes and your overall assessment>

| # | Severity | Finding |
|---|----------|---------|
| 1 | Critical | ... |

**Verified against code:**

| Claim | Evidence | Status |
|-------|----------|--------|
| "<quoted claim from the design>" | `path/to/file.cs:123` | Verified / Incorrect / Could Not Verify |

**What the design does well:** <praise — this is useful signal, not filler>
```

Record every claim you verified, including the ones that checked out. A verification that
passed is evidence the design is trustworthy; omitting it hides the work.

## Review Process

### Phase 1: Context (20% of time)

1. Read `.ai/ai-rules.md` for project standards and conventions
2. Read `.design/README.md` to understand the doc landscape
3. Read the design doc being reviewed -- entirely, not skimming
4. Read all cross-referenced design docs

### Phase 2: Verification (40% of time)

1. **Verify claims against code.** The design says "`HandleNewGameAsync` reads
   parallel arrays" -- open the file and check. Record each verification in
   the review.
2. **Check for conflicts.** Does the design contradict anything in existing
   design docs? Does it duplicate existing functionality?
3. **Trace the data flow.** Walk through a concrete scenario end-to-end: a
   request arrives, goes through the API, hits the state machine, updates the
   database, returns to the client. Does the design handle every step?
4. **Check edge cases.** What happens with empty data? What happens during
   concurrent access? What happens if the database is unavailable?

### Phase 3: Analysis (25% of time)

1. **Evaluate architecture decisions.** Are the chosen patterns appropriate?
   Is there a simpler approach that achieves the same goals?
2. **Assess scope.** Is the design doing too much or too little?
3. **Check testability.** Can the proposed changes be tested without
   end-to-end infrastructure? Are the verification steps concrete?
4. **Consider deployment.** Can this be deployed incrementally? What happens
   if the deploy is partially applied?

### Phase 4: Documentation (15% of time)

1. Post each finding as its own comment on the tracking issue
2. Organize by severity (Critical, Important, Suggestion, Question, Praise)
3. Include verification evidence for every claim you checked
4. Provide concrete recommendations, not vague feedback

## Common Design Review Anti-Patterns

**Avoid these in designs:**

- **Speculative architecture** -- building for requirements that don't exist
  yet. A string `"engine"` field is fine; a full plugin system is not.
- **Abstraction for one use case** -- if there's only one implementation, you
  don't need an interface yet.
- **Ignoring existing patterns** -- the project has established patterns for
  database entities, API endpoints, state management. New designs should follow
  them unless there's a good reason not to.
- **Missing migration story** -- any data model change needs a plan for
  existing data.
- **Vague testing** -- "we'll add tests" is not a testing strategy. Name the
  specific tests and what they verify.

**Avoid these in reviews:**

- **Rubber stamping** -- approving without reading
- **Scope creep** -- requesting changes outside the design's stated scope
- **Rewriting** -- proposing a fundamentally different design instead of
  reviewing the one presented
- **Vague feedback** -- "this could be better" without specifics
- **Blocking on style** -- design docs don't need perfect prose; they need
  clear architecture

## AI-Assisted Design Reviews

### When to Use

- Systematic verification of claims against code
- Checking cross-references between design docs
- Identifying conflicts with existing architecture
- Ensuring completeness of the design checklist

### Limitations

AI reviews cannot replace human judgment for:

- Business requirement validation
- Strategic architectural trade-offs
- Priority decisions (what to build first)
- Assessing team capacity and skill alignment

### Quick Start for AI Sessions

When a user asks you to perform a design review, follow this exact sequence:

1. Read `.ai/ai-rules.md` -- project standards (load first, always)
2. Read `.design/README.md` -- understand the full doc landscape
3. Read the design doc(s) being reviewed -- entirely, not skimming
4. Read all docs cross-referenced by the design (e.g., if it references
   `game-state-machine.md`, read that too)
5. Follow the Review Process phases below (Context → Verification → Analysis
   → Documentation)
6. Post findings as issue comments (one per finding), never as files in the
   repository

### Instructions for AI Reviewers

1. **Read `.ai/ai-rules.md` first** -- this governs all project work
2. **Read the entire design doc** before forming opinions
3. **Read `.design/README.md`** to understand the full doc landscape
4. **Verify every factual claim** against the actual codebase using Read/Grep
5. **Record verification evidence** in the review (file paths, line numbers)
6. **Be specific** -- reference exact sections of the design doc
7. **Distinguish severity** -- don't mark style issues as critical
8. **Post findings as issue comments**, one per finding — never write review files
