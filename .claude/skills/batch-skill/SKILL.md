# Batch Skill for PureSharp Issue Orchestration

## Overview

The Batch Skill orchestrates safe, parallel execution of multiple GitHub Issues for PureSharp development. It:

1. Selects executable issues from the roadmap
2. Analyzes issue dependencies
3. Classifies issues for parallel execution
4. Determines safe execution order
5. Collects results across multiple issue implementations
6. Generates structured reports with evidence classification

## Core Responsibilities

### Issue Selection & Dependency Analysis

- Retrieve open issues from PureSharp roadmap (#6-#18)
- Parse issue relationships (parent, blocking, blocked-by, sub-issues)
- Identify issue categories:
  - **Independent**: No blocking dependencies
  - **Dependent**: Blocked by other issues
  - **Blocking**: Blocks other issues
- Extract acceptance criteria and verification needs from issue body

### Parallel Execution Classification

**Issues CANNOT run in parallel if:**
- They modify the same file/directory (confirmed)
- They modify the same architectural component (confirmed)
- Dependency chain exists (confirmed)
- API change + consumer change (confirmed)
- Release/version issues (confirmed)
- Conflict status is UNVERIFIED

**Issues CAN run in parallel if:**
- Independent spec implementations (CONFIRMED: no file overlap)
- Different diagnostic analyzers (CONFIRMED: different files)
- Different documentation sections (CONFIRMED: different files)
- Different test categories (CONFIRMED: different files)

**CRITICAL: Parallel Safety Must Be CONFIRMED**

Do not assume parallel execution is safe based on analysis alone.

Evidence Levels:

- **CONFIRMED**: File-by-file analysis shows no conflicts
- **INFERRED**: Design suggests no conflicts (not sufficient for parallel)
- **UNVERIFIED**: Cannot determine conflict status

Parallel execution requires CONFIRMED evidence.
If conflict status is UNVERIFIED or INFERRED, use SERIAL EXECUTION.

### Execution Order Determination

1. Build dependency graph (DAG)
2. Topological sort to find execution layers
3. Identify parallel batches within each layer
4. Flag issues requiring human review/approval

### Result Collection & Verification

- Track per-issue:
  - Git branch and commits
  - Build status
  - Test results
  - PR status
  - CI results
- Distinguish:
  - **Success**: All checks passing
  - **Blocked**: Dependency failure
  - **Failed**: Implementation or verification failure
  - **Pending**: Awaiting human approval
  - **Unexecuted**: Skipped due to conflict

### Report Generation

Generate structured reports in two layers:

**Human Report** (Executive Summary)
- What was executed
- Success/failure breakdown
- Critical blockers
- Recommended next steps

**AI Report** (Technical Details)
- Evidence classification (CONFIRMED/INFERRED/UNVERIFIED)
- Git state per issue
- Verification results
- Dependency resolution status

## Usage

### Typical Workflow

### Claude Code Skill Invocation

The Batch Skill is invoked as a Claude Code skill (not Kiro-specific).

**Analyze dependencies and plan batch execution**:
```
/batch-skill analyze --issues 7,8,9,10,11,12
```

Provides:
- Dependency graph analysis
- Parallel execution classification
- Execution order recommendation
- Blocking relationship summary

**Integration workflow** (standard Kiro + Batch Skill):
1. Batch Skill provides planning (analyze command)
2. Kiro spec system handles implementation (`/kiro-spec-quick`)
3. Merge Skill handles integration (separate skill)

## Dependency Graph Example (Illustrative)

**Note**: The following example is illustrative. Actual PureSharp v1.0 issues may have different dependency structures. Always verify dependencies via GitHub issue metadata before execution.

```
Example Sequential Dependencies:
Issue #7: Diagnostic SSOT
    ↓ (blocks)
Issue #8: Regression Tests
    ↓ (blocks)
Issue #9: RT Semantics
    ↓ (blocks)
Issue #10: Interprocedural Analysis

Example Independent Issues:
Issue #11: LVP (independent from #7-#10)
Issue #12: FluentIf (independent from #7-#10)

Example Execution Plan:
Parallel Batch Layer 1: [#7]
Parallel Batch Layer 2: [#8, #11]
Parallel Batch Layer 3: [#9, #12]
Parallel Batch Layer 4: [#10]
```

### Actual PureSharp v1.0 Structure

In PureSharp v1.0 roadmap:
- All issues #7-#18 have parent issue #6 (the roadmap)
- Actual dependencies may be different from sequential examples above
- Always analyze actual GitHub issue metadata for real dependency structure

## Issue Metadata Format

Each issue should include:

```markdown
## Blocking Issues
- #X, #Y (must complete first)

## Blocked By
- #A, #B (blocks these from parallel execution)

## Modified Files
- /src/path/to/component
- /tests/path/to/tests

## Verification Gates
- [ ] Build succeeds
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] CI passes
- [ ] PR approval
- [ ] Documentation complete
```

## Git State Verification

Before batch execution, verify:

- Current branch (must be clean or on feature branch)
- Base branch (usually `main`)
- No uncommitted changes
- Remote is reachable

During batch execution:

- Per-issue branch management
- Commit tracking
- PR creation/status
- CI result polling

After batch execution:

- Branch cleanup
- Merge state verification
- Target branch verification

## Evidence Classification

### CONFIRMED
- Facts verified via Git, CLI, API, or test results
- Examples:
  - `git status` returns clean
  - `dotnet test` returns all tests pass
  - GitHub API confirms PR merge status
  - CI workflow completed with status

### INFERRED
- Reasonable conclusion from confirmed evidence
- Examples:
  - "Build passed because test suite passed" (dependency inference)
  - "No file conflicts likely" (from dependency analysis)
  - "Parallel execution safe" (from architectural analysis)

### UNVERIFIED
- Information not yet checked
- Examples:
  - "CI in progress"
  - "PR review status unknown"
  - "Remote branch state uncertain"

**NEVER**:
- Assume success
- Infer merge status without checking
- Skip verification steps
- Claim CONFIRMED for unverified state

## Merge Gate Requirements

Batch can only proceed to merge phase when:

For each issue:
- [ ] Branch exists and is clean
- [ ] Base commit identified (CONFIRMED)
- [ ] Result commit identified (CONFIRMED)
- [ ] Build succeeded (CONFIRMED)
- [ ] Relevant tests passed (CONFIRMED)
- [ ] CI passed (CONFIRMED)
- [ ] PR exists and is mergeable (CONFIRMED)
- [ ] No uncommitted changes (CONFIRMED)
- [ ] No file conflicts with other batch issues (CONFIRMED or analysis-based)

If any check is UNVERIFIED or BLOCKED, report as **BATCH GATE BLOCKED**.

## Configuration

Create `.kiro/batch.config.json`:

```json
{
  "defaultBatchName": "v1.0-batch",
  "maxParallelTasks": 2,
  "ciTimeoutMinutes": 30,
  "verificationTimeout": 300,
  "reportFormat": "both",
  "requireHumanApproval": true,
  "failureMode": "stop",
  "gitCheckoutDepth": 1,
  "pruneLocalBranches": true
}
```

## Verification Checklist

- [ ] All issues analyzed for dependencies
- [ ] Parallel batches identified correctly
- [ ] No undetected file conflicts
- [ ] Dependency graph validated
- [ ] Git state verified for all branches
- [ ] Build succeeds for each issue
- [ ] Tests passing for each issue
- [ ] CI status checked for each issue
- [ ] PR status confirmed for each issue
- [ ] Final merge gates passed

## Limitations & Future Work

Current phase:
- GitHub issues API only (no other trackers)
- Manual merge phase (see Merge Skill)
- No automatic rebase/conflict resolution
- Requires pre-written specs/PRs

Future phases:
- Automatic issue -> PR -> merge workflow
- Conflict detection and resolution
- Cross-issue test suite coordination
- Performance/resource monitoring
- Advanced scheduling (priority, estimated time)

## Related Skills

- **Merge Skill**: Safe PR merging with full verification
- **Kiro Spec Skills**: Individual issue spec-driven development
- **Code Review**: Automated code review during batch execution

## See Also

- Issue #18: [Engineering] Introduce Batch and Merge Skills
- Issue #6: [Roadmap] PureSharp v1.0.0 roadmap
- `.kiro/steering/`: Project guidance documents
