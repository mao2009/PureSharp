---
name: merge-skill
description: Safe integration of a PureSharp pull request into the base branch. Defines the evidence that must be CONFIRMED before a PR becomes a merge candidate, the human approval boundary, and the post-merge verification contract. Use when verifying, approving, or executing a merge.
---

# Merge Skill for PureSharp Safe Integration

## Status of this document

**This document is the executable specification and the single source of truth (SSOT)
for merge safety in this repository.** There is no helper script. Behaviour is defined
here, not in any `.ps1`, `.sh`, `.py`, or `.js` file, and none may be introduced as a
requirement of this skill.

The skill defines **which facts must be established**, not **which commands to run**.
Any trusted tool available in the execution environment may be used to establish them.

## Design Principles

1. **SKILL.md is the SSOT.** If this document and any other artifact disagree, this
   document wins.
2. **No required environment-dependent script.** No PowerShell, shell, Python, or Node
   helper is required, and none may be added to satisfy this skill. Migrating the logic
   from one scripting language to another is not a valid response to this rule.
3. **Facts, not commands.** Each gate names the fact to establish and the acceptance
   criterion, never a fixed command line.
4. **Any trusted source is acceptable.** See "Selecting a trusted source".
5. **Unavailable evidence is UNVERIFIED, and UNVERIFIED blocks.** Never skip a check
   because a tool is missing.
6. **Never infer a gate open.** Gates require CONFIRMED evidence.
7. **Human approval is required before every merge.** Technical success is not
   permission.

## Fixed Safety Policy (not configurable)

These hold in every environment and cannot be relaxed by configuration. If a
configuration file appears to weaken any of them, the configuration is wrong and the
merge is BLOCKED.

- Fail closed: any error, ambiguity, or missing evidence blocks the merge.
- Explicit human approval is required before merge execution.
- Auto-merge is never enabled by this skill.
- Verification is bound to an exact commit SHA; stale evidence never satisfies a gate.
- UNVERIFIED blocks. INFERRED does not satisfy a gate.
- This skill never force-pushes and never pushes directly to the base branch.
- Working tree must be clean at the point of any local verification.

## Evidence Classification

Shared with the Batch Skill. Use these three labels and no others.

| Label | Meaning |
|---|---|
| **CONFIRMED** | Actually observed from a trusted source during this verification run. |
| **INFERRED** | A reasonable conclusion drawn from CONFIRMED facts, but not itself observed. |
| **UNVERIFIED** | Not established — including "the tool was unavailable" and "the answer was ambiguous". |

Rules:

- Every gate item in this document requires **CONFIRMED**.
- **INFERRED** may be reported for context and may support a decision that fails safe
  (for example, choosing serial execution), but never opens a gate.
- **UNVERIFIED** blocks. Report it as UNVERIFIED; do not silently omit it.
- Evidence is bound to the SHA it was observed for. Re-using evidence gathered for a
  different SHA makes it UNVERIFIED, not CONFIRMED.

## Selecting a Trusted Source

Establish each fact from whichever trusted source the environment actually offers.
No single mechanism is mandatory. In rough order of preference:

1. A first-party GitHub connector or integration, where the host designates it as such.
2. The GitHub CLI (`gh`), including its GraphQL access.
3. The GitHub REST/GraphQL API through an authenticated client.
4. `git` itself, for purely local facts (branch, commit, ancestry, working tree).
5. Repository-native build and test tooling, for build and test facts.

Rules:

- Prefer the source that reports the fact **directly**. Do not derive a PR's HEAD from a
  local branch when the PR itself can be asked.
- Local `git` alone cannot establish PR state, CI results, reviews, or mergeability.
- If **no** available source can establish a required fact: record it **UNVERIFIED** and
  report **MERGE BLOCKED**. "The tool was not available, so the check was skipped" is
  never acceptable.

## Required Evidence

Each item below must be CONFIRMED, bound to the verification SHA, before the PR can be
reported a merge candidate.

### A. PR identity

- PR number
- PR state (must be OPEN)
- Base branch
- **PR HEAD SHA** — this is the *verification SHA*; every other item is bound to it

### B. Git state

- Current working branch
- The HEAD under verification, and that it equals the PR HEAD SHA
- Working tree state (clean: no uncommitted changes, no untracked files, no merge or
  rebase in progress)
- Relationship to the base branch (ancestry known; whether the base has moved since the
  PR branch diverged)

### C. Build and test

- Required build completed and succeeded, for the verification SHA
- Required test run completed with zero failures, for the verification SHA
- Any repository-specific validation required for the changed area

Build and test evidence may come from CI (item D) rather than a local run, provided it
is bound to the verification SHA. A local run at a different SHA does not satisfy this.

### D. CI

- The CI result that belongs to the **exact** verification SHA
- **CI HEAD SHA == PR HEAD SHA** — CONFIRMED, not assumed
- All required checks completed (none pending, queued, or in progress)
- All required checks concluded successfully

A CI result for any other commit is stale. Stale CI is UNVERIFIED and blocks.

### E. Reviews

- Effective human approvals, computed per "Approval Semantics" below
- The required approval count in force
- No blocking review state outstanding

### F. Mergeability

- No merge conflicts with the base branch
- Repository merge state reported by the host
- Any required repository policy checks (branch protection, required checks) satisfied

### G. Human approval

- Explicit human approval to execute the merge of this specific PR at this specific SHA

## Approval Semantics

This is a contract, not an implementation. Any source that can answer these questions is
acceptable, provided it can report, per review: the reviewer identity, whether the
reviewer is a human or a bot, the review state, the review's submission time, and the
commit the review was submitted against.

**A review counts as an effective human approval only when all hold:**

- The reviewer is a **human**, not a bot.
- The review state is **APPROVED**.
- It is that reviewer's **latest effective review**.
- The review was submitted against a commit equal to the **current PR HEAD**.

**Determining a reviewer's latest effective state:**

- Only `APPROVED`, `CHANGES_REQUESTED`, and `DISMISSED` change a reviewer's state.
- `COMMENTED` and `PENDING` reviews never change it.
- Each reviewer contributes **at most one** state: their most recent state-changing review.

**Never counted as an approval:**

| Case | Result |
|---|---|
| Bot review (any state) | not an approval |
| `COMMENTED` / `PENDING` | not an approval |
| Review that was dismissed | not an approval |
| Reviewer approved, then later requested changes | not an approval — and blocking |
| Approval submitted against an older commit (stale HEAD) | not an approval |
| Reviewer identity, timestamp, or reviewed commit cannot be determined | UNVERIFIED → BLOCKED |

**Bot identification.** Treat a reviewer as a bot when the host reports the account as a
bot type, when the login carries a bot marker such as a `[bot]` suffix, or when the login
is a known automation account for this repository (for example the code-review bot).
Automated review tooling never contributes a human approval, regardless of verdict.

**Outcomes:**

- Effective human approvals ≥ required count, and no blocking review → gate CONFIRMED.
- Effective human approvals < required count → **MERGE BLOCKED**.
- Any reviewer's latest effective state is `CHANGES_REQUESTED` → **MERGE BLOCKED**.
- Review data cannot be obtained or is incomplete → **UNVERIFIED → MERGE BLOCKED**.

Approval is never inferred from a passing build, from elapsed time, from a comment that
reads as positive, or from the absence of objection.

## HEAD Binding

Every gate is bound to one SHA — the PR HEAD observed at the start of verification.

- Record the verification SHA explicitly, and report it in every result.
- CI evidence, review evidence, and build/test evidence must each belong to that SHA.
- If the PR HEAD changes at any point during verification, the run is invalid.
  Report **BLOCKED — PR HEAD changed during verification** and start over.
- A new push to the PR invalidates all prior evidence, including prior approvals.

## Merge Workflow

```text
VERIFY PR IDENTITY
    ↓
VERIFY CURRENT HEAD
    ↓
VERIFY GIT STATE
    ↓
VERIFY BUILD / TEST
    ↓
VERIFY CI FOR EXACT HEAD
    ↓
VERIFY EFFECTIVE HUMAN REVIEWS
    ↓
VERIFY MERGEABILITY
    ↓
PRODUCE MERGE CANDIDATE REPORT
    ↓
HUMAN REVIEW
    ↓
EXPLICIT HUMAN APPROVAL
    ↓
MERGE EXECUTION
    ↓
POST-MERGE VERIFICATION
```

**Technical verification success is not permission to merge.** Passing every gate
through "VERIFY MERGEABILITY" produces a **MERGE CANDIDATE** and nothing more. The
transition to MERGE EXECUTION is authorised only by an explicit human decision.

The skill's default mode is verification only: it reads state, changes nothing, pushes
nothing, and is safe to run at any time.

## Merge Execution

No shell-specific command is prescribed. Choose the mechanism from the trusted sources
available, honouring the repository's merge policy and the configured merge method.

**Prefer a mechanism that lets you state the expected HEAD**, so the host itself refuses
the merge if the PR moved. Where the host supports it, pass the expected SHA rather than
merging whatever is current.

**Immediately before executing the merge, re-confirm all of:**

- [ ] Explicit human approval exists for this PR at this SHA
- [ ] PR HEAD unchanged since verification
- [ ] CI still valid and successful for the current HEAD
- [ ] Effective human approvals still satisfy the required count
- [ ] No blocking review state has appeared
- [ ] Mergeability unchanged; no conflicts
- [ ] Base branch has not moved in a way that invalidates the verification

If any of these cannot be re-confirmed: **BLOCKED**. Do not merge. Return to VERIFY.

Never bypass branch protection, never force-push, never push directly to the base
branch, and never enable auto-merge to satisfy a gate.

## Post-Merge Verification

Required evidence, each CONFIRMED:

- PR state is `MERGED`
- The actual merge commit is identified
- The base branch contains the expected result
- Base branch HEAD and state verified after the merge
- Post-merge CI result, where the repository requires one
- Linked issue state updated as expected
- Branch cleanup eligibility determined

Anything not established here is **UNVERIFIED**. Report it as such — a completed merge
does not retroactively confirm what was never checked.

## Failure Behaviour

| Situation | Result |
|---|---|
| Any required evidence UNVERIFIED | **MERGE BLOCKED** |
| Any gate item only INFERRED | **MERGE BLOCKED** |
| CI failed, pending, queued, or in progress | **MERGE BLOCKED** |
| CI belongs to a different SHA than PR HEAD | **MERGE BLOCKED** (stale) |
| Effective human approvals below required count | **MERGE BLOCKED** |
| Any `CHANGES_REQUESTED` outstanding | **MERGE BLOCKED** |
| Merge conflict, or mergeability unknown | **MERGE BLOCKED** |
| Working tree dirty at local verification | **MERGE BLOCKED** |
| PR HEAD changed mid-verification | **BLOCKED**, restart verification |
| Configuration missing, malformed, or invalid | **CONFIG ERROR → MERGE BLOCKED** |
| Configuration attempts to weaken fixed safety policy | **CONFIG ERROR → MERGE BLOCKED** |
| No explicit human approval | **MERGE BLOCKED** |
| No trusted source can establish a required fact | **UNVERIFIED → MERGE BLOCKED** |

There is no configuration, flag, or argument that converts any of these into a pass.

## Configuration

`.kiro/merge.config.json` carries **data only**. All behaviour is defined in this
document. Configuration can make the gate *stricter*, never weaker.

Schema:

| Key | Type | Meaning |
|---|---|---|
| `requiredApprovals` | integer ≥ 1 | Effective human approvals required. Must be at least 1; there is no way to require zero. |
| `mergeMethod` | `"merge"` \| `"squash"` \| `"rebase"` \| `"ff-only"` | Merge method to use during MERGE EXECUTION. |
| `deleteBranchAfterMerge` | boolean | Whether the PR branch is eligible for deletion after a confirmed merge. |

Rules:

- Missing file, malformed JSON, an unknown key, a key of the wrong type, or
  `requiredApprovals < 1` → **CONFIG ERROR → MERGE BLOCKED**. There is no default
  fallback for `requiredApprovals`.
- Anything not in the table above is not configurable. Human approval, fail-closed
  behaviour, HEAD binding, and auto-merge being disabled are fixed policy and are
  deliberately absent from the schema so they cannot be switched off.

## Report Format

Report the verification SHA, then each evidence group with its classification, then the
verdict. See `../EXAMPLE-REPORT.md` for a worked example.

The verdict is exactly one of:

- **MERGE CANDIDATE** — every gate CONFIRMED; awaiting human review and explicit approval
- **MERGE BLOCKED** — at least one gate failed, INFERRED, or UNVERIFIED
- **MERGED** — merge executed after explicit approval, with post-merge verification result

Never report "safe to merge" or "ready to merge" as a substitute for MERGE CANDIDATE;
those phrasings read as authorisation, which this skill cannot grant.

## Related

- `../batch-skill/SKILL.md` — batch orchestration and parallel safety
- `../WORKFLOW.md` — end-to-end workflow from issue to merged PR
- `../EXAMPLE-REPORT.md` — reporting example
- Issue #18 — Batch and Merge Skills
- Issue #6 — PureSharp v1.0.0 roadmap
