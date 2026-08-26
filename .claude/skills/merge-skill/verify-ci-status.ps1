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

        Write-Host "✓ PR #$PR: $($prInfo.state)" -ForegroundColor Green
        Write-Host "  Branch: $($prInfo.headRefName) -> $($prInfo.baseRefName)" -ForegroundColor Gray
        Write-Host "  Merge State: $($prInfo.mergeStateStatus)" -ForegroundColor Gray
    } else {
        Add-Error "Failed to fetch PR information"
        exit 1
    }
} catch {
    Add-Error "Exception fetching PR: $_"
    exit 1
}

# 2. Get CI Workflows
Write-Host "Fetching CI workflow status..." -ForegroundColor Cyan

try {
    # Get workflow runs for the PR
    $prBranch = $result.pr.headRef
    $runs = & gh run list --branch $prBranch --json workflowName,name,status,conclusion,databaseId --limit 5 2>$null | ConvertFrom-Json

    if ($LASTEXITCODE -eq 0 -and $runs.Count -gt 0) {
        foreach ($run in $runs) {
            $status = if ($run.conclusion) { $run.conclusion } else { $run.status }

            Add-Workflow -Name $run.workflowName -Status $status -RunId $run.databaseId

            Write-Host "✓ Workflow: $($run.workflowName)" -ForegroundColor Cyan
            Write-Host "  Status: $status" -ForegroundColor Gray
            Write-Host "  ID: $($run.databaseId)" -ForegroundColor Gray
        }
    } else {
        Write-Host "⚠ No workflows found for branch $prBranch" -ForegroundColor Yellow
    }
} catch {
    Add-Error "Exception fetching workflows: $_"
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

# 4. Check if all gates passed
Write-Host "Checking merge gates..." -ForegroundColor Cyan

$allGatesPassed = $true

# Check merge state
if ($result.pr.mergeState -eq "MERGEABLE" -or $result.pr.mergeState -eq "CLEAN") {
    Write-Host "✓ Merge state: MERGEABLE" -ForegroundColor Green
} else {
    Write-Host "✗ Merge state: $($result.pr.mergeState)" -ForegroundColor Red
    $allGatesPassed = $false
}

# Check workflow status
foreach ($wf in $result.workflow) {
    if ($wf.status -eq "success" -or $wf.status -eq "completed") {
        Write-Host "✓ Workflow status: $($wf.name) = $($wf.status)" -ForegroundColor Green
    } elseif ($wf.status -eq "in_progress" -or $wf.status -eq "pending" -or $wf.status -eq "queued") {
        Write-Host "⏳ Workflow in progress: $($wf.name)" -ForegroundColor Yellow
        $allGatesPassed = $false
    } else {
        Write-Host "✗ Workflow failed: $($wf.name) = $($wf.status)" -ForegroundColor Red
        $allGatesPassed = $false
    }
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
