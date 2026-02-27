<#
.SYNOPSIS
    Runs Unity PlayMode tests and guarantees a deterministic XML results artifact path.

.DESCRIPTION
    Unity occasionally ignores the requested -testResults output path in some environments
    and writes to the default PlayModeTestResults.xml at project root instead. This wrapper
    executes PlayMode tests, then enforces artifact determinism by copying the default result
    file to the requested destination when needed.

.PARAMETER ProjectRoot
    Path to the Unity project root. Defaults to repository root inferred from script location.

.PARAMETER UnityExe
    Full path to Unity Editor executable.

.PARAMETER ResultsPath
    Desired XML results path (absolute or project-relative).

.PARAMETER LogPath
    Desired Unity log path (absolute or project-relative).
#>

[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe',
    [string]$ResultsPath = 'PlayModeTestResults.xml',
    [string]$LogPath = 'Logs/playmode_tests.log'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoPath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $Root $Path
}

if (-not $ProjectRoot) {
    if ($PSScriptRoot) {
        $ProjectRoot = Split-Path -Parent $PSScriptRoot
    }
    else {
        $ProjectRoot = (Get-Location).Path
    }
}

$ProjectRoot = (Resolve-Path $ProjectRoot).Path

if (-not (Test-Path -LiteralPath $UnityExe)) {
    Write-Error "Unity executable not found: $UnityExe"
}

$resultsFullPath = Resolve-RepoPath -Root $ProjectRoot -Path $ResultsPath
$logFullPath = Resolve-RepoPath -Root $ProjectRoot -Path $LogPath
$defaultResultsPath = Join-Path $ProjectRoot 'PlayModeTestResults.xml'
$resultUsesDefaultPath = [string]::Equals($resultsFullPath, $defaultResultsPath, [System.StringComparison]::OrdinalIgnoreCase)

$resultsDirectory = Split-Path -Parent $resultsFullPath
$logDirectory = Split-Path -Parent $logFullPath

if ($resultsDirectory -and -not (Test-Path -LiteralPath $resultsDirectory)) {
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
}

if ($logDirectory -and -not (Test-Path -LiteralPath $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

if (Test-Path -LiteralPath $resultsFullPath) {
    Remove-Item -LiteralPath $resultsFullPath -Force
}

if (-not $resultUsesDefaultPath -and (Test-Path -LiteralPath $defaultResultsPath)) {
    Remove-Item -LiteralPath $defaultResultsPath -Force
}

Write-Host "=== ByteWar PlayMode Test Runner ===" -ForegroundColor Cyan
Write-Host "Project root: $ProjectRoot" -ForegroundColor Gray
Write-Host "Results path: $resultsFullPath" -ForegroundColor Gray
Write-Host "Log path: $logFullPath" -ForegroundColor Gray

$unityArgs = @(
    '-runTests',
    '-projectPath', $ProjectRoot,
    '-testPlatform', 'PlayMode',
    '-testResults', $resultsFullPath,
    '-batchmode',
    '-nographics',
    '-logFile', $logFullPath
)

$unityProcess = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru
$unityProcess.WaitForExit()
$unityExitCode = $unityProcess.ExitCode

$artifactPath = $null
if (Test-Path -LiteralPath $resultsFullPath) {
    $artifactPath = $resultsFullPath
}
elseif (Test-Path -LiteralPath $defaultResultsPath) {
    Copy-Item -LiteralPath $defaultResultsPath -Destination $resultsFullPath -Force
    $artifactPath = $resultsFullPath
    Write-Warning "Unity wrote default PlayModeTestResults.xml; copied it to requested path for deterministic artifacts."
}

if (-not $artifactPath) {
    Write-Error "No PlayMode XML result file was produced. See log: $logFullPath"
    exit 2
}

try {
    [xml]$resultXml = Get-Content -LiteralPath $artifactPath -Raw
    $testRun = $resultXml.'test-run'
    Write-Host "PlayMode totals: total=$($testRun.total) passed=$($testRun.passed) failed=$($testRun.failed) skipped=$($testRun.skipped)" -ForegroundColor Green
}
catch {
    Write-Warning "Failed to parse XML summary from '$artifactPath': $($_.Exception.Message)"
}

if ($unityExitCode -ne 0) {
    Write-Error "Unity PlayMode run exited with code $unityExitCode."
    exit $unityExitCode
}

Write-Host "PlayMode run completed successfully. Deterministic XML artifact: $artifactPath" -ForegroundColor Green
exit 0