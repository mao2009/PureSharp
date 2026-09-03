---
name: batch-skill
description: Orchestrates multiple PureSharp GitHub issues as a batch. Defines dependency analysis, the CONFIRMED-only evidence bar for parallel execution, execution graph construction, result aggregation, and batch reporting. Use when planning or running work across several issues.
---

# Batch Skill for PureSharp Issue Orchestration

## Status of this document

**This document is the executable specification and the single source of truth (SSOT)
for batch orchestration in this repository.** There is no helper script. Behaviour is
defined here, not in any `.ps1`, `.sh`, `.py`, or `.js` file, and none may be introduced
as a requirement of this skill.

The skill defines **which facts must be established**, not **which commands to run**.

## Design Principles

1. **SKILL.md is the SSOT.**
2. **No required environment-dependent script.** Porting logic from one scripting
   language to another is not a valid response to this rule.
3. **Facts, not commands.**
4. **Any trusted source is acceptable** — see the Merge Skill's "Selecting a trusted
   source", which applies identically here.
5. **Unavailable evidence is UNVERIFIED.** Never skip a check because a tool is missing.
6. **Parallel execution requires proof of safety, not absence of evidence of danger.**
7. **Serial execution is the safe default.** When in doubt, run serially.

## Fixed Safety Policy (not configurable)

- Parallel execution requires CONFIRMED evidence on every safety dimension.
- INFERRED safety → serial execution.
- UNVERIFIED safety → serial execution, or BLOCKED where the unknown affects correctness.
- A batch never merges anything. Merging is the Merge Skill's responsibility and always
  requires explicit human approval.
- Fail closed: any error, ambiguity, or missing evidence degrades toward serial or blocked.

## Evidence Classification

Identical to the Merge Skill.

| Label | Meaning |
|---|---|
| **CONFIRMED** | Actually observed from a trusted source during this run. |
| **INFERRED** | A reasonable conclusion from CONFIRMED facts, not itself observed. |
| **UNVERIFIED** | Not established, including "the tool was unavailable". |

- Gate items require **CONFIRMED**.
- **INFERRED** may support a decision that fails safe — choosing serial execution is such
  a decision. It may never open a gate or authorise parallelism.
- **UNVERIFIED** must be reported, never omitted.

## Batch Workflow

1. **Retrieve the issues.** Obtain the batch's issues from a trusted GitHub source.
2. **Confirm current state.** For each issue: open or closed, assignee, labels, linked
   PRs. Work from live state, never from a cached or remembered list.
3. **Read the requirements.** Read each issue body and its acceptance criteria.
4. **Identify dependencies.** Establish blocking / blocked-by relationships from issue
   metadata and issue content — not from issue-number ordering.
5. **Investigate affected areas.** For each issue, determine the code, configuration,
   and public contracts it will touch.
6. **Classify the evidence.** Label every dependency and affected-area finding
   CONFIRMED, INFERRED, or UNVERIFIED.
7. **Decide parallel safety.** Apply the checklist below, pairwise.
8. **Build the execution graph.** Resolve the dependency DAG into ordered layers, with
   parallel groups only where safety is CONFIRMED.
9. **Execute the batch**, honouring the graph.
10. **Aggregate results** per issue.
11. **Report**, in the two-layer format.

## Parallel Safety

Two issues may run in parallel **only when every dimension below is CONFIRMED for that
specific pair**:

- [ ] Dependency graph resolved — every edge between them determined, none assumed
- [ ] No execution-order dependency
- [ ] No shared mutable configuration
- [ ] No shared public contract change
- [ ] No architectural coupling that requires ordering
- [ ] No conflicting change area
- [ ] No release or version coordination conflict

**File non-overlap alone is never sufficient.** Two issues touching disjoint files can
still conflict through shared configuration, a shared public contract, architectural
coupling, or a required execution order. "Different analyzers" and "different files" are
starting observations, not conclusions.

Decision table:

| Evidence level on any dimension | Execution |
|---|---|
| All dimensions CONFIRMED | **PARALLEL permitted** |
| Any dimension INFERRED | **SERIAL** |
| Any dimension UNVERIFIED | **SERIAL**, or **BLOCKED** if the unknown affects correctness |

**If safety cannot be proven, do not choose parallel execution.** The absence of a known
conflict is not evidence of independence. Never promote INFERRED to CONFIRMED because a
conflict seems unlikely.

Configuration may lower the parallel limit but can never lower this evidence bar.

## Result Collection

Track, per issue:

- Branch and the commits produced
- Build result
- Test result
- PR identity and state
- CI result, bound to the PR HEAD

Classify each issue's outcome as exactly one of:

| Status | Meaning |
|---|---|
| **Success** | All required checks CONFIRMED passing |
| **Blocked** | A dependency did not complete, or a gate blocked it |
| **Failed** | Implementation or verification failed |
| **Pending** | Awaiting human approval or an external result |
| **Unexecuted** | Skipped — conflict risk, or an unresolved dependency |

## Batch Gate

A batch may advance to the merge phase only when, for **every** issue:

- [ ] Branch exists and its working tree is clean (CONFIRMED)
- [ ] Base commit identified (CONFIRMED)
- [ ] Result commit identified (CONFIRMED)
- [ ] Build succeeded (CONFIRMED)
- [ ] Relevant tests passed (CONFIRMED)
- [ ] CI passed for the PR HEAD (CONFIRMED)
- [ ] PR exists and is mergeable (CONFIRMED)
- [ ] No file conflicts with other issues in the batch (CONFIRMED)

Conflict evidence bar:

| Conflict evidence | Batch gate |
|---|---|
| CONFIRMED | may pass |
| INFERRED | **BATCH GATE BLOCKED** |
| UNVERIFIED | **BATCH GATE BLOCKED** |

Passing the batch gate makes each PR a **merge candidate** and nothing more. Every merge
still runs through the Merge Skill, including its explicit human approval step. The batch
gate never authorises a merge.

## Reporting

Two layers, both required.

**Human report** — what was executed, success and failure breakdown, critical blockers,
recommended next steps.

**AI report** — evidence classification per finding, per-issue git state, verification
results, dependency resolution status, and the parallel-safety decision with the evidence
that drove it.

Report the parallel-safety decision explicitly, including which dimensions were
CONFIRMED and which forced serial execution. A batch that ran serially because safety
could not be proven is a correct outcome and should be reported as such, not as a
shortfall.

See `../EXAMPLE-REPORT.md`.

## Configuration

`.kiro/batch.config.json` carries **data only**. All behaviour is defined here.

| Key | Type | Meaning |
|---|---|---|
| `maxParallelTasks` | integer ≥ 1 | Upper bound on concurrent tasks. `1` forces fully serial execution. |

Rules:

- Missing file, malformed JSON, an unknown key, a key of the wrong type, or
  `maxParallelTasks < 1` → **CONFIG ERROR → BATCH BLOCKED**.
- `maxParallelTasks` is a **ceiling, not a licence**. It never authorises parallelism the
  evidence bar has not already permitted. Raising it cannot make an INFERRED or
  UNVERIFIED pair parallel-eligible.
- Safety policy is deliberately absent from the schema so it cannot be switched off.
- The v1.0 roadmap, its phases, and its target dates live in the GitHub issues
  (Issue #6), not in this configuration. Do not duplicate them here — a second copy
  silently goes stale.

## Related

- `../merge-skill/SKILL.md` — merge gates, approval semantics, human approval boundary
- `../WORKFLOW.md` — end-to-end workflow
- `../EXAMPLE-REPORT.md` — reporting example
- Issue #18 — Batch and Merge Skills
- Issue #6 — PureSharp v1.0.0 roadmap
