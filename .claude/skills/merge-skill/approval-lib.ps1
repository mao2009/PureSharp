# PureSharp Merge Skill - Approval Evaluation Library
# Dot-source this file to use Get-MergeApprovalConfig and Get-EffectiveApprovals.
# Kept separate from verify-ci-status.ps1 so the approval semantics can be unit tested
# without live GitHub API access (see test-approval-logic.ps1).

# Logins that are never counted as human approvals, in addition to any account
# GitHub itself reports with __typename = "Bot" or a "[bot]" login suffix.
$script:KnownBotLogins = @(
    'coderabbitai',
    'dependabot',
    'github-actions',
    'renovate',
    'codecov',
    'sonarcloud',
    'copilot-pull-request-reviewer'
)

function Test-PropertyPresent {
    param([AllowNull()]$InputObject, [Parameter(Mandatory = $true)][string]$Name)

    if ($null -eq $InputObject) { return $false }
    if ($InputObject -is [System.Collections.IDictionary]) { return $InputObject.Contains($Name) }
    return ($null -ne $InputObject.PSObject.Properties[$Name])
}

function Get-PropertyValue {
    param([AllowNull()]$InputObject, [Parameter(Mandatory = $true)][string]$Name)

    if (-not (Test-PropertyPresent -InputObject $InputObject -Name $Name)) { return $null }
    if ($InputObject -is [System.Collections.IDictionary]) { return $InputObject[$Name] }
    return $InputObject.PSObject.Properties[$Name].Value
}

function Test-ReviewAuthorIsBot {
    param([AllowNull()]$Author)

    if ($null -eq $Author) { return $true }

    if ((Get-PropertyValue -InputObject $Author -Name '__typename') -eq 'Bot') { return $true }

    $login = Get-PropertyValue -InputObject $Author -Name 'login'
    if ([string]::IsNullOrWhiteSpace($login)) { return $true }
    if ($login -match '\[bot\]$') { return $true }
    if ($script:KnownBotLogins -contains $login.ToLowerInvariant()) { return $true }

    return $false
}

function ConvertTo-ReviewTimestamp {
    param([AllowNull()]$Value)

    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }

    $parsed = [datetime]::MinValue
    # Normalize every timestamp to UTC so reviews from different offsets sort correctly.
    # NOTE: RoundtripKind must NOT be combined with AdjustToUniversal - .NET rejects that pair.
    $styles = [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal
    $ok = [datetime]::TryParse($text, [System.Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$parsed)

    if (-not $ok) { return $null }
    return $parsed
}

<#
.SYNOPSIS
    Evaluates PR reviews into the set of effective human approvals bound to a specific HEAD SHA.

.DESCRIPTION
    Semantics (fail-closed):
      * Only APPROVED / CHANGES_REQUESTED / DISMISSED change a reviewer's state.
        COMMENTED and PENDING reviews are ignored, matching GitHub's own semantics.
      * Each reviewer contributes at most ONE state: their latest relevant review.
      * A reviewer whose latest relevant state is CHANGES_REQUESTED or DISMISSED is not an
        approval. Active CHANGES_REQUESTED is reported separately so callers can hard-block.
      * Bot reviews never count as human approvals.
      * An APPROVED review only counts when its commit OID equals the current PR HEAD SHA.
        Approvals of older commits are reported as stale and do NOT count.
      * A review whose author, timestamp, or commit OID cannot be determined is reported as
        UNVERIFIED and does NOT count.

.OUTPUTS
    Hashtable with: approvals (int), approvers, changesRequested, dismissed,
    staleApprovals, botReviews, unverified.
#>
function Get-EffectiveApprovals {
    param(
        [AllowNull()]$Reviews,
        [AllowNull()][AllowEmptyString()][string]$HeadSha
    )

    $out = @{
        approvals        = 0
        approvers        = @()
        changesRequested = @()
        dismissed        = @()
        staleApprovals   = @()
        botReviews       = @()
        unverified       = @()
    }

    if ([string]::IsNullOrWhiteSpace($HeadSha)) {
        $out.unverified += 'HEAD SHA not supplied - approvals cannot be bound to PR HEAD'
        return $out
    }

    $all = @($Reviews | Where-Object { $null -ne $_ })
    if ($all.Count -eq 0) { return $out }

    $relevantStates = @('APPROVED', 'CHANGES_REQUESTED', 'DISMISSED')
    $byReviewer = @{}
    $index = 0

    foreach ($review in $all) {
        $index++

        $state = [string](Get-PropertyValue -InputObject $review -Name 'state')
        if ([string]::IsNullOrWhiteSpace($state)) {
            $out.unverified += "Review #$index has no state - not counted"
            continue
        }
        if ($relevantStates -notcontains $state) { continue }

        $author = Get-PropertyValue -InputObject $review -Name 'author'
        $login = [string](Get-PropertyValue -InputObject $author -Name 'login')

        if (Test-ReviewAuthorIsBot -Author $author) {
            if ([string]::IsNullOrWhiteSpace($login)) {
                $out.unverified += "Review #$index ($state) has an unresolvable author - not counted"
            } else {
                $out.botReviews += "$login ($state)"
            }
            continue
        }

        $submitted = ConvertTo-ReviewTimestamp -Value (Get-PropertyValue -InputObject $review -Name 'submittedAt')
        if ($null -eq $submitted) {
            $out.unverified += "$login ($state) has an unparsable submittedAt - not counted"
            continue
        }

        $commit = Get-PropertyValue -InputObject $review -Name 'commit'
        $commitOid = [string](Get-PropertyValue -InputObject $commit -Name 'oid')

        if (-not $byReviewer.ContainsKey($login)) { $byReviewer[$login] = @() }
        $byReviewer[$login] += [pscustomobject]@{
            login     = $login
            state     = $state
            submitted = $submitted
            oid       = $commitOid
            order     = $index
        }
    }

    foreach ($login in ($byReviewer.Keys | Sort-Object)) {
        $latest = @($byReviewer[$login] | Sort-Object -Property submitted, order)[-1]

        switch ($latest.state) {
            'CHANGES_REQUESTED' { $out.changesRequested += $login }
            'DISMISSED'         { $out.dismissed += $login }
            'APPROVED' {
                if ([string]::IsNullOrWhiteSpace($latest.oid)) {
                    $out.unverified += "$login APPROVED but the reviewed commit OID is unavailable - not counted"
                } elseif ($latest.oid -ne $HeadSha) {
                    $shortOid = if ($latest.oid.Length -ge 7) { $latest.oid.Substring(0, 7) } else { $latest.oid }
                    $out.staleApprovals += "$login (approved $shortOid, not current HEAD)"
                } else {
                    $out.approvals++
                    $out.approvers += $login
                }
            }
        }
    }

    return $out
}

<#
.SYNOPSIS
    Reads the approval gate settings from .kiro/merge.config.json (fail-closed).

.OUTPUTS
    Hashtable with: ok (bool), errors, notes, requiredApprovals (int), source (string).
#>
function Get-MergeApprovalConfig {
    param([Parameter(Mandatory = $true)][string]$ConfigPath)

    $cfg = @{
        ok                = $false
        errors            = @()
        notes             = @()
        requiredApprovals = 0
        source            = $ConfigPath
    }

    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        $cfg.errors += "Merge config not found: $ConfigPath"
        return $cfg
    }

    try {
        $json = Get-Content -LiteralPath $ConfigPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    } catch {
        $cfg.errors += "Failed to parse merge config: $_"
        return $cfg
    }

    $approval = Get-PropertyValue -InputObject $json -Name 'approval'
    if ($null -eq $approval) {
        $cfg.errors += "Merge config has no 'approval' section"
        return $cfg
    }

    if (-not (Test-PropertyPresent -InputObject $approval -Name 'requiredApprovals')) {
        $cfg.errors += 'Merge config approval.requiredApprovals is missing'
        return $cfg
    }

    $rawRequired = Get-PropertyValue -InputObject $approval -Name 'requiredApprovals'
    $required = 0
    if (-not [int]::TryParse([string]$rawRequired, [ref]$required)) {
        $cfg.errors += "Merge config approval.requiredApprovals is not an integer: '$rawRequired'"
        return $cfg
    }
    if ($required -lt 1) {
        $cfg.errors += "Merge config approval.requiredApprovals must be >= 1 (got $required); human approval cannot be disabled"
        return $cfg
    }
    $cfg.requiredApprovals = $required

    # requirePRApproval: ENFORCED. Phase 1 has no supported way to disable human approval.
    if (-not (Test-PropertyPresent -InputObject $approval -Name 'requirePRApproval')) {
        $cfg.errors += 'Merge config approval.requirePRApproval is missing'
        return $cfg
    }
    if ((Get-PropertyValue -InputObject $approval -Name 'requirePRApproval') -ne $true) {
        $cfg.errors += 'Merge config approval.requirePRApproval=false is NOT SUPPORTED - human approval is always required'
        return $cfg
    }

    # requireCodeOwnerApproval: NOT YET ENFORCED in Phase 1. Fail closed if it is requested,
    # rather than silently reporting a gate this script cannot actually check.
    if ((Get-PropertyValue -InputObject $approval -Name 'requireCodeOwnerApproval') -eq $true) {
        $cfg.errors += 'Merge config approval.requireCodeOwnerApproval=true is NOT YET ENFORCED (Phase 2) - refusing to report an unchecked gate as passed'
        return $cfg
    }
    $cfg.notes += 'requireCodeOwnerApproval: NOT YET ENFORCED (Phase 2); config value must remain false'

    # dismissStaleReviews: this script is unconditionally stricter than this setting.
    # Approvals are always bound to the current PR HEAD, so stale approvals never count
    # regardless of the configured value.
    $configuredDismiss = 'absent'
    if (Test-PropertyPresent -InputObject $approval -Name 'dismissStaleReviews') {
        $configuredDismiss = [string](Get-PropertyValue -InputObject $approval -Name 'dismissStaleReviews')
    }
    $cfg.notes += "dismissStaleReviews: config value ($configuredDismiss) is IGNORED - approvals are always bound to the current PR HEAD (stricter than this setting)"

    $cfg.ok = $true
    return $cfg
}
