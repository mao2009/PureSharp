# PureSharp Merge Skill - Build and Test Verification Script
# This script verifies build and test status for merge gates
# Usage: .\verify-build-and-tests.ps1 [-Configuration Release] [-Format json|text]

param(
    [string]$Configuration = "Release",
    [string]$Format = "text",
    [switch]$Verbose
)

# Initialize result object
$result = @{
    timestamp = Get-Date -Format "o"
    build = @{}
    tests = @()
    verified = $true
    errors = @()
}

# Helper function
function Add-TestResult {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Output
    )
    $result.tests += @{
        name = $Name
        passed = $Passed
        output = $Output
        timestamp = Get-Date -Format "o"
    }
}

function Add-Error {
    param([string]$Message)
    $result.errors += $Message
    $result.verified = $false
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

# 1. Build Verification
Write-Host "Verifying build..." -ForegroundColor Cyan

try {
    Write-Host "Running: dotnet build --configuration $Configuration"

    $buildOutput = & dotnet build --configuration $Configuration 2>&1
    $buildExitCode = $LASTEXITCODE

    if ($buildExitCode -eq 0) {
        $result.build.status = "SUCCESS"
        $result.build.exitCode = 0
        Write-Host "✓ Build succeeded" -ForegroundColor Green
    } else {
        $result.build.status = "FAILED"
        $result.build.exitCode = $buildExitCode
        Add-Error "Build failed with exit code $buildExitCode"
    }

    $result.build.output = $buildOutput -join "`n"
} catch {
    Add-Error "Exception during build: $_"
    $result.build.status = "ERROR"
}

# 2. Test Verification
Write-Host "Verifying tests..." -ForegroundColor Cyan

try {
    Write-Host "Running: dotnet test --configuration $Configuration --no-build"

    $testOutput = & dotnet test --configuration $Configuration --no-build --verbosity normal 2>&1
    $testExitCode = $LASTEXITCODE

    if ($testExitCode -eq 0) {
        Write-Host "✓ Tests passed" -ForegroundColor Green
        $result.tests += @{
            name = "Unit Tests"
            passed = $true
            output = $testOutput -join "`n"
            timestamp = Get-Date -Format "o"
        }
    } else {
        Write-Host "✗ Tests failed" -ForegroundColor Red
        Add-Error "Tests failed with exit code $testExitCode"
        $result.tests += @{
            name = "Unit Tests"
            passed = $false
            output = $testOutput -join "`n"
            timestamp = Get-Date -Format "o"
        }
    }
} catch {
    Add-Error "Exception during test: $_"
}

# 3. Analyzer-specific verification
Write-Host "Verifying analyzer-specific tests..." -ForegroundColor Cyan

# Parse test output for specific analyzers
if ($result.tests.Count -gt 0) {
    $testOutput = $result.tests[0].output

    # Look for test result patterns
    $passed = $testOutput | Select-String "passed"
    $failed = $testOutput | Select-String "failed"

    if ($passed) {
        Write-Host "✓ Analyzer tests: $passed" -ForegroundColor Green
        $result.testSummary = @{
            passed = $passed
            failed = $failed
        }
    }
}

# 4. Package verification (if Release build)
if ($Configuration -eq "Release") {
    Write-Host "Verifying package creation..." -ForegroundColor Cyan

    try {
        Write-Host "Running: dotnet pack"

        $packOutput = & dotnet pack src/PureSharp.Core/PureSharp.Core.csproj --configuration $Configuration -o out --no-build 2>&1
        $packExitCode = $LASTEXITCODE

        if ($packExitCode -eq 0) {
            Write-Host "✓ Package created successfully" -ForegroundColor Green
            $result.build.packageCreated = $true
        } else {
            Write-Host "✗ Package creation failed" -ForegroundColor Red
            Add-Error "Package creation failed with exit code $packExitCode"
            $result.build.packageCreated = $false
        }
    } catch {
        Add-Error "Exception during package creation: $_"
    }
}

# 5. Summary
Write-Host "`nSummary" -ForegroundColor Cyan
Write-Host "Build: $($result.build.status)" -ForegroundColor $(if ($result.build.status -eq "SUCCESS") { "Green" } else { "Red" })
Write-Host "Tests: $($result.tests.Count) test suite(s)" -ForegroundColor Gray

if ($result.verified) {
    Write-Host "✓ Build and tests verified successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Build or test verification failed" -ForegroundColor Red
}

# Output format
if ($Format -eq "json") {
    $result | ConvertTo-Json -Depth 10 | Write-Output
} else {
    Write-Host "`nDetailed Report:" -ForegroundColor Cyan
    Write-Host "Build Status: $($result.build.status)" -ForegroundColor Gray
    Write-Host "Build Exit Code: $($result.build.exitCode)" -ForegroundColor Gray
    Write-Host "Tests Passed: $($result.tests | Where-Object { $_.passed } | Measure-Object | Select-Object -ExpandProperty Count)" -ForegroundColor Gray
}

# Return exit code based on verification
exit ($result.verified ? 0 : 1)
