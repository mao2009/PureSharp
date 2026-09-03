# PureSharp Batch & Merge Workflow

End-to-end flow from issue selection to a merged, verified PR.

This document describes **how the phases fit together**. It does not restate the gates,
the evidence requirements, or the failure behaviour — those live in the specifications:

- `batch-skill/SKILL.md` — batch orchestration and parallel safety
- `merge-skill/SKILL.md` — merge gates, approval semantics, human approval boundary

Where this document and a `SKILL.md` disagree, the `SKILL.md` wins.

## Overview

```text
Issue selection
    ↓
Batch analysis      (Batch Skill)      dependencies, parallel safety, execution graph
    ↓
Implementation      (per issue)        spec-driven work on a feature branch
    ↓
Pull request        (per issue)
    ↓
Merge verification  (Merge Skill)      gates → MERGE CANDIDATE or MERGE BLOCKED
    ↓
Human review + explicit human approval
    ↓
Merge execution     (Merge Skill)
    ↓
Post-merge verification
    ↓
Batch report
```

No step in this workflow requires a particular shell, runtime, or CLI. Each phase
establishes facts from whichever trusted source the environment offers.

## Phase 1 — Batch analysis

Invoke the Batch Skill with the issues under consideration.

It retrieves live issue state, reads requirements, establishes dependencies, investigates
affected areas, classifies every finding, and decides parallel safety pairwise. The
output is an execution graph: ordered layers, with parallel groups only where safety is
CONFIRMED on every dimension.

Expect serial execution to be the common answer. Two issues touching disjoint files can
still conflict through shared configuration, a shared public contract, architectural
coupling, or a required order — so file non-overlap alone never justifies parallelism. A
batch that runs serially because independence could not be proven is a correct outcome.

## Phase 2 — Implementation

For each issue, in the order the execution graph gives:

1. Create a feature branch for the issue
2. Implement against the issue's requirements
3. Build and test locally
4. Open a pull request
5. Let CI run

Use the repository's existing spec-driven workflow for the implementation itself. The
Batch Skill governs *ordering and concurrency*, not how any one issue is implemented.

## Phase 3 — Merge verification and integration

The full boundary, in order. Every step is mandatory:

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
EXPLICIT HUMAN APPROVAL          ← required; no automated substitute
    ↓
MERGE EXECUTION
    ↓
POST-MERGE VERIFICATION
```

**Technical verification success is not permission to merge.** Passing every gate through
VERIFY MERGEABILITY produces a **MERGE CANDIDATE** — a candidate, not a decision. Only an
explicit human approval moves it to MERGE EXECUTION.

At the human review step, read the verification report and confirm that every gate item
is CONFIRMED, and that no gate item is INFERRED or UNVERIFIED. If the report says MERGE
BLOCKED, fix the cause and re-verify from the top; a blocked PR is never merged.

Immediately before executing the merge, the Merge Skill re-confirms that nothing moved —
HEAD, CI, approvals, mergeability, and the human approval itself. Any of these failing
re-confirmation blocks the merge and sends the PR back to VERIFY.

## Phase 4 — Reporting

After the batch completes, produce both layers:

- **Human report** — what ran, success and failure breakdown, blockers, next steps
- **AI report** — per-finding evidence classification, per-issue state, verification
  results, and the parallel-safety decision with the evidence behind it

See `EXAMPLE-REPORT.md`.

## Failure handling

Every failure below is handled the same way: **fix the cause, then re-verify from the
top.** Verification evidence is bound to a commit, so any new push invalidates all prior
evidence — including approvals already given.

| Failure | Action |
|---|---|
| Build failed | Fix on the feature branch, push, wait for CI, re-verify |
| Tests failed | Investigate the failures, fix code or test, push, re-verify |
| CI pending or in progress | Wait for completion, then re-verify. Pending is not a pass |
| CI failed | Fix the cause, push, re-verify |
| CI belongs to an older commit | Stale — wait for CI on the current HEAD, then re-verify |
| Merge conflict | Rebase the feature branch on the base branch, resolve, test, push, re-verify |
| Base branch moved | Re-verify; rebase if the move invalidates the verification |
| Approvals insufficient | Request review. Never substitute an automated verdict for a human approval |
| Repository requires reviews, none present | Obtain the required third-party reviews. Out-of-band approval cannot substitute |
| Self-authored PR, no third-party reviewer available | Use the out-of-band approval path in `merge-skill/SKILL.md`, bound to the exact HEAD SHA. Never record a self-approval as a GitHub review |
| `mergeMethod` not enabled in the repository | CONFIG ERROR. Fix the configuration; never silently merge by another method |
| Changes requested | Address the review, push, obtain a fresh approval on the new HEAD |
| PR HEAD changed mid-verification | Restart verification from the top |
| Required evidence unobtainable | UNVERIFIED → BLOCKED. Find another trusted source; do not skip the check |

Nothing in this table is merged around, waived, or configured away.

## See also

- `README.md` — overview, invocation, safety principles
- `batch-skill/SKILL.md`, `merge-skill/SKILL.md` — the specifications
- `EXAMPLE-REPORT.md` — reporting example
- Issue #18 — Batch and Merge Skills
- Issue #6 — PureSharp v1.0.0 roadmap
