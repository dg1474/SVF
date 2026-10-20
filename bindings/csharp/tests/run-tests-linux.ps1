<#
.SYNOPSIS
    Runs the SVF.NET C# test suite in a containerized Linux environment using Docker.
.DESCRIPTION
    Validates SVF.NET behavior under Debian/Ubuntu Linux using official Microsoft .NET SDK images.
    Tests runtime detection (linux-x64), binary resolution (wpa without .exe),
    permission handling (chmod +x / File.SetUnixFileMode), and process execution.
#>
[CmdletBinding()]
param(
    [ValidateSet("8.0", "9.0", "all")]
    [string]$DotnetVersion = "all"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = (Resolve-Path (Join-Path $ScriptDir "..\..\..")).Path

Write-Host "=================================================="
Write-Host " Running SVF.NET Tests in Linux Environment (Docker)"
Write-Host " Project Root   : $ProjectRoot"
Write-Host " Target .NET Ver: $DotnetVersion"
Write-Host "=================================================="

# Check Docker availability
$dockerVersion = docker --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "CRITICAL: Docker is not running or not installed. Required for Linux testing."
    exit 1
}
Write-Host "Docker found: $dockerVersion"

$versionsToTest = @()
if ($DotnetVersion -eq "8.0" -or $DotnetVersion -eq "all") {
    $versionsToTest += "8.0"
}
if ($DotnetVersion -eq "9.0" -or $DotnetVersion -eq "all") {
    $versionsToTest += "9.0"
}

$workspaceMount = "${ProjectRoot}:/workspace"

foreach ($ver in $versionsToTest) {
    $image = "mcr.microsoft.com/dotnet/sdk:$ver"
    Write-Host "`n--- Testing on Linux with .NET $ver ($image) ---"

    docker run --rm -v $workspaceMount -w /workspace $image bash /workspace/bindings/csharp/tests/run-tests-linux.sh
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Linux test suite FAILED on .NET $ver with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
    Write-Host "Linux test suite PASSED on .NET $ver."
}

Write-Host "`n=================================================="
Write-Host " All Linux container tests passed successfully."
Write-Host "=================================================="
exit 0
