# PureSharp Batch & Merge Workflow Guide

This guide describes the unified workflow for orchestrating multiple GitHub Issues and safely merging changes into PureSharp's main branch.

## Overview

The PureSharp development workflow consists of two coordinated skills:

1. **Batch Skill**: Orchestrates multiple related issues with dependency tracking
2. **Merge Skill**: Safely integrates changes with comprehensive verification

Together, they enable:
- Issue selection from roadmap
- Dependency analysis
- Parallel execution planning
- Result collection
- Safe merge integration
- Structured reporting

## Workflow Phases

### Phase 1: Issue Selection & Analysis

**Input**: Issue numbers (e.g., #7, #8, #9, #10)

```bash
# Analyze issues for batch execution
/kiro:batch-init "v1.0 Phase 1" --issues 7,8,9,10
```

**Output**: Dependency graph, parallel execution plan, blocking relationships

**Example Output**:
```
Issue Analysis Report
====================

Dependency Graph:
  Issue #7: Diagnostic SSOT
    ├─ blocks: #8, #9, #10
    └─ blocked-by: none

  Issue #8: Regression Tests
    ├─ blocks: #9
    └─ blocked-by: #7

  Issue #9: RT Semantics
    ├─ blocks: #10
    └─ blocked-by: #8

  Issue #10: Interprocedural Analysis
    ├─ blocks: none
    └─ blocked-by: #9

Execution Plan:
  Layer 1 (parallel): [#7]
  Layer 2 (parallel): [#8]
  Layer 3 (parallel): [#9]
  Layer 4 (parallel): [#10]

File Impact:
  #7: /src/Analyzer.cs, /src/DiagnosticCatalog.cs
  #8: /src/Tests/*, /src/Regression.cs
  #9: /src/SemanticAnalyzer.cs
  #10: /src/InterproceduralAnalyzer.cs

Conflict Analysis: NO DIRECT FILE CONFLICTS

Recommendation: SEQUENTIAL EXECUTION (dependencies)
```

### Phase 2: Issue Implementation

**For each issue in execution order**:

```bash
# Use Kiro spec workflow for individual issue
/kiro-spec-quick #7 --auto
/kiro-spec-quick #8 --auto
# etc.
```

**Per-issue workflow**:
1. Create feature branch: `feature/issue-NNN`
2. Implement per spec
3. Run local tests
4. Create PR
5. Wait for CI

### Phase 3: Merge Verification & Integration

**For each completed issue (or batch)**:

```bash
# Verify merge gates
/kiro:merge-init --pr 123
/kiro:merge-verify --details

# Execute merge (if gates pass)
/kiro:merge-execute
```

**Merge Gate Checklist** (automated verification):

- [x] Git branch verified
- [x] Base commit confirmed
- [x] Result commit confirmed
- [x] Working tree clean
- [x] Build success
- [x] Tests passed
- [x] CI completed
- [x] PR mergeable
- [x] No file conflicts
- [x] All gates passed

### Phase 4: Reporting

**After batch completion**:

```bash
# Generate final report
/kiro:batch-report --format both
```

**Human Report**: Executive summary for stakeholders

```
# v1.0 Phase 1 - Batch Execution Report

## Summary
- Issues Executed: 4 of 4
- Success Rate: 100%
- Duration: 2 hours 45 minutes
- Status: ✓ COMPLETED

## Issues Completed
✓ #7: Diagnostic SSOT (merged)
✓ #8: Regression Tests (merged)
✓ #9: RT Semantics (merged)
✓ #10: Interprocedural Analysis (merged)

## Changes
- 12 files modified
- 1,247 lines added
- 89 lines removed
- 3 new test files

## Verification
- Build: ✓ SUCCESS
- Tests: ✓ 234/234 PASSED
- CI: ✓ COMPLETED
- Code Review: ✓ APPROVED

## PRs Merged
- #121 (Issue #7)
- #122 (Issue #8)
- #123 (Issue #9)
- #124 (Issue #10)

## Next Steps
→ Proceed to Phase 2 (#11-#13)
```

**AI Report**: Detailed technical evidence

```
# Technical Verification Report

## Evidence Classification

### CONFIRMED (Verified via Git/CLI/API)
- Current branch: feature/issue-18
- Base commit: def5678 (origin/main)
- Result commit: abc1234
- Working tree: clean (0 changes, 0 untracked)
- Build exit code: 0 (success)
- Test exit code: 0 (234/234 passed)
- CI status: completed=success
- PR #123 merge state: MERGEABLE

### INFERRED (Reasonable conclusion from evidence)
- No conflicts likely (no file overlap between #7-#10)
- Parallel execution safe (independent components)
- Chain dependencies satisfied (all blockers completed)

### UNVERIFIED (Not yet checked)
- None (all critical items verified)

## Git State
- Current Branch: main
- Base Branch: origin/main
- Base Commit: def5678... (2026-08-26 14:30)
- Result Commit: abc1234... (2026-08-26 16:45)
- Commits Ahead: 4
- Working Tree: clean

## Build & Test Results
- Build Status: SUCCESS
- Build Duration: 2m 15s
- Unit Tests: 234 passed, 0 failed
- Analyzer Tests:
  - RT: 24 passed
  - LVP: 18 passed
  - FIF: 12 passed
- Coverage: 85% (modified code)

## CI/CD Pipeline
- Workflow: CI & NuGet Upload
- Status: completed
- Conclusion: success
- Duration: 4m 30s
- Jobs: all passed
- Artifacts: NuGet package created

## PR Status
- PR #123
- State: MERGED (2026-08-26 17:00)
- Approvals: 1/1
- Comments: 0 unresolved

## Merge Gate Status
```
✓ All Gates Passed - Safe to Merge
```
```

## Practical Examples

### Example 1: Execute Single Issue (Batch of 1)

```bash
# Issue #7 only
/kiro:batch-init "Issue #7" --issues 7

# Analyzes if #7 has blockers
# Output: No dependencies, can execute immediately

# Implementation
/kiro-spec-quick #7

# When PR #121 is ready:
/kiro:merge-init --pr 121
/kiro:merge-verify
/kiro:merge-execute
/kiro:merge-report --format human
```

### Example 2: Execute Multiple Independent Issues

```bash
# Issues #11 and #12 are independent
/kiro:batch-init "LVP & FluentIf" --issues 11,12

# Analyzes dependencies
# Output: Both are independent, can run in parallel

# Execute both (in parallel or sequentially)
/kiro-spec-quick #11
/kiro-spec-quick #12

# When both PRs ready:
/kiro:batch-verify

# Merge both (can merge independently)
/kiro:merge-init --pr 131
/kiro:merge-execute

/kiro:merge-init --pr 132
/kiro:merge-execute

# Final report
/kiro:batch-report --format both
```

### Example 3: Chain Dependencies

```bash
# Issues with dependencies
/kiro:batch-init "RT Chain" --issues 9,10

# Analyzes: #9 blocks #10
# Output: Sequential execution required

# Layer 1: Execute #9
/kiro-spec-quick #9

# Wait for #9 to merge
/kiro:merge-init --pr 129
/kiro:merge-execute

# Layer 2: Execute #10 (now can start since #9 merged)
/kiro-spec-quick #10

# Merge #10
/kiro:merge-init --pr 130
/kiro:merge-execute
```

## Failure Handling

### Build Failed

```
Failure: Build failed for Issue #8

Actions:
1. Fix build error on feature/issue-8
2. Push fix
3. Wait for CI retry
4. Re-run merge-verify
5. If CI passes, retry merge

Do not merge until build succeeds.
```

### Test Failed

```
Failure: 12 tests failed in RT analyzer

Actions:
1. Investigate test failures (output provided)
2. Fix implementation or test
3. Run tests locally
4. Push fix
5. Wait for CI
6. Re-run merge-verify
7. If all pass, retry merge

Do not merge until all tests pass.
```

### Merge Conflict

```
Failure: Cannot merge - conflicts detected

Diagnosis: main branch changed since PR created

Actions:
1. Fetch origin/main
2. Rebase feature/issue-8 on origin/main
3. Resolve conflicts locally
4. Test locally (dotnet build && dotnet test)
5. Force-push to feature/issue-8
6. Wait for CI retry
7. Re-run merge-verify
8. Retry merge

Do not merge with unresolved conflicts.
```

### CI Timeout

```
Failure: CI workflow running for 45+ minutes

Actions:
1. Check GitHub Actions logs
2. If hanging: Cancel and restart workflow
3. If failed: Fix issue and push
4. Wait for new CI run
5. Re-run merge-verify
6. Retry merge

Do not merge with pending CI.
```

## Configuration

### Batch Configuration

Create `.kiro/batch.config.json`:

```json
{
  "defaultBatchName": "v1.0-batch",
  "maxParallelTasks": 2,
  "ciTimeoutMinutes": 30,
  "requireHumanApproval": true,
  "failureMode": "stop",
  "gitCheckoutDepth": 1,
  "pruneLocalBranches": true
}
```

### Merge Configuration

Create `.kiro/merge.config.json`:

```json
{
  "baseBranch": "main",
  "requireFastForward": false,
  "requirePRApproval": true,
  "requiredApprovals": 1,
  "ciTimeoutMinutes": 30,
  "postMergeVerification": true,
  "autoCleanupBranch": true,
  "verificationReportFormat": "ai",
  "failureMode": "block"
}
```

## Verification Scripts

### Git State Verification

```bash
# Verify Git state (branch, commits, working tree, remote)
.\\.claude\skills\merge-skill\verify-git-state.ps1 -Format text

# Output: JSON report with all Git details
.\\.claude\skills\merge-skill\verify-git-state.ps1 -Format json
```

### Build & Test Verification

```bash
# Run build and tests locally
.\\.claude\skills\merge-skill\verify-build-and-tests.ps1 -Configuration Release

# Quick status check
.\\.claude\skills\merge-skill\verify-build-and-tests.ps1 -Format json
```

### CI Status Verification

```bash
# Check CI status for PR
.\\.claude\skills\merge-skill\verify-ci-status.ps1 -PR 123

# Wait for CI with timeout
.\\.claude\skills\merge-skill\verify-ci-status.ps1 -PR 123 -Wait -TimeoutMinutes 30
```

## Evidence Standards

### CONFIRMED Evidence Examples

```
✓ CONFIRMED: git status returned "On branch feature/issue-18"
✓ CONFIRMED: git rev-parse HEAD returned abc1234...
✓ CONFIRMED: dotnet build exited with code 0
✓ CONFIRMED: dotnet test exited with code 0 (234 tests passed)
✓ CONFIRMED: gh run view returned status "completed" with conclusion "success"
✓ CONFIRMED: gh pr view returned mergeStateStatus "MERGEABLE"
✓ CONFIRMED: git diff origin/main returned no conflicts
```

### INFERRED Evidence Examples

```
⊙ INFERRED: Build passed, so no syntax errors in changed files
⊙ INFERRED: No file conflicts in parallel issues #11 and #12
⊙ INFERRED: Chain #7→#8→#9→#10 satisfies all dependencies
⊙ INFERRED: CI will likely pass (same environment, similar changes)
```

### UNVERIFIED Evidence Examples

```
✗ UNVERIFIED: "PR is probably ready" → need to check PR state
✗ UNVERIFIED: "Tests should pass" → need to run tests
✗ UNVERIFIED: "CI is running" → need to confirm current status
✗ UNVERIFIED: "No conflicts" → need to check git diff
```

**Golden Rule**: Never claim success for something you haven't verified.

## Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| "Branch not found" | Remote branch deleted | Recreate from latest main |
| "Build failed" | Code error | Fix and push |
| "Tests failed" | Logic error or missing test | Fix and push |
| "CI pending" | GitHub Actions slow | Wait and re-check |
| "Merge conflict" | Main changed | Rebase feature branch |
| "PR not mergeable" | CI not passed | Wait for CI, fix errors |
| "Base commit changed" | Main updated | Rebase feature branch |

## Integration with Kiro Spec Workflow

The Batch & Merge Skills integrate with the existing Kiro spec workflow:

```
Issue Selection
    ↓
/kiro:batch-init (dependency analysis)
    ↓
/kiro-spec-quick (per issue)
    ↓
Create PR (automatic or manual)
    ↓
/kiro:merge-verify (gate check)
    ↓
/kiro:merge-execute (safe merge)
    ↓
/kiro:batch-report (final report)
```

## See Also

- `.claude/skills/batch-skill/SKILL.md` - Batch Skill specification
- `.claude/skills/merge-skill/SKILL.md` - Merge Skill specification
- Issue #18 - GitHub Issue for Batch & Merge Skills
- Issue #6 - PureSharp v1.0 Roadmap
