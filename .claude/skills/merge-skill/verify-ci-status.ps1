# PureSharp Merge Skill - CI + Approval Verification Script
# Verifies GitHub Actions CI status and human approval state for the TECHNICAL merge gates.
#
# This script NEVER merges anything. A passing run means the technical gates are satisfied and
# the PR becomes a MERGE CANDIDATE. Merge execution still requires explicit human approval.
#
# Usage: .\verify-ci-status.ps1 -PR <number> [-TimeoutMinutes 30] [-Format json|text] [-Wait]

param(
    [Parameter(Mandatory = $true)][int]$PR,
    [string]$Format = "text",
    [int]$TimeoutMinutes = 30,
    [string]$ConfigPath,
    [switch]$Wait
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'approval-lib.ps1')

# Repository root: .claude/skills/merge-skill -> .claude/skills -> .claude -> repo root
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')).Path
if (-not $ConfigPath) {
    $ConfigPath = Join-Path $repoRoot '.kiro/merge.config.json'
}

# Initialize result object.
# NOTE: 'pr' MUST be a hashtable - it is populated with named properties below.
$result = @{
    timestamp = Get-Date -Format "o"
    pr        = @{
        number            = $PR
        approvalsVerified = $false
    }
    ci        = @{}
    approval  = @{}
    verified  = $true
    errors    = @()
    notes     = @()
    workflow  = @()
}

function Add-Error {
    param([string]$Message)
    $result.errors += $Message
    $result.verified = $false
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

function Add-Note {
    param([string]$Message)
    $result.notes += $Message
    Write-Host "  NOTE: $Message" -ForegroundColor DarkGray
}

function Add-Workflow {
    param([string]$Name, [string]$Status, [string]$RunId, [string]$HeadSha)
    $result.workflow += @{
        name      = $Name
        status    = $Status
        runId     = $RunId
        headSha   = $HeadSha
        timestamp = Get-Date -Format "o"
    }
}

function Write-Result {
    param([int]$ExitCode)

    if ($Format -eq "json") {
        $result | ConvertTo-Json -Depth 10 | Write-Output
    }
    exit $ExitCode
}

function Get-ShortSha {
    param([AllowNull()][string]$Sha)
    if ([string]::IsNullOrWhiteSpace($Sha)) { return "(none)" }
    if ($Sha.Length -lt 7) { return $Sha }
    return $Sha.Substring(0, 7)
}

# ---------------------------------------------------------------------------
# 0. Load merge configuration (FAIL-CLOSED: no hardcoded fallback)
# ---------------------------------------------------------------------------
Write-Host "Loading merge configuration..." -ForegroundColor Cyan

$approvalConfig = Get-MergeApprovalConfig -ConfigPath $ConfigPath
foreach ($note in $approvalConfig.notes) { Add-Note $note }

if (-not $approvalConfig.ok) {
    foreach ($cfgError in $approvalConfig.errors) { Add-Error "CONFIG ERROR: $cfgError" }
    Write-Host "`nMERGE BLOCKED: configuration could not be loaded" -ForegroundColor Red
    Write-Result -ExitCode 1
}

$requiredApprovals = $approvalConfig.requiredApprovals
$result.approval.requiredApprovals = $requiredApprovals
$result.approval.requiredApprovalsSource = $approvalConfig.source
Write-Host "OK Config: requiredApprovals = $requiredApprovals (from $($approvalConfig.source))" -ForegroundColor Green

# ---------------------------------------------------------------------------
# 1. PR information
# ---------------------------------------------------------------------------
Write-Host "Fetching PR #$PR information..." -ForegroundColor Cyan

try {
    $prJson = & gh pr view $PR --json number,state,headRefName,baseRefName,headRefOid,mergeStateStatus 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $prJson) {
        Add-Error "Failed to fetch PR information (gh pr view exit $LASTEXITCODE)"
        Write-Result -ExitCode 1
    }
    $prInfo = $prJson | ConvertFrom-Json
} catch {
    Add-Error "Exception fetching PR: $_"
    Write-Result -ExitCode 1
}

$result.pr.number     = $prInfo.number
$result.pr.state      = $prInfo.state
$result.pr.headRef    = $prInfo.headRefName
$result.pr.baseRef    = $prInfo.baseRefName
$result.pr.mergeState = $prInfo.mergeStateStatus
$result.pr.headSha    = $prInfo.headRefOid

Write-Host "OK PR #$($result.pr.number): $($result.pr.state)" -ForegroundColor Green
Write-Host "  Branch: $($result.pr.headRef) -> $($result.pr.baseRef)" -ForegroundColor Gray
Write-Host "  Merge State: $($result.pr.mergeState)" -ForegroundColor Gray

if ([string]::IsNullOrWhiteSpace($result.pr.headSha)) {
    Add-Error "Failed to fetch PR head SHA - cannot bind CI or approvals to PR HEAD"
    Write-Result -ExitCode 1
}
Write-Host "  PR HEAD: $($result.pr.headSha)" -ForegroundColor Gray

# ---------------------------------------------------------------------------
# 2. Approval verification (HEAD-bound, per-reviewer latest state, bots excluded)
# ---------------------------------------------------------------------------
Write-Host "Verifying PR approvals..." -ForegroundColor Cyan

$reviewNodes = $null
$graphqlQuery = @'
query($owner:String!, $repo:String!, $pr:Int!) {
  repository(owner:$owner, name:$repo) {
    pullRequest(number:$pr) {
      headRefOid
      reviews(first:100) {
        totalCount
        nodes {
          state
          submittedAt
          commit { oid }
          author { login __typename }
        }
      }
    }
  }
}
'@

try {
    $repoJson = & gh repo view --json owner,name 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $repoJson) {
        Add-Error "Failed to resolve repository for review lookup (gh repo view exit $LASTEXITCODE)"
        Write-Result -ExitCode 1
    }
    $repoInfo = $repoJson | ConvertFrom-Json

    $reviewJson = & gh api graphql -f query=$graphqlQuery -f owner=$($repoInfo.owner.login) -f repo=$($repoInfo.name) -F pr=$PR 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $reviewJson) {
        Add-Error "Failed to fetch PR reviews via GraphQL (exit $LASTEXITCODE) - approval state UNVERIFIED"
        Write-Result -ExitCode 1
    }

    $reviewData = $reviewJson | ConvertFrom-Json
    if ($reviewData.PSObject.Properties['errors']) {
        Add-Error "GraphQL returned errors while fetching reviews - approval state UNVERIFIED"
        Write-Result -ExitCode 1
    }

    $pullRequest = $reviewData.data.repository.pullRequest
    if ($null -eq $pullRequest) {
        Add-Error "GraphQL returned no pull request data - approval state UNVERIFIED"
        Write-Result -ExitCode 1
    }

    # The GraphQL HEAD must agree with the REST HEAD; if not, the PR moved mid-run.
    if ($pullRequest.headRefOid -ne $result.pr.headSha) {
        Add-Error "PR HEAD changed during verification ($(Get-ShortSha $result.pr.headSha) -> $(Get-ShortSha $pullRequest.headRefOid)) - re-run required"
        Write-Result -ExitCode 1
    }

    $reviewNodes = @($pullRequest.reviews.nodes)
    if ($pullRequest.reviews.totalCount -gt $reviewNodes.Count) {
        Add-Error "PR has $($pullRequest.reviews.totalCount) reviews but only $($reviewNodes.Count) were retrieved - approval state UNVERIFIED (pagination not implemented)"
        Write-Result -ExitCode 1
    }
} catch {
    Add-Error "Exception fetching PR reviews: $_ - approval state UNVERIFIED"
    Write-Result -ExitCode 1
}

$approvalState = Get-EffectiveApprovals -Reviews $reviewNodes -HeadSha $result.pr.headSha

$result.approval.effectiveApprovals = $approvalState.approvals
$result.approval.approvers          = @($approvalState.approvers)
$result.approval.changesRequested   = @($approvalState.changesRequested)
$result.approval.dismissed          = @($approvalState.dismissed)
$result.approval.staleApprovals     = @($approvalState.staleApprovals)
$result.approval.botReviewsExcluded = @($approvalState.botReviews)
$result.approval.unverified         = @($approvalState.unverified)

Write-Host "  Effective approvals: $($approvalState.approvals) / $requiredApprovals" -ForegroundColor Gray
if ($approvalState.approvers.Count -gt 0) {
    Write-Host "  Approved by: $($approvalState.approvers -join ', ')" -ForegroundColor Gray
}
if ($approvalState.botReviews.Count -gt 0) {
    Write-Host "  Bot reviews excluded: $($approvalState.botReviews -join ', ')" -ForegroundColor DarkGray
}
foreach ($stale in $approvalState.staleApprovals) {
    Write-Host "  STALE approval ignored: $stale" -ForegroundColor Yellow
}
foreach ($dismissedBy in $approvalState.dismissed) {
    Write-Host "  Dismissed review ignored: $dismissedBy" -ForegroundColor Yellow
}
foreach ($unverifiedItem in $approvalState.unverified) {
    Add-Error "UNVERIFIED approval evidence: $unverifiedItem"
}

if ($approvalState.changesRequested.Count -gt 0) {
    Add-Error "Active CHANGES_REQUESTED from: $($approvalState.changesRequested -join ', ')"
}

if ($approvalState.approvals -ge $requiredApprovals) {
    Write-Host "OK PR approvals satisfied" -ForegroundColor Green
    $result.pr.approvalsVerified = $true
} else {
    Write-Host "NG PR approvals insufficient" -ForegroundColor Red
    Add-Error "PR requires $requiredApprovals effective human approval(s) on HEAD $(Get-ShortSha $result.pr.headSha), has $($approvalState.approvals)"
    $result.pr.approvalsVerified = $false
}

# ---------------------------------------------------------------------------
# 3. CI workflow runs (HEAD-bound)
# ---------------------------------------------------------------------------
Write-Host "Fetching CI workflow status..." -ForegroundColor Cyan

$runs = @()
try {
    $runsJson = & gh run list --branch $result.pr.headRef --json workflowName,name,status,conclusion,databaseId,headSha --limit 20 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $runsJson) {
        Add-Error "Failed to fetch workflow runs (CI API failed, exit $LASTEXITCODE)"
        Write-Result -ExitCode 1
    }
    $runs = @($runsJson | ConvertFrom-Json)
} catch {
    Add-Error "Exception fetching workflows: $_"
    Write-Result -ExitCode 1
}

if ($runs.Count -eq 0) {
    Add-Error "No CI workflows found for branch $($result.pr.headRef)"
}

$staleRuns = 0
foreach ($run in $runs) {
    $status = if ($run.conclusion) { $run.conclusion } else { $run.status }

    # CRITICAL: only CI runs whose HEAD matches the PR HEAD are evidence for this PR.
    if ($run.headSha -ne $result.pr.headSha) {
        $staleRuns++
        continue
    }

    Add-Workflow -Name $run.workflowName -Status $status -RunId $run.databaseId -HeadSha $run.headSha
    Write-Host "OK Workflow: $($run.workflowName)" -ForegroundColor Cyan
    Write-Host "  Status: $status" -ForegroundColor Gray
    Write-Host "  SHA: $(Get-ShortSha $run.headSha)" -ForegroundColor Gray
}

$result.ci.runsInspected = $runs.Count
$result.ci.runsAtPrHead  = $result.workflow.Count
$result.ci.staleRunsIgnored = $staleRuns
$result.ci.headSha = if ($result.workflow.Count -gt 0) { $result.pr.headSha } else { $null }
$result.ci.headMatchesPrHead = ($result.workflow.Count -gt 0)

if ($staleRuns -gt 0) {
    Write-Host "  $staleRuns run(s) at other commits ignored as stale" -ForegroundColor Yellow
}

if ($runs.Count -gt 0 -and $result.workflow.Count -eq 0) {
    Add-Error "No CI run matches PR HEAD $(Get-ShortSha $result.pr.headSha) - all CI evidence is stale"
}

# ---------------------------------------------------------------------------
# 4. Optional wait for CI completion
# ---------------------------------------------------------------------------
# KNOWN LIMITATION (Phase 2 - CI polling refresh):
# The gate evaluation in section 5 reads $result.workflow, which was captured in section 3
# BEFORE this loop runs. Waiting therefore cannot turn a pending workflow into a passing
# gate within a single invocation - a run that was 'in_progress' at snapshot time still
# blocks. This fails CLOSED (it can never turn a blocked verdict into a pass), so it is a
# usability limit, not a safety hole. Until it is fixed, re-run the script after CI
# finishes rather than relying on -Wait to produce a passing verdict.
if ($Wait) {
    Write-Host "Waiting for CI completion (timeout: $TimeoutMinutes min)..." -ForegroundColor Cyan
    Write-Host "NOTE: -Wait cannot upgrade a blocked verdict in this run; re-run after CI finishes." -ForegroundColor DarkGray

    $startTime = Get-Date
    $timeout = New-TimeSpan -Minutes $TimeoutMinutes

    while ((Get-Date) - $startTime -lt $timeout) {
        try {
            $latestJson = & gh run list --branch $result.pr.headRef --json status,conclusion,headSha --limit 1 2>$null
            if ($LASTEXITCODE -ne 0 -or -not $latestJson) {
                Add-Error "Failed to poll workflow status (exit $LASTEXITCODE)"
                break
            }
            $latestRun = @($latestJson | ConvertFrom-Json)[0]

            if ($latestRun.headSha -ne $result.pr.headSha) {
                Add-Error "Newest CI run is for $(Get-ShortSha $latestRun.headSha), not PR HEAD $(Get-ShortSha $result.pr.headSha)"
                break
            }

            if ($latestRun.conclusion) {
                $result.ci.final = $latestRun.conclusion
                Write-Host "OK CI completed: $($latestRun.conclusion)" -ForegroundColor Green
                break
            }

            $elapsed = (Get-Date) - $startTime
            Write-Host "... CI in progress ($($elapsed.TotalSeconds.ToString('F0'))s)" -ForegroundColor Yellow
            Start-Sleep -Seconds 30
        } catch {
            Add-Error "Exception checking workflow status: $_"
            break
        }
    }

    if (-not $result.ci.final) {
        Add-Error "CI did not complete within $TimeoutMinutes minutes"
    }
}

# ---------------------------------------------------------------------------
# 5. Technical gate evaluation (FAIL-CLOSED)
# ---------------------------------------------------------------------------
Write-Host "`nChecking merge gates..." -ForegroundColor Cyan

$allGatesPassed = $result.verified

if ($result.pr.state -ne "OPEN") {
    Write-Host "NG PR state: $($result.pr.state)" -ForegroundColor Red
    $allGatesPassed = $false
} else {
    Write-Host "OK PR state: OPEN" -ForegroundColor Green
}

if ($result.pr.mergeState -eq "MERGEABLE" -or $result.pr.mergeState -eq "CLEAN") {
    Write-Host "OK Merge state: $($result.pr.mergeState)" -ForegroundColor Green
} else {
    Write-Host "NG Merge state: $($result.pr.mergeState)" -ForegroundColor Red
    $allGatesPassed = $false
}

if ($result.workflow.Count -eq 0) {
    Write-Host "NG No CI workflow at PR HEAD" -ForegroundColor Red
    $allGatesPassed = $false
} else {
    Write-Host "OK CI HEAD matches PR HEAD: $(Get-ShortSha $result.pr.headSha)" -ForegroundColor Green
    foreach ($wf in $result.workflow) {
        if ($wf.status -eq "success") {
            Write-Host "OK Workflow: $($wf.name) = $($wf.status)" -ForegroundColor Green
        } elseif ($wf.status -eq "in_progress" -or $wf.status -eq "pending" -or $wf.status -eq "queued") {
            Write-Host "... Workflow pending: $($wf.name)" -ForegroundColor Yellow
            $allGatesPassed = $false
        } else {
            Write-Host "NG Workflow failed/unknown: $($wf.name) = $($wf.status)" -ForegroundColor Red
            $allGatesPassed = $false
        }
    }
}

if ($result.pr.approvalsVerified) {
    Write-Host "OK Approvals: $($result.approval.effectiveApprovals) / $requiredApprovals" -ForegroundColor Green
} else {
    Write-Host "NG Approvals: $($result.approval.effectiveApprovals) / $requiredApprovals" -ForegroundColor Red
    $allGatesPassed = $false
}

# ---------------------------------------------------------------------------
# 6. Summary
# ---------------------------------------------------------------------------
$result.verified = $allGatesPassed

Write-Host "`nSummary" -ForegroundColor Cyan
Write-Host "PR:                 #$($result.pr.number) ($($result.pr.state))" -ForegroundColor Gray
Write-Host "PR HEAD:            $($result.pr.headSha)" -ForegroundColor Gray
Write-Host "CI HEAD:            $(if ($result.ci.headSha) { $result.ci.headSha } else { '(none at PR HEAD)' })" -ForegroundColor Gray
Write-Host "CI HEAD == PR HEAD: $($result.ci.headMatchesPrHead)" -ForegroundColor Gray
Write-Host "Workflows at HEAD:  $($result.workflow.Count) (stale ignored: $staleRuns)" -ForegroundColor Gray
Write-Host "Approvals:          $($result.approval.effectiveApprovals) / $requiredApprovals" -ForegroundColor Gray

if ($result.verified) {
    Write-Host "`nResult: MERGE CANDIDATE (all technical gates passed)" -ForegroundColor Green
    Write-Host "Merge execution still requires EXPLICIT HUMAN APPROVAL. This script does not merge." -ForegroundColor Yellow
} else {
    Write-Host "`nResult: MERGE BLOCKED" -ForegroundColor Red
    if ($result.errors.Count -gt 0) {
        Write-Host "Errors:" -ForegroundColor Red
        $result.errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    }
}

if ($Format -ne "json") {
    Write-Host "`nDetailed Report:" -ForegroundColor Cyan
    Write-Host "State: $($result.pr.state)" -ForegroundColor Gray
    Write-Host "Merge State: $($result.pr.mergeState)" -ForegroundColor Gray
    Write-Host "Workflows: $($result.workflow.Count)" -ForegroundColor Gray
    Write-Host "Errors: $($result.errors.Count)" -ForegroundColor Gray
}

if ($result.verified) { Write-Result -ExitCode 0 } else { Write-Result -ExitCode 1 }
