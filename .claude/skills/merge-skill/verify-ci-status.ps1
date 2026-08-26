# PureSharp Merge Skill - CI Status Verification Script
# This script verifies GitHub Actions CI status for merge gates
# Usage: .\verify-ci-status.ps1 -PR <number> [-TimeoutMinutes 30] [-Format json|text]

param(
    [int]$PR,
    [string]$Format = "text",
    [int]$TimeoutMinutes = 30,
    [switch]$Wait,
    [switch]$Verbose
)

# Initialize result object
$result = @{
    timestamp = Get-Date -Format "o"
    pr = $PR
    ci = @{}
    verified = $true
    errors = @()
    workflow = @()
}

# Helper function
function Add-Error {
    param([string]$Message)
    $result.errors += $Message
    $result.verified = $false
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

function Add-Workflow {
    param(
        [string]$Name,
        [string]$Status,
        [string]$RunId
    )
    $result.workflow += @{
        name = $Name
        status = $Status
        runId = $RunId
        timestamp = Get-Date -Format "o"
    }
}

# 1. Get PR Information
Write-Host "Fetching PR #$PR information..." -ForegroundColor Cyan

try {
    $prInfo = & gh pr view $PR --json number,state,headRefName,baseRefName,mergeStateStatus,statusCheckRollup 2>$null | ConvertFrom-Json

    if ($LASTEXITCODE -eq 0) {
        $result.pr.number = $prInfo.number
        $result.pr.state = $prInfo.state
        $result.pr.headRef = $prInfo.headRefName
        $result.pr.baseRef = $prInfo.baseRefName
        $result.pr.mergeState = $prInfo.mergeStateStatus

        Write-Host "✓ PR #$($prInfo.number): $($prInfo.state)" -ForegroundColor Green
        Write-Host "  Branch: $($prInfo.headRefName) -> $($prInfo.baseRefName)" -ForegroundColor Gray
        Write-Host "  Merge State: $($prInfo.mergeStateStatus)" -ForegroundColor Gray
    } else {
        Add-Error "Failed to fetch PR information"
        exit 1
    }

    # CRITICAL PHASE 1: Verify GitHub PR approvals
    Write-Host "Checking PR approvals..." -ForegroundColor Cyan

    try {
        $reviews = & gh pr view $PR --json reviews 2>$null | ConvertFrom-Json

        if ($LASTEXITCODE -ne 0) {
            Add-Error "Failed to fetch PR reviews (GitHub API error)"
            exit 1
        }

        # Count APPROVED reviews (not just comments)
        $approvedReviews = @($reviews.reviews | Where-Object { $_.state -eq "APPROVED" })
        $approvalCount = $approvedReviews.Count

        # For now, require at least 1 approval
        $requiredApprovals = 1

        Write-Host "  Approvals: $approvalCount / $requiredApprovals" -ForegroundColor Gray

        if ($approvalCount -ge $requiredApprovals) {
            Write-Host "✓ PR approvals satisfied" -ForegroundColor Green
            $result.pr.approvalsVerified = $true
        } else {
            Write-Host "✗ PR approvals insufficient" -ForegroundColor Red
            Add-Error "PR requires $requiredApprovals approval(s), has $approvalCount"
            exit 1
        }
    } catch {
        Add-Error "Exception checking approvals: $_"
        exit 1
    }

} catch {
    Add-Error "Exception fetching PR: $_"
    exit 1
}

# 2. Get PR Head SHA and CI Workflow Runs
Write-Host "Fetching CI workflow status..." -ForegroundColor Cyan

try {
    # Get PR head SHA for CI comparison
    $prHeadInfo = & gh pr view $PR --json headRefOid 2>$null | ConvertFrom-Json

    if ($LASTEXITCODE -eq 0 -and $prHeadInfo.headRefOid) {
        $result.pr.headSha = $prHeadInfo.headRefOid
        Write-Host "✓ PR Head SHA: $($result.pr.headSha.Substring(0,7))" -ForegroundColor Cyan
    } else {
        Add-Error "Failed to fetch PR head SHA"
        exit 1
    }
} catch {
    Add-Error "Exception fetching PR head SHA: $_"
    exit 1
}

try {
    # Get workflow runs for the PR branch
    $prBranch = $result.pr.headRef
    $runs = & gh run list --branch $prBranch --json workflowName,name,status,conclusion,databaseId,headSha --limit 5 2>$null | ConvertFrom-Json

    if ($LASTEXITCODE -ne 0 -or -not $runs) {
        Add-Error "Failed to fetch workflow runs (CI API failed)"
        exit 1
    }

    if ($runs.Count -eq 0) {
        Add-Error "No CI workflows found for branch $prBranch"
        exit 1
    }

    foreach ($run in $runs) {
        $status = if ($run.conclusion) { $run.conclusion } else { $run.status }

        # CRITICAL: Validate CI run HEAD matches PR HEAD
        $ciHeadSha = $run.headSha
        if ($ciHeadSha -ne $result.pr.headSha) {
            Write-Host "⚠ Warning: CI run SHA ($($ciHeadSha.Substring(0,7))) does not match PR HEAD ($($result.pr.headSha.Substring(0,7)))" -ForegroundColor Yellow
            Write-Host "  This CI result may be stale - marking as UNVERIFIED" -ForegroundColor Yellow
            Add-Error "CI HEAD SHA does not match PR HEAD - CI result is stale"
            continue
        }

        Add-Workflow -Name $run.workflowName -Status $status -RunId $run.databaseId

        Write-Host "✓ Workflow: $($run.workflowName)" -ForegroundColor Cyan
        Write-Host "  Status: $status" -ForegroundColor Gray
        Write-Host "  SHA: $($ciHeadSha.Substring(0,7))" -ForegroundColor Gray
    }
} catch {
    Add-Error "Exception fetching workflows: $_"
    exit 1
}

# 3. Check workflow completion (if Wait flag is set)
if ($Wait) {
    Write-Host "Waiting for CI completion (timeout: $TimeoutMinutes min)..." -ForegroundColor Cyan

    $startTime = Get-Date
    $timeout = New-TimeSpan -Minutes $TimeoutMinutes

    while ((Get-Date) - $startTime -lt $timeout) {
        try {
            $latestRun = & gh run list --branch $result.pr.headRef --json status,conclusion --limit 1 2>$null | ConvertFrom-Json

            if ($latestRun.conclusion) {
                $result.ci.final = $latestRun.conclusion
                Write-Host "✓ CI completed: $($latestRun.conclusion)" -ForegroundColor Green
                break
            } else {
                $elapsed = (Get-Date) - $startTime
                Write-Host "⏳ CI in progress... ($($elapsed.TotalSeconds.ToString('F0'))s)" -ForegroundColor Yellow
                Start-Sleep -Seconds 30
            }
        } catch {
            Add-Error "Exception checking workflow status: $_"
            break
        }
    }

    if (-not $result.ci.final) {
        Add-Error "CI did not complete within $TimeoutMinutes minutes"
    }
}

# 4. Check if all gates passed (FAIL-CLOSED design)
Write-Host "Checking merge gates..." -ForegroundColor Cyan

# Start with verified state from PR info
$allGatesPassed = $result.verified

# Check merge state (must be explicitly mergeable)
if ($result.pr.mergeState -eq "MERGEABLE" -or $result.pr.mergeState -eq "CLEAN") {
    Write-Host "✓ Merge state: MERGEABLE" -ForegroundColor Green
} else {
    Write-Host "✗ Merge state: $($result.pr.mergeState)" -ForegroundColor Red
    $allGatesPassed = $false
}

# Check workflow status - FAIL-CLOSED: no workflows found = BLOCKED
if ($result.workflow.Count -eq 0) {
    Write-Host "✗ No valid CI workflows found" -ForegroundColor Red
    $allGatesPassed = $false
} else {
    foreach ($wf in $result.workflow) {
        if ($wf.status -eq "success") {
            Write-Host "✓ Workflow: $($wf.name) = $($wf.status)" -ForegroundColor Green
        } elseif ($wf.status -eq "in_progress" -or $wf.status -eq "pending" -or $wf.status -eq "queued") {
            Write-Host "⏳ Workflow pending: $($wf.name)" -ForegroundColor Yellow
            $allGatesPassed = $false
        } else {
            Write-Host "✗ Workflow failed/unknown: $($wf.name) = $($wf.status)" -ForegroundColor Red
            $allGatesPassed = $false
        }
    }
}

# Check approvals
if (-not $result.pr.approvalsVerified) {
    Write-Host "✗ PR approvals not verified" -ForegroundColor Red
    $allGatesPassed = $false
}

# 5. Summary
Write-Host "`nSummary" -ForegroundColor Cyan

$result.verified = $allGatesPassed
if ($result.verified) {
    Write-Host "✓ CI verification passed" -ForegroundColor Green
} else {
    Write-Host "✗ CI verification failed or incomplete" -ForegroundColor Red
    if ($result.errors.Count -gt 0) {
        Write-Host "Errors:" -ForegroundColor Red
        $result.errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    }
}

# Output format
if ($Format -eq "json") {
    $result | ConvertTo-Json -Depth 10 | Write-Output
} else {
    Write-Host "`nDetailed Report:" -ForegroundColor Cyan
    Write-Host "PR: #$($result.pr.number)" -ForegroundColor Gray
    Write-Host "State: $($result.pr.state)" -ForegroundColor Gray
    Write-Host "Merge State: $($result.pr.mergeState)" -ForegroundColor Gray
    Write-Host "Workflows: $($result.workflow.Count)" -ForegroundColor Gray
}

# Return exit code
exit ($result.verified ? 0 : 1)
