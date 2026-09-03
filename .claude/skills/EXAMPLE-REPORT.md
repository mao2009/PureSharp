# Reporting Example

An illustrative report showing the two-layer format used by both skills. The numbers,
issue references, and SHAs below are **fictional** — this file demonstrates *format and
evidence discipline*, nothing more. It is not a record of real work.

For the rules themselves see `batch-skill/SKILL.md` and `merge-skill/SKILL.md`.

---

# Human Report

## v1.0 Phase 1 — Batch Execution

**Result**: 4 of 4 issues completed and merged
**Execution**: serial (parallel safety not CONFIRMED for any pair)

| Issue | Title | PR | Result |
|---|---|---|---|
| #7 | Diagnostic SSOT | #121 | Merged |
| #8 | Regression Tests | #122 | Merged |
| #9 | RT Semantics | #123 | Merged |
| #10 | Interprocedural Analysis | #124 | Merged |

**Changes**: 12 files, +1,247 / −89 lines, 3 new test files
**Verification**: build passed, 234/234 tests passed, CI passed for each PR HEAD
**Approvals**: 1 effective human approval per PR, each bound to that PR's final HEAD

**Notes for the reader**

- Issues #7–#10 form a dependency chain, so they ran serially. Parallelism was never
  eligible here.
- Each merge was executed only after an explicit human approval of that specific HEAD.

**Next**: Phase 2 (#11–#13). Their independence is not yet CONFIRMED, so plan for serial
execution until it is.

---

# AI Report

## Verification SHA binding

Every item below is bound to the SHA named beside it. Evidence gathered for a different
SHA is not reused.

| PR | Verification SHA |
|---|---|
| #121 | `abc1234` |
| #122 | `bcd2345` |
| #123 | `cde3456` |
| #124 | `def4567` |

## CONFIRMED

Observed from a trusted source during this run.

**Git state** (per issue, at its verification SHA)
- Branch, base commit, and result commit identified for each of #7–#10
- Working tree clean at each verification point — no uncommitted or untracked files
- Each feature branch ancestry to the base branch established

**Build and test**
- Build succeeded for each verification SHA
- 234/234 tests passed; 0 failed, 0 skipped
- Analyzer suites: RT 24 passed, LVP 18 passed, FIF 12 passed

**CI**
- CI concluded successfully for each PR
- CI HEAD == PR HEAD confirmed for each PR (see the SHA table above)
- No required check left pending, queued, or in progress
- Stale runs at superseded commits were identified and excluded

**Reviews**
- Required approvals: 1 (source: `.kiro/merge.config.json` → `requiredApprovals`)
- Effective human approvals: 1 per PR, each submitted against that PR's final HEAD
- Stale approvals ignored: 0
- Bot reviews excluded: 4 — automated review tooling, not counted as human approval
- Outstanding `CHANGES_REQUESTED`: none

**Mergeability**
- No conflicts with the base branch for any PR
- Host-reported merge state clean for each PR

**File conflict analysis** — read from the actual branch diffs, not inferred from issue
descriptions, because the batch conflict gate requires CONFIRMED evidence:
- #7 touches `Analyzer.cs`, `DiagnosticCatalog.cs`, localization files
- #8 touches `Tests/`, `TestData/`, `TestBase.cs` — no overlap with #7
- #9 touches `ReferentialTransparencyAnalyzer.cs`, `SemanticAnalyzer.cs` — no overlap with #7, #8
- #10 touches `InterproceduralAnalyzer.cs`, `CallGraphBuilder.cs` — no overlap with #7–#9
- Shared mutable configuration touched by more than one issue: none
- Shared public contract touched by more than one issue: none

**Human approval**
- Explicit human approval recorded per PR, naming the PR and its SHA, before execution

**Post-merge**
- Each PR state `MERGED`; merge commit identified for each
- Base branch contains the expected result; base branch HEAD verified after each merge
- Post-merge CI on the base branch concluded successfully

## INFERRED

Reasonable conclusions from the confirmed facts. **None of these opened a gate.** They
are recorded for context, and where they bore on execution they pushed toward the safe
side.

- **Dependency chain satisfied** — inferred from execution order: #7 completed first,
  then #8, #9, #10, each after its blocker merged.
- **Phase 2 (#11–#13) may be independent** — inferred from the analyzers they are
  scoped to. This did **not** authorise parallelism. Independence must be CONFIRMED on
  every dimension before #11–#13 run in parallel; until then they run serially.

## UNVERIFIED

Not established. Each would block any gate that depended on it.

- Long-term performance impact of the #10 interprocedural changes — not measured
- Downstream consumer compatibility beyond the repository's own test suite — not exercised

Neither is a gate item for this batch, so neither blocked it. They are reported rather
than omitted so the reader can see the edge of what was checked.

## Gate results

Batch gate, per issue: all items CONFIRMED → each PR a merge candidate.

Merge gate, per PR:

```text
Layer 1 — technical gates:  all CONFIRMED  → MERGE CANDIDATE
Layer 2 — human approval:   EXPLICIT HUMAN APPROVAL recorded
Verdict:                    MERGED, post-merge verification passed
```

Passing layer 1 produced a candidate only. Every merge required the layer 2 approval.

---

## What a blocked report looks like

The same structure, with the verdict and the blocking reason stated plainly. Blocked is
a normal, correct outcome — not a failure of the report.

```text
PR:                 #125
Verification SHA:   ef56789
CI:                 success, CI HEAD == PR HEAD (ef56789)
Mergeability:       clean, no conflicts
Required approvals: 1
Effective human approvals: 0
Bot reviews excluded: 1

Verdict: MERGE BLOCKED
  - Requires 1 effective human approval on ef56789, has 0
```

Note what this report does **not** do: it does not describe the PR as ready, safe, or
merely awaiting a formality. Zero human approvals means blocked, and the passing CI does
not soften that.
