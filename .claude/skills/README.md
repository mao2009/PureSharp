# PureSharp Batch & Merge Skills

Welcome to the PureSharp Batch & Merge Skills system. This directory contains the infrastructure for safe, coordinated development of multiple GitHub Issues leading up to PureSharp v1.0.0.

## Overview

Two primary skills orchestrate the development workflow:

1. **Batch Skill** (`.claude/skills/batch-skill/SKILL.md`)
   - Orchestrates multiple related issues
   - Analyzes dependencies
   - Plans parallel execution
   - Collects results

2. **Merge Skill** (`.claude/skills/merge-skill/SKILL.md`)
   - Verifies Git state
   - Validates builds and tests
   - Checks CI/CD pipelines
   - Enforces merge gates
   - Executes safe merges

## Quick Start

### For Batch Execution

```bash
# Analyze issues for batch execution
/kiro:batch-init "v1.0 Phase 1" --issues 7,8,9,10

# Execute per-issue implementation (using existing Kiro spec workflow)
/kiro-spec-quick #7
/kiro-spec-quick #8
# etc.

# Generate final report
/kiro:batch-report --format both
```

### For Merge

```powershell
# Verify merge gates for a PR (verification-only, no changes made)
/merge-skill verify --pr 123 --details

# Review verification report
# - Check CONFIRMED items
# - Check for UNVERIFIED or BLOCKED items

# If SAFE to merge:
# 1. Manual merge execution (human approval)
git checkout main
git merge --ff-only feature/issue-NNN
git push origin main

# 2. Post-merge verification (automated)
/merge-skill verify --pr 123 --post-merge
```

## Files & Directories

```text
.claude/
├── skills/
│   ├── README.md (this file)
│   ├── WORKFLOW.md (comprehensive workflow guide)
│   ├── EXAMPLE-REPORT.md (sample report showing both layers)
│   ├── batch-skill/
│   │   ├── SKILL.md (Batch Skill specification)
│   │   └── (implementation details)
│   ├── merge-skill/
│   │   ├── SKILL.md (Merge Skill specification)
│   │   ├── verify-git-state.ps1 (Git verification script)
│   │   ├── verify-build-and-tests.ps1 (Build/test verification)
│   │   └── verify-ci-status.ps1 (CI verification)
│   └── conventions/
│       └── (commit message rules, etc.)
├── projects/
│   └── C--Users-Hobart-Documents-Projects-PureSharp/
│       └── memory/ (project-specific memory)
└── CLAUDE.md (global instructions)

.kiro/
├── batch.config.json (Batch Skill configuration)
├── merge.config.json (Merge Skill configuration)
├── steering/ (project guidance documents)
├── specs/ (individual issue specifications)
└── settings/ (Kiro settings and templates)
```

## Configuration

### Batch Configuration (`.kiro/batch.config.json`)

Controls batch execution behavior:
- Max parallel tasks
- CI/CD timeouts
- Failure handling
- Reporting format
- Roadmap phases

### Merge Configuration (`.kiro/merge.config.json`)

Controls merge verification:
- Base branch
- Approval requirements
- Gate enforcement
- Build/test/CI timeouts
- Post-merge verification

## Verification Scripts

Located in `.claude/skills/merge-skill/`:

### verify-git-state.ps1
Checks Git state: branch, commits, working tree, remote

```powershell
.\.claude\skills\merge-skill\verify-git-state.ps1 -Format text
```

### verify-build-and-tests.ps1
Runs local build and tests

```powershell
.\.claude\skills\merge-skill\verify-build-and-tests.ps1 -Configuration Release
```

### verify-ci-status.ps1
Checks GitHub Actions CI status

```powershell
.\.claude\skills\merge-skill\verify-ci-status.ps1 -PR 123
```

## Evidence Classification

All reports use three evidence types:

| Type | Definition | Example |
|------|-----------|---------|
| **CONFIRMED** | Verified via Git/CLI/API/test | `git status` returns clean |
| **INFERRED** | Reasonable from evidence | No conflicts (from file analysis) |
| **UNVERIFIED** | Not yet checked | "CI probably passing" |

**Golden Rule**: Never claim success for something you haven't verified.

## Two-Layer Reporting

### Human Report
Executive summary for stakeholders:
- What changed
- Success/failure status
- Key metrics
- Next steps

**Example**: `EXAMPLE-REPORT.md` - "Human Report" section

### AI Report
Detailed technical report with full evidence:
- Verified evidence (CONFIRMED)
- Inferred conclusions (INFERRED)
- Unverified items (UNVERIFIED)
- Git state per issue
- Build/test results
- CI/CD status
- PR status

**Example**: `EXAMPLE-REPORT.md` - "AI Report" section

## Integration with Kiro Specs

The Batch & Merge Skills integrate seamlessly with existing Kiro spec workflow:

```text
Issue Selection
    ↓
/kiro:batch-init (analyze dependencies)
    ↓
/kiro-spec-quick (implement per issue)
    ↓
Create PR (automatic or manual)
    ↓
/kiro:merge-verify (gate check)
    ↓
/kiro:merge-execute (safe merge)
    ↓
/kiro:batch-report (final report)
```

See `WORKFLOW.md` for complete workflow examples.

## Safety Guarantees

### Merge Gate Requirements

Merge only executes when ALL conditions are CONFIRMED:

- [x] Git branch verified
- [x] Base commit confirmed
- [x] Result commit confirmed
- [x] Working tree clean
- [x] Build succeeded
- [x] Tests passed
- [x] CI completed successfully
- [x] PR exists and is mergeable
- [x] No file conflicts
- [x] All approvals met

If ANY item is unverified or failed: **MERGE BLOCKED**

### Failure Scenarios

The skills handle failure gracefully:

- **Build Failed**: Report error, block merge, suggest fixes
- **Tests Failed**: Report failures, block merge, provide diagnostics
- **CI Pending**: Wait or timeout, report status
- **Merge Conflict**: Report conflict, suggest rebase
- **Approval Missing**: Report requirement, block merge

See `WORKFLOW.md` for detailed failure handling.

## Recommended Workflow

### Step 1: Plan the Batch
```bash
/kiro:batch-init "v1.0 Phase N" --issues <numbers>
```

Review dependency graph and execution plan.

### Step 2: Implement Issues
For each issue in recommended order:
```bash
/kiro-spec-quick #<issue>
```

### Step 3: Create PRs
Create PRs for each completed issue (automatic with Kiro specs).

### Step 4: Verify Merges
For each PR:
```bash
/kiro:merge-init --pr <number>
/kiro:merge-verify
/kiro:merge-execute
```

### Step 5: Report
After all issues merged:
```bash
/kiro:batch-report --format both
```

## Troubleshooting

### Issue: "Branch not found"
- Verify feature branch exists
- Check branch name matches issue number
- Recreate from latest main if needed

### Issue: "Build failed"
- Run `dotnet build --configuration Release`
- Fix compilation errors
- Push fix to feature branch
- Rerun merge-verify

### Issue: "Tests failed"
- Run `dotnet test`
- Investigate test output
- Fix code or tests
- Push fix and rerun merge-verify

### Issue: "CI pending"
- Check GitHub Actions workflow status
- Wait for completion (up to 30 min default)
- Rerun merge-verify to check updated status

### Issue: "Merge conflict"
- Fetch latest main
- Rebase feature branch on main
- Resolve conflicts locally
- Test locally
- Force-push (if needed)
- Rerun merge-verify

See `WORKFLOW.md` for more troubleshooting scenarios.

## Project Context

### PureSharp v1.0.0 Roadmap

```text
#6  Roadmap
#7  Diagnostic SSOT
#8  Regression Tests
#9  RT Semantics
#10 Interprocedural
#11 LVP Edge Cases
#12 FluentIf Hardening
#13 NuGet Consumer Verification
#14 Compatibility & Performance
#15 Documentation
#16 v0.9 RC
#17 v1.0.0 Release
#18 Batch & Merge Skills (this issue)
```

The Batch & Merge Skills enable efficient, safe execution of this roadmap.

### v1.0.0 Goals

- Establish diagnostic SSOT (#7)
- Strengthen analyzer rules (#8-#12)
- Package & verify NuGet consumer (#13)
- Ensure compatibility & performance (#14)
- Complete documentation (#15)
- Release v1.0.0 (#16-#17)

## See Also

- **WORKFLOW.md**: Comprehensive workflow guide with examples
- **EXAMPLE-REPORT.md**: Sample report showing report format
- **batch-skill/SKILL.md**: Batch Skill specification
- **merge-skill/SKILL.md**: Merge Skill specification
- Issue #18: [Engineering] Introduce Batch and Merge Skills
- Issue #6: [Roadmap] PureSharp v1.0.0 roadmap
- `.kiro/steering/`: Project guidance documents

## Contributing

When implementing new features:

1. Create issue in roadmap
2. Define spec (`.kiro/specs/<issue>`)
3. Implement using Kiro spec workflow
4. Use Batch & Merge Skills for coordination
5. Provide both Human and AI reports

## Support

For questions or issues with Batch & Merge Skills:

1. Review this README
2. Check WORKFLOW.md for examples
3. See EXAMPLE-REPORT.md for report format
4. Consult skill specifications
5. Check .kiro config files

---

**Last Updated**: 2026-08-26  
**Version**: 1.0  
**Status**: Ready for v1.0 roadmap execution
