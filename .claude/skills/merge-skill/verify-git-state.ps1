# PureSharp Merge Skill - Git State Verification Script
# This script verifies Git state for merge gates
# Usage: .\verify-git-state.ps1 [-Verbose] [-Format json|text]

param(
    [string]$Format = "text",
    [switch]$Verbose,
    [string]$BaseBranch = "main"
)

# Initialize result object
$result = @{
    timestamp = Get-Date -Format "o"
    branch = @{}
    commits = @{}
    workingTree = @{}
    remote = @{}
    verified = $true
    errors = @()
}

# Helper function to add error
function Add-Error {
    param([string]$Message)
    $result.errors += $Message
    $result.verified = $false
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

# 1. Branch Verification
Write-Host "Verifying branch..." -ForegroundColor Cyan

try {
    $currentBranch = git rev-parse --abbrev-ref HEAD 2>$null
    if ($LASTEXITCODE -ne 0) {
        Add-Error "Failed to get current branch"
    } else {
        $result.branch.current = $currentBranch
        Write-Host "✓ Current branch: $currentBranch" -ForegroundColor Green
    }
} catch {
    Add-Error "Exception getting branch: $_"
}

try {
    $branchTracking = git branch -vv 2>$null | Select-String "^\*"
    if ($branchTracking) {
        $result.branch.tracking = $branchTracking -replace "^\*\s+", "" -split "\s+" | Select-Object -First 2
        Write-Host "✓ Tracking: $($result.branch.tracking -join ' -> ')" -ForegroundColor Green
    }
} catch {
    Add-Error "Exception getting tracking: $_"
}

# 2. Commit Verification
Write-Host "Verifying commits..." -ForegroundColor Cyan

try {
    $resultCommit = git rev-parse HEAD 2>$null
    if ($LASTEXITCODE -eq 0) {
        $result.commits.result = $resultCommit
        $resultCommitMsg = git log -1 --format="%s" HEAD 2>$null
        $result.commits.resultMessage = $resultCommitMsg
        Write-Host "✓ Result commit: $resultCommit ($resultCommitMsg)" -ForegroundColor Green
    } else {
        Add-Error "Failed to get current commit"
    }
} catch {
    Add-Error "Exception getting result commit: $_"
}

try {
    $baseCommit = git rev-parse "origin/$BaseBranch" 2>$null
    if ($LASTEXITCODE -eq 0) {
        $result.commits.base = $baseCommit
        $baseCommitMsg = git log -1 --format="%s" "origin/$BaseBranch" 2>$null
        $result.commits.baseMessage = $baseCommitMsg
        Write-Host "✓ Base commit: $baseCommit ($baseCommitMsg)" -ForegroundColor Green
    } else {
        Add-Error "Failed to get base commit (origin/$BaseBranch)"
    }
} catch {
    Add-Error "Exception getting base commit: $_"
}

if ($result.commits.result -and $result.commits.base) {
    try {
        $ahead = git rev-list --count "$($result.commits.base)..$($result.commits.result)" 2>$null
        $behind = git rev-list --count "$($result.commits.result)..$($result.commits.base)" 2>$null
        $result.commits.ahead = [int]$ahead
        $result.commits.behind = [int]$behind
        Write-Host "✓ Ancestry: $ahead commits ahead, $behind commits behind" -ForegroundColor Green
    } catch {
        Add-Error "Exception analyzing ancestry: $_"
    }
}

# 3. Working Tree Verification
Write-Host "Verifying working tree..." -ForegroundColor Cyan

try {
    $status = git status --porcelain 2>$null
    if ($LASTEXITCODE -eq 0) {
        $result.workingTree.clean = [string]::IsNullOrEmpty($status)
        if ($result.workingTree.clean) {
            Write-Host "✓ Working tree: clean" -ForegroundColor Green
        } else {
            Write-Host "✗ Working tree: dirty" -ForegroundColor Red
            $result.workingTree.changes = $status -split "`n"
            Add-Error "Working tree has uncommitted changes or untracked files"
        }
    } else {
        Add-Error "Failed to check working tree status"
    }
} catch {
    Add-Error "Exception checking working tree: $_"
}

try {
    $stash = git stash list 2>$null
    if ($LASTEXITCODE -eq 0) {
        $result.workingTree.stashes = @($stash | Measure-Object -Line).Lines
        if ($result.workingTree.stashes -gt 0) {
            Write-Host "⚠ Stashes: $($result.workingTree.stashes) stashed changes" -ForegroundColor Yellow
        }
    }
} catch {
    Add-Error "Exception checking stash: $_"
}

# 4. Remote Verification
Write-Host "Verifying remote..." -ForegroundColor Cyan

try {
    git fetch --dry-run 2>$null
    if ($LASTEXITCODE -eq 0) {
        $result.remote.reachable = $true
        Write-Host "✓ Remote: reachable" -ForegroundColor Green
    } else {
        Add-Error "Remote not reachable"
        $result.remote.reachable = $false
    }
} catch {
    Add-Error "Exception checking remote: $_"
}

# 5. Summary
Write-Host "`nSummary" -ForegroundColor Cyan
if ($result.verified) {
    Write-Host "✓ Git state verified successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Git state verification failed" -ForegroundColor Red
    Write-Host "Errors:" -ForegroundColor Red
    $result.errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

# Output format
if ($Format -eq "json") {
    $result | ConvertTo-Json -Depth 10 | Write-Output
} else {
    Write-Host "`nDetailed Report:" -ForegroundColor Cyan
    Write-Host "Branch: $($result.branch.current)" -ForegroundColor Gray
    Write-Host "Result Commit: $($result.commits.result)" -ForegroundColor Gray
    Write-Host "Base Commit: $($result.commits.base)" -ForegroundColor Gray
    Write-Host "Working Tree Clean: $($result.workingTree.clean)" -ForegroundColor Gray
    Write-Host "Remote Reachable: $($result.remote.reachable)" -ForegroundColor Gray
}

# Return exit code based on verification
exit ($result.verified ? 0 : 1)
