# PureSharp Batch & Merge Skills

Two skills coordinate multi-issue development and safe integration for the PureSharp
v1.0.0 roadmap.

| Skill | Specification | Responsibility |
|---|---|---|
| **Batch Skill** | `batch-skill/SKILL.md` | Orchestrate multiple issues: dependencies, parallel safety, execution graph, aggregation |
| **Merge Skill** | `merge-skill/SKILL.md` | Verify a PR against merge gates, enforce the human approval boundary, verify after merge |

## Scriptless by design

Each `SKILL.md` is an **executable specification and the single source of truth** for its
skill. There are no helper scripts, and none may be added as a requirement.

This is deliberate. The skills define **which facts must be established**, not **which
commands to run** — so they carry no dependency on PowerShell version, shell dialect,
path separator, file encoding, exit-code convention, or any one CLI being installed.

They run unchanged on Windows, Linux, macOS, and WSL. No PowerShell, Bash, Python, or
Node runtime is required.

Establish each fact with whichever trusted source the environment offers — a GitHub
connector, `gh`, the GitHub API, `git` for local facts, or the repository's own build and
test tooling. If **no** available source can establish a required fact, the result is
**UNVERIFIED**, and UNVERIFIED blocks. Skipping a check because a tool is missing is
never acceptable.

Porting the removed helper scripts into another language would reintroduce exactly the
coupling this design removes, and is not a valid change.

## Using the skills

Invoke a skill and follow its specification. The Merge Skill's default mode is
verification only: it reads state, changes nothing, and is safe to run at any time.

```text
Batch Skill   → analyse issues, decide execution order, run, aggregate, report
Merge Skill   → verify a PR against the gates, report MERGE CANDIDATE or MERGE BLOCKED
```

Neither skill merges anything on its own. See `WORKFLOW.md` for the end-to-end flow.

## Safety principles

These are fixed policy. No configuration, flag, or argument relaxes them.

1. **Fail closed.** Any error, ambiguity, or missing evidence blocks.
2. **Explicit human approval before every merge.** Technical verification success is not
   permission to merge. The approval comes from one of exactly two sources — a
   third-party GitHub `APPROVED` review, or, for a self-authored PR in a repository that
   requires no third-party review, an explicit out-of-band approval bound to the exact
   HEAD SHA. **Repository policy always wins**: where the repository requires approving
   reviews, nothing here bypasses it. A self-authored PR is never recorded as carrying
   its author's GitHub approval.
3. **Evidence is bound to an exact commit.** Stale evidence never satisfies a gate; a new
   push invalidates all prior evidence, approvals included.
4. **UNVERIFIED blocks. INFERRED never opens a gate.**
5. **Parallel execution requires proof of safety**, not merely the absence of known
   danger. Serial is the safe default.
6. **Auto-merge is never enabled**; the base branch is never force-pushed or pushed to
   directly.

## Evidence classification

Both skills use these three labels and no others.

| Label | Meaning | May open a gate? |
|---|---|---|
| **CONFIRMED** | Actually observed from a trusted source this run | Yes |
| **INFERRED** | Reasonable conclusion from confirmed facts, not itself observed | No — may only support a decision that fails safe |
| **UNVERIFIED** | Not established, including "the tool was unavailable" | No — blocks |

**Golden rule**: never claim success for something you have not verified.

## Configuration

Both configuration files carry **data only**. Behaviour lives in the `SKILL.md` files.

| File | Keys |
|---|---|
| `.kiro/merge.config.json` | `requiredApprovals`, `mergeMethod`, `deleteBranchAfterMerge` |
| `.kiro/batch.config.json` | `maxParallelTasks` |

A missing file, malformed JSON, an unknown key, or a value out of range is a **CONFIG
ERROR** and blocks. Safety policy is deliberately absent from both schemas so it cannot
be switched off, and configuration can only make a gate stricter, never weaker.

## Files

```text
.claude/skills/
├── README.md              (this file — overview, invocation, safety principles)
├── WORKFLOW.md            (end-to-end workflow: batch → PR → review → merge)
├── EXAMPLE-REPORT.md      (reporting example)
├── batch-skill/
│   └── SKILL.md           (batch specification — SSOT)
└── merge-skill/
    └── SKILL.md           (merge specification — SSOT)

.kiro/
├── batch.config.json
└── merge.config.json
```

## Project context

The skills support the PureSharp v1.0.0 roadmap tracked in **Issue #6**. The roadmap's
phases, issue membership, and target dates live in the GitHub issues — deliberately not
duplicated here, where a second copy would silently go stale.

## See also

- `WORKFLOW.md` — end-to-end workflow
- `EXAMPLE-REPORT.md` — reporting example
- `batch-skill/SKILL.md`, `merge-skill/SKILL.md` — the specifications themselves
- Issue #18 — Batch and Merge Skills
- Issue #6 — PureSharp v1.0.0 roadmap
- `.kiro/steering/` — project guidance documents
