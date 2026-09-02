# PureSharp Merge Skill - Approval Logic Test Harness
# Exercises Get-EffectiveApprovals against fixtures that cannot be reproduced on a live PR.
# Usage: pwsh -NoProfile -File .\test-approval-logic.ps1

. (Join-Path $PSScriptRoot 'approval-lib.ps1')

$HEAD = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
$OLD  = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'

$failures = 0
$passes = 0

function New-Review {
    param(
        [string]$State,
        [string]$Login,
        [string]$TypeName = 'User',
        [string]$Oid = $HEAD,
        [string]$SubmittedAt
    )
    [pscustomobject]@{
        state       = $State
        submittedAt = $SubmittedAt
        commit      = [pscustomobject]@{ oid = $Oid }
        author      = [pscustomobject]@{ login = $Login; '__typename' = $TypeName }
    }
}

function Assert-Case {
    param(
        [string]$Name,
        [array]$Reviews,
        [int]$ExpectedApprovals,
        [string]$HeadSha = $HEAD,
        [hashtable]$Expect = @{}
    )

    $actual = Get-EffectiveApprovals -Reviews $Reviews -HeadSha $HeadSha
    $ok = ($actual.approvals -eq $ExpectedApprovals)
    $detail = "approvals=$($actual.approvals) expected=$ExpectedApprovals"

    foreach ($key in $Expect.Keys) {
        $actualCount = @($actual[$key]).Count
        if ($actualCount -ne $Expect[$key]) {
            $ok = $false
            $detail += "; $key=$actualCount expected=$($Expect[$key])"
        }
    }

    if ($ok) {
        $script:passes++
        Write-Host "PASS  $Name ($detail)" -ForegroundColor Green
    } else {
        $script:failures++
        Write-Host "FAIL  $Name ($detail)" -ForegroundColor Red
    }
}

Write-Host "Approval logic tests (HEAD = $(($HEAD).Substring(0,7)))`n" -ForegroundColor Cyan

# Case A: same reviewer approves twice on current HEAD => counted once
Assert-Case -Name 'A: same reviewer APPROVED twice => 1' -ExpectedApprovals 1 -Reviews @(
    New-Review -State 'APPROVED' -Login 'alice' -SubmittedAt '2026-09-01T10:00:00Z'
    New-Review -State 'APPROVED' -Login 'alice' -SubmittedAt '2026-09-01T11:00:00Z'
)

# Case B: same reviewer approves then requests changes => not counted, and blocking
Assert-Case -Name 'B: APPROVED then CHANGES_REQUESTED => 0' -ExpectedApprovals 0 -Expect @{ changesRequested = 1 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'alice' -SubmittedAt '2026-09-01T10:00:00Z'
    New-Review -State 'CHANGES_REQUESTED' -Login 'alice' -SubmittedAt '2026-09-01T11:00:00Z'
)

# Case C: bot approval => not counted
Assert-Case -Name 'C: bot APPROVED (__typename=Bot) => 0' -ExpectedApprovals 0 -Expect @{ botReviews = 1 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'coderabbitai' -TypeName 'Bot' -SubmittedAt '2026-09-01T10:00:00Z'
)

Assert-Case -Name 'C2: bot APPROVED ([bot] login suffix) => 0' -ExpectedApprovals 0 -Expect @{ botReviews = 1 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'some-app[bot]' -TypeName 'User' -SubmittedAt '2026-09-01T10:00:00Z'
)

Assert-Case -Name 'C3: known bot login, User typename => 0' -ExpectedApprovals 0 -Expect @{ botReviews = 1 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'dependabot' -TypeName 'User' -SubmittedAt '2026-09-01T10:00:00Z'
)

# Case D: approval bound to an older commit => stale, not counted
Assert-Case -Name 'D: APPROVED on old HEAD => 0 (stale)' -ExpectedApprovals 0 -Expect @{ staleApprovals = 1 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'alice' -Oid $OLD -SubmittedAt '2026-09-01T10:00:00Z'
)

# Case E: human approval on current HEAD => counted
Assert-Case -Name 'E: human APPROVED on current HEAD => 1' -ExpectedApprovals 1 -Expect @{ approvers = 1 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'alice' -SubmittedAt '2026-09-01T10:00:00Z'
)

# --- Additional fail-closed cases -----------------------------------------

# COMMENTED reviews do not change reviewer state (GitHub semantics)
Assert-Case -Name 'F: APPROVED then COMMENTED => 1' -ExpectedApprovals 1 -Reviews @(
    New-Review -State 'APPROVED' -Login 'alice' -SubmittedAt '2026-09-01T10:00:00Z'
    New-Review -State 'COMMENTED' -Login 'alice' -SubmittedAt '2026-09-01T11:00:00Z'
)

# CHANGES_REQUESTED then a fresh APPROVED => counted
Assert-Case -Name 'G: CHANGES_REQUESTED then APPROVED => 1' -ExpectedApprovals 1 -Expect @{ changesRequested = 0 } -Reviews @(
    New-Review -State 'CHANGES_REQUESTED' -Login 'alice' -SubmittedAt '2026-09-01T10:00:00Z'
    New-Review -State 'APPROVED' -Login 'alice' -SubmittedAt '2026-09-01T11:00:00Z'
)

# DISMISSED latest state => not counted
Assert-Case -Name 'H: APPROVED then DISMISSED => 0' -ExpectedApprovals 0 -Expect @{ dismissed = 1 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'alice' -SubmittedAt '2026-09-01T10:00:00Z'
    New-Review -State 'DISMISSED' -Login 'alice' -SubmittedAt '2026-09-01T11:00:00Z'
)

# Two distinct humans on current HEAD => 2
Assert-Case -Name 'I: two distinct humans APPROVED => 2' -ExpectedApprovals 2 -Expect @{ approvers = 2 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'alice' -SubmittedAt '2026-09-01T10:00:00Z'
    New-Review -State 'APPROVED' -Login 'bob' -SubmittedAt '2026-09-01T10:05:00Z'
)

# Missing commit OID => UNVERIFIED, not counted
Assert-Case -Name 'J: APPROVED with no commit OID => 0 (UNVERIFIED)' -ExpectedApprovals 0 -Expect @{ unverified = 1 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'alice' -Oid '' -SubmittedAt '2026-09-01T10:00:00Z'
)

# Unparsable timestamp => UNVERIFIED, not counted
Assert-Case -Name 'K: APPROVED with bad submittedAt => 0 (UNVERIFIED)' -ExpectedApprovals 0 -Expect @{ unverified = 1 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'alice' -SubmittedAt 'not-a-date'
)

# Null author => UNVERIFIED, not counted
Assert-Case -Name 'L: APPROVED with null author => 0 (UNVERIFIED)' -ExpectedApprovals 0 -Expect @{ unverified = 1 } -Reviews @(
    [pscustomobject]@{
        state       = 'APPROVED'
        submittedAt = '2026-09-01T10:00:00Z'
        commit      = [pscustomobject]@{ oid = $HEAD }
        author      = $null
    }
)

# No reviews at all => 0
Assert-Case -Name 'M: no reviews => 0' -ExpectedApprovals 0 -Reviews @()

# Missing HEAD SHA => 0 and UNVERIFIED (never fail open)
Assert-Case -Name 'N: empty HEAD SHA => 0 (UNVERIFIED)' -ExpectedApprovals 0 -HeadSha '' -Expect @{ unverified = 1 } -Reviews @(
    New-Review -State 'APPROVED' -Login 'alice' -SubmittedAt '2026-09-01T10:00:00Z'
)

# --- Config loader cases ---------------------------------------------------

Write-Host "`nConfig loader tests" -ForegroundColor Cyan

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')).Path
$realConfig = Join-Path $repoRoot '.kiro/merge.config.json'
$realCfg = Get-MergeApprovalConfig -ConfigPath $realConfig
if ($realCfg.ok) {
    $passes++
    Write-Host "PASS  real .kiro/merge.config.json loads (requiredApprovals=$($realCfg.requiredApprovals))" -ForegroundColor Green
} else {
    $failures++
    Write-Host "FAIL  real .kiro/merge.config.json rejected: $($realCfg.errors -join '; ')" -ForegroundColor Red
}

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("merge-config-test-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    function Test-Config {
        param([string]$Name, [string]$Json, [bool]$ExpectOk, [int]$ExpectRequired = 0)
        $path = Join-Path $tmp ((New-Guid).ToString('N') + '.json')
        Set-Content -LiteralPath $path -Value $Json -Encoding utf8
        $cfg = Get-MergeApprovalConfig -ConfigPath $path
        $ok = ($cfg.ok -eq $ExpectOk) -and (-not $ExpectOk -or $cfg.requiredApprovals -eq $ExpectRequired)
        if ($ok) {
            $script:passes++
            Write-Host "PASS  $Name" -ForegroundColor Green
        } else {
            $script:failures++
            Write-Host "FAIL  $Name (ok=$($cfg.ok) required=$($cfg.requiredApprovals) errors=$($cfg.errors -join '; '))" -ForegroundColor Red
        }
    }

    Test-Config -Name 'config requiredApprovals=2 is honoured' -ExpectOk $true -ExpectRequired 2 -Json '{"approval":{"requirePRApproval":true,"requiredApprovals":2,"requireCodeOwnerApproval":false,"dismissStaleReviews":false}}'
    Test-Config -Name 'config requiredApprovals=0 is rejected'  -ExpectOk $false -Json '{"approval":{"requirePRApproval":true,"requiredApprovals":0}}'
    Test-Config -Name 'config missing requiredApprovals rejected' -ExpectOk $false -Json '{"approval":{"requirePRApproval":true}}'
    Test-Config -Name 'config requirePRApproval=false rejected' -ExpectOk $false -Json '{"approval":{"requirePRApproval":false,"requiredApprovals":1}}'
    Test-Config -Name 'config requireCodeOwnerApproval=true rejected (not yet enforced)' -ExpectOk $false -Json '{"approval":{"requirePRApproval":true,"requiredApprovals":1,"requireCodeOwnerApproval":true}}'
    Test-Config -Name 'config missing approval section rejected' -ExpectOk $false -Json '{"merge":{"baseBranch":"main"}}'
    Test-Config -Name 'malformed JSON rejected' -ExpectOk $false -Json '{ not json'

    $missingCfg = Get-MergeApprovalConfig -ConfigPath (Join-Path $tmp 'does-not-exist.json')
    if (-not $missingCfg.ok) {
        $passes++
        Write-Host "PASS  missing config file rejected" -ForegroundColor Green
    } else {
        $failures++
        Write-Host "FAIL  missing config file accepted" -ForegroundColor Red
    }
} finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "`n$passes passed, $failures failed" -ForegroundColor $(if ($failures -eq 0) { 'Green' } else { 'Red' })
if ($failures -gt 0) { exit 1 } else { exit 0 }
