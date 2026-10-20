<#
.SYNOPSIS
    Runs the SVF.NET C# test suite natively on Windows.
.DESCRIPTION
    Discovers the local dotnet runtime, builds the tests, and executes
    the test suite against local Windows binaries (wpa.exe).
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$BitcodePath = ""
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..\..\..")

Write-Host "=================================================="
Write-Host " Running SVF.NET Tests on Windows"
Write-Host " Project Root : $ProjectRoot"
Write-Host " Configuration: $Configuration"
Write-Host "=================================================="

# Locate dotnet executable
$cmd = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe = if ($cmd) { $cmd.Source } else { $null }
if (-not $dotnetExe -and (Test-Path "C:\Program Files\dotnet\dotnet.exe")) {
    $dotnetExe = "C:\Program Files\dotnet\dotnet.exe"
}

if (-not $dotnetExe) {
    Write-Error "CRITICAL: dotnet executable not found on system PATH or Program Files."
    exit 1
}

$testDll = Join-Path $ScriptDir "bin\$Configuration\net8.0\SVF.NET.Tests.dll"

# Resolve default bitcode path if not specified
if (-not $BitcodePath) {
    $defaultBc = Join-Path $ProjectRoot "Test-Suite\test_cases_bc\basic_c_tests\struct-nested-array3.c.bc"
    if (Test-Path $defaultBc) {
        $BitcodePath = $defaultBc
    }
}

$testArgs = @($testDll)
if ($BitcodePath) {
    $testArgs += $BitcodePath
}

Write-Host "Executing: $dotnetExe $($testArgs -join ' ')"
& $dotnetExe @testArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "SVF.NET Windows tests FAILED with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

Write-Host "SVF.NET Windows tests completed successfully."
exit 0
