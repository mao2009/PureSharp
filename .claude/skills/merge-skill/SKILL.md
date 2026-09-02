# Merge Skill for PureSharp Safe Integration

## Overview

The Merge Skill safely integrates changes into the main branch through comprehensive Git state verification, CI/test validation, and multi-gate merge protection. It:

1. Verifies branch and commit state
2. Validates working tree cleanliness
3. Confirms build and test success
4. Checks CI pipeline completion
5. Validates PR status and approvals
6. Enforces merge gates
7. Executes merge with post-verification

## Canonical Merge Boundary

Every document, script, and workflow in this repository follows exactly this sequence:

```text
VERIFY
    ↓
ALL REQUIRED GATES PASS
    ↓
MERGE CANDIDATE
    ↓
HUMAN REVIEW
    ↓
EXPLICIT HUMAN APPROVAL
    ↓
MERGE EXECUTION
    ↓
POST-MERGE VERIFY
```

Passing every technical gate produces a **MERGE CANDIDATE**, never a merge.
The transition from MERGE CANDIDATE to MERGE EXECUTION is gated solely by an
explicit human approval decision. No script in this skill performs a merge.

## Core Responsibilities

### Git State Verification

**Branch Verification**
- Current branch name
- Branch tracking status (local vs remote)
- Branch creation date and last commit
- Branch permissions and access

**Commit Verification**
- Base commit (target branch HEAD)
- Result commit (feature branch HEAD)
- Commit ancestry (fast-forward eligible)
- Commit authorship and dates
- Signed commits (if required)

**Working Tree Verification**
- No uncommitted changes
- No untracked files (except .gitignore)
- No merge conflicts
- No rebase in progress
- All stashed changes accounted for

### Build & Test Verification

**Build Verification**
```
dotnet build --configuration Release
```
- Full clean build succeeds
- No warnings treated as errors
- Package creation succeeds
- All artifacts generated

**Test Verification**
- Unit tests pass (PureSharp.Analyzers.Tests)
- Analyzer tests (all diagnostic codes)
- FluentIf tests
- LVP tests
- RT tests
- Integration tests

**Test Coverage**
- Changes covered by regression tests
- No test removal without justification
- Test output captured for reporting

### CI/CD Pipeline Verification

**GitHub Actions Verification**
- CI workflow triggered
- Workflow completed (not pending)
- Workflow status: SUCCESS or FAILURE
- All jobs passed
- Build matrix validated (if multi-platform)
- Artifact creation confirmed
- No skipped critical jobs

**CI Timeout Handling**
- Max wait time: 30 minutes (configurable)
- Status check interval: 30 seconds
- Timeout behavior: FAIL (don't merge unverified)

### PR Verification

**PR Metadata**
- PR number
- PR state (OPEN, MERGED, CLOSED)
- PR title and description
- PR branch and target branch
- PR creation date

**PR Approval** (see "Approval Semantics" below)
- Effective approvals, bound to the current PR HEAD
- Required approvals, read from `.kiro/merge.config.json`
- Change requests resolved

**PR Conflicts**
- No merge conflicts
- Mergeable state confirmed

Note: auto-merge is not a gate and is never relied upon. `.kiro/merge.config.json`
sets `merge.enableAutoMerge: false`, and this skill never enables it. GitHub reporting
`mergeable = true` is a statement about conflicts, not an authorization to merge.

### Approval Semantics

`verify-ci-status.ps1` computes **effective human approvals** as follows (fail-closed):

- Only `APPROVED` / `CHANGES_REQUESTED` / `DISMISSED` change a reviewer's state.
  `COMMENTED` and `PENDING` reviews are ignored.
- Each reviewer contributes **at most one** state: their latest relevant review.
  A reviewer who approved and later requested changes does **not** count as an approval.
- Bot reviews never count (GitHub `__typename = Bot`, a `[bot]` login suffix, or a
  known bot login such as `coderabbitai`).
- An `APPROVED` review counts **only** when its reviewed commit OID equals the current
  PR HEAD. Approvals of earlier commits are stale and do not count.
- If review authorship, timestamp, or commit OID cannot be determined, the review is
  reported UNVERIFIED and the gate is **BLOCKED**. Approval is never inferred.

Configuration keys in `.kiro/merge.config.json` → `approval`:

| Key | Phase 1 status |
|-----|----------------|
| `requiredApprovals` | **ENFORCED** — read from config; no hardcoded fallback. Must be an integer ≥ 1 or the run is a CONFIG ERROR. |
| `requirePRApproval` | **ENFORCED** — must be `true`. `false` is NOT SUPPORTED and blocks the run; human approval cannot be disabled. |
| `requireCodeOwnerApproval` | **NOT YET ENFORCED** (Phase 2) — must be `false`. Setting `true` blocks the run rather than reporting an unchecked gate as passed. |
| `dismissStaleReviews` | **IGNORED** — this skill is unconditionally stricter: approvals are always bound to the current PR HEAD regardless of this value. |

A configuration that cannot be loaded, parsed, or validated results in
**CONFIG ERROR → MERGE BLOCKED** with a non-zero exit code. There is no fallback default.

### Merge Gate Enforcement

Gates are split into two distinct layers. Passing layer 1 does **not** authorize a merge.

#### Layer 1 — Technical Gates (automated)

**TECHNICAL GATES PASSED** requires ALL of:

```
✓ Branch verified (current, tracked correctly)
✓ Base commit confirmed (git rev-parse origin/main)
✓ Result commit confirmed (git rev-parse HEAD)
✓ Working tree clean (git status --porcelain empty)
✓ Build succeeded (dotnet build exit 0)
✓ Relevant tests passed (test runner exit 0)
✓ CI completed successfully at the current PR HEAD (CI headSha == PR headRefOid)
✓ PR exists and mergeable (gh pr view --json mergeStateStatus)
✓ No file conflicts with concurrent changes (CONFIRMED only)
✓ Target branch HEAD unchanged since PR creation
✓ Effective approvals >= requiredApprovals (from config, HEAD-bound, bots excluded)
```

Result of layer 1 passing: the PR becomes a **MERGE CANDIDATE**. Nothing is merged.

#### Layer 2 — Human Approval Gate (manual, non-automatable)

```
✓ A human reviewed the verification report
✓ A human gave EXPLICIT approval to merge this specific HEAD
```

**MERGE GATE PASSED** = Layer 1 PASSED **and** Layer 2 PASSED.

**MERGE GATE BLOCKED** if ANY of:
- Unverified state (UNCONFIRMED)
- Failed check (test failure, build error)
- Pending CI (workflow in progress)
- CI evidence not bound to the current PR HEAD (stale CI)
- Insufficient effective approvals, or an active CHANGES_REQUESTED
- Merge conflicts
- Base branch changed requiring rebase
- Branch protection rule violation
- **No explicit human approval recorded** (layer 2 not satisfied)

### Merge Execution

Preconditions, in order. Every one must hold before any command below is run:

1. **Human approval confirmed** — a human has reviewed the verification report and
   explicitly approved merging this specific HEAD. This is the first and
   non-negotiable precondition.
2. Layer 1 technical gates all PASSED for that same HEAD.
3. PR HEAD unchanged since the approval was given.

Then:

1. Fetch latest from remote
2. Verify base commit unchanged
3. Merge with strategy:
   - `--ff-only` (fast-forward only, or fail)
   - `--squash` (if configured)
   - `--no-ff` (if branch protection requires)
4. Verify merge success
5. Push to remote
6. Verify remote state

### Post-Merge Verification

After merge:
1. Verify main branch updated
2. Verify PR marked merged
3. Verify branch cleanup policy
4. Run smoke tests on main
5. Verify CI triggered on main

## Usage

### Claude Code Skill Invocation

The Merge Skill is invoked as a Claude Code skill for safe merge integration.

**Verify merge gates** (VERIFICATION ONLY - no changes made):
```
/merge-skill verify --pr 123 --details
```

Performs all merge gate verifications:
- Git state verification
- Build verification
- Test verification
- CI status verification
- PR verification

Outputs: Human-readable report with CONFIRMED/INFERRED/UNVERIFIED evidence.

**Gate Status**: Reports whether merge is SAFE or BLOCKED.

### Authority & Safety

**Default Mode**: Verification only
- No merge executed
- No branches modified
- No pushes made
- Safe for any user to run

**Merge Execution**: Requires explicit human approval
- User must review verification report
- User must make merge decision
- Merge is executed by user with proper permissions
- Post-merge verification runs automatically

### Typical Workflow

1. **VERIFY** (automated):
   ```
   /merge-skill verify --pr 123
   ```
   → Review report, check CONFIRMED items

2. **ALL REQUIRED GATES PASS** (automated, layer 1)
   → If any technical gate fails: MERGE BLOCKED. Fix and return to step 1.

3. **MERGE CANDIDATE**
   → The PR is a candidate only. Nothing has been merged.

4. **HUMAN REVIEW** (manual):
   - Examine every CONFIRMED item
   - Confirm there are no INFERRED or UNVERIFIED items in the gate set

5. **EXPLICIT HUMAN APPROVAL** (manual, required):
   → A human states the decision to merge this specific HEAD.
   → Without this step, do not proceed. There is no automated substitute.

6. **MERGE EXECUTION** (manual, only after step 5):
   ```
   git checkout main
   git merge --ff-only feature/issue-NNN
   git push origin main
   ```
   → User manually executes merge with full control

7. **POST-MERGE VERIFY** (automated):
   ```
   /merge-skill verify --pr 123 --post-merge
   ```
   → Verify main branch updated and CI triggered

## Evidence Classification in Merge Context

### CONFIRMED (Must verify before merge)

- `git status` output (no uncommitted changes)
- `git rev-parse HEAD` (exact commit hash)
- `git rev-parse origin/main` (base commit hash)
- `dotnet build` exit code (0 = success)
- `dotnet test` exit code
- `gh run view --json status` (completed status)
- `gh pr view --json mergeStateStatus` (MERGEABLE or BLOCKED)
- `git log --oneline` (commit history visible)

Examples of what to CONFIRM:
```
CONFIRMED: git status returned "On branch feature/issue-18"
CONFIRMED: Build succeeded with exit code 0
CONFIRMED: CI workflow completed with status "success"
CONFIRMED: PR #123 mergeable state is "MERGEABLE"
CONFIRMED: 24 tests passed, 0 failed
```

### INFERRED (Must still verify)

- "Build passed so no syntax errors" (reasonable, but verify with test)
- "No conflicts likely since last rebase was clean" (still check)
- "Parallel changes won't conflict" (still check CI)

**Never INFER merge status from these**:
- Time since last check (CI may have failed after)
- Developer confidence (need actual verification)
- Past success history (current state matters)

### UNVERIFIED (Do not merge)

- "CI is probably running" → Must see actual status
- "Tests should pass" → Must run tests
- "I think merge succeeded" → Must verify branch state
- "Branch looks clean" → Must run git status

**Merge gate blocks on ANY UNVERIFIED item**.

## Git State Report Format

```
# Git State

## Branch Information
- Current Branch: feature/issue-18
- Tracking: origin/feature/issue-18
- Branch Created: 2026-08-26T12:00:00Z
- Last Commit: abc1234 (2 hours ago)

## Commits
- Base Commit: main = def5678
- Result Commit: HEAD = abc1234
- Ancestry: abc1234 is 5 commits ahead of main

## Working Tree
- Status: clean
- Uncommitted: 0
- Untracked: 0
- Conflicts: 0

## Remote
- Reachable: yes
- Last Fetch: 5 minutes ago
```

## Build & Test Report

```
# Build & Test Verification

## Build Status
- Configuration: Release
- Status: SUCCESS
- Duration: 2m 15s
- Output: [logs]

## Unit Tests
- PureSharp.Analyzers.Tests: 87 passed, 0 failed
- Duration: 1m 32s

## Analyzer Tests
- RT (Referential Transparency): 24 passed
- LVP (Local Variable Purity): 18 passed
- FIF (FluentIf): 12 passed

## Coverage
- Modified Files: 3
- Affected Code: 85% coverage
```

## CI Report

```
# CI/CD Pipeline Status

## Workflow: CI & NuGet Upload
- Run ID: 123456789
- Status: SUCCESS
- Triggered: By push to feature/issue-18
- Duration: 4m 30s
- Completed: 2026-08-26T14:30:00Z

## Jobs
- build-test: SUCCESS (build, test, pack steps)

## Artifacts
- NuGet Package: PureSharp.Core.0.1.7.nupkg (verified)

## Checks Passed
- ✓ Build (ubuntu-latest)
- ✓ Tests (87 passed)
- ✓ Package creation
```

## PR Report

```
# Pull Request Status

## PR Information
- Number: #123
- Title: [Issue #18] Implement batch and merge skills
- State: OPEN
- Branch: feature/issue-18 → main

## Merge Status
- Mergeable: YES
- Merge State Status: MERGEABLE
- Conflicts: 0
- Draft: NO

## Approvals
- Required Approvals: 1 (source: .kiro/merge.config.json)
- Effective Approvals: 1 (human, bound to HEAD abc1234)
- Stale Approvals Ignored: 0
- Bot Reviews Excluded: 1 (coderabbitai)
- Change Requests: 0
- Dismissed Reviews: 0

## Commits
- Commit Count: 5
- Latest Commit: abc1234
```

## Merge Gate Checklist

```
# Merge Gate Verification

## Git State
- [CONFIRMED] Branch: feature/issue-18
- [CONFIRMED] Base Commit: def5678 (main)
- [CONFIRMED] Result Commit: abc1234
- [CONFIRMED] Working Tree: clean
- [CONFIRMED] Upstream: synchronized

## Build
- [CONFIRMED] Build: SUCCESS
- [CONFIRMED] No warnings as errors
- [CONFIRMED] Artifacts: created

## Tests
- [CONFIRMED] Unit Tests: 87/87 passed
- [CONFIRMED] Analyzer Tests: 54/54 passed
- [CONFIRMED] Integration Tests: 12/12 passed

## CI
- [CONFIRMED] GitHub Actions: completed
- [CONFIRMED] Status: SUCCESS
- [CONFIRMED] All jobs passed

## PR
- [CONFIRMED] PR #123: exists
- [CONFIRMED] Mergeable: YES
- [CONFIRMED] Effective approvals: 1/1 (HEAD-bound, bots excluded)

## Result (Layer 1)
### TECHNICAL GATES PASSED ✓ → MERGE CANDIDATE

All automated verification gates confirmed for HEAD abc1234.
NOT merged. NOT authorized to merge.

## Result (Layer 2)
### AWAITING EXPLICIT HUMAN APPROVAL

Next: HUMAN REVIEW → EXPLICIT HUMAN APPROVAL → MERGE EXECUTION → POST-MERGE VERIFY
```

## Configuration

`.kiro/merge.config.json` is the single source of truth for the approval gate.
The scripts read it directly; nothing is hardcoded. Relevant excerpt:

```json
{
  "merge": {
    "baseBranch": "main",
    "requireFastForward": false,
    "requireSignedCommits": false,
    "enableAutoMerge": false
  },
  "approval": {
    "requirePRApproval": true,
    "requiredApprovals": 1,
    "requireCodeOwnerApproval": false,
    "dismissStaleReviews": false
  }
}
```

Changing `approval.requiredApprovals` to `2` makes the script require two effective
human approvals. See "Approval Semantics" above for which keys are ENFORCED,
NOT YET ENFORCED, or IGNORED in Phase 1.

## Verification Checklist

Layer 1 — technical gates (automated):

- [ ] Git branch verified
- [ ] Base commit confirmed
- [ ] Result commit confirmed
- [ ] Working tree clean confirmed
- [ ] Build success confirmed
- [ ] Unit tests pass confirmed
- [ ] Analyzer tests pass confirmed
- [ ] Integration tests pass confirmed
- [ ] CI workflow completed at the current PR HEAD confirmed
- [ ] PR state verified
- [ ] PR mergeable confirmed
- [ ] Effective approvals >= requiredApprovals confirmed
- [ ] No file conflicts confirmed
- [ ] All technical gates passed confirmed → MERGE CANDIDATE

Layer 2 — human approval gate (manual):

- [ ] Human review of the verification report completed
- [ ] Explicit human approval recorded for this HEAD

Post-merge (automated, only after layers 1 and 2):

- [ ] Merge executed successfully
- [ ] Remote state synchronized
- [ ] Post-merge verification passed

## Failure Scenarios & Recovery

### Scenario: Build Failed

```
MERGE GATE BLOCKED: Build failed

Diagnosis:
- dotnet build exit code: 1
- Error: CS0103 - Name 'MyVar' does not exist

Recovery:
1. Fix code on feature branch
2. Push fixes
3. Wait for CI retry
4. Re-run merge-verify

Do not merge until build succeeds.
```

### Scenario: CI Timeout

```
MERGE GATE BLOCKED: CI not completed

Diagnosis:
- GitHub Actions workflow running for 45 minutes
- Status: in_progress
- Timeout configured: 30 minutes

Recovery:
1. Check workflow logs for blockage
2. If stuck: restart workflow manually
3. If failed: fix issue and push
4. Re-run merge-verify

Do not merge with pending CI.
```

### Scenario: Merge Conflict

```
MERGE GATE BLOCKED: Merge conflict detected

Diagnosis:
- Main branch changed since PR created
- Auto-merge returned conflicted state

Recovery:
1. Fetch latest main
2. Rebase feature/issue-18 onto main
3. Resolve conflicts locally
4. Push rebased branch (force-push if needed)
5. Wait for CI retry
6. Re-run merge-verify

Do not merge with conflicts.
```

## Related Skills

- **Batch Skill**: Orchestrate multiple issues with proper merge gates
- **Code Review**: Automated review gates before merge
- **Kiro Spec Skills**: Individual issue specification

## See Also

- Issue #18: [Engineering] Introduce Batch and Merge Skills
- Issue #6: [Roadmap] PureSharp v1.0.0 roadmap
- `.kiro/steering/`: Project guidance documents
