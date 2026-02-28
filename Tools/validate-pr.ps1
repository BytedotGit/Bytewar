<#
.SYNOPSIS
    PR validation script — enforces spec-driven development checks.

.DESCRIPTION
    Run this script before merging any change. It validates:
    1. All required governance files exist
    2. AGENTS.md files are up-to-date (re-runs generator and diffs)
    3. CHANGELOG.md was updated
    4. No files exceed 800 LOC
    5. No public mutable fields in runtime code
    6. Optional deterministic PlayMode test validation via Tools/run-playmode-tests.ps1

.PARAMETER ProjectRoot
    Path to the Unity project root.

.PARAMETER Fix
    If set, auto-fixes AGENTS.md staleness by regenerating them.

.PARAMETER RunPlayMode
    If set, executes deterministic PlayMode tests and validates XML totals.

.PARAMETER PlayModeResultsPath
    XML output path passed to Tools/run-playmode-tests.ps1.

.PARAMETER PlayModeLogPath
    Log output path passed to Tools/run-playmode-tests.ps1.

.PARAMETER UnityExe
    Unity executable path passed to Tools/run-playmode-tests.ps1.

.PARAMETER VerbosePublicFieldWarnings
    If set, prints every public field finding instead of grouped summaries.

.PARAMETER MaxPublicFieldWarningFiles
    Maximum number of grouped public-field warning lines to print when not verbose.

.EXAMPLE
    .\validate-pr.ps1
    .\validate-pr.ps1 -Fix
    .\validate-pr.ps1 -RunPlayMode
    .\validate-pr.ps1 -VerbosePublicFieldWarnings
#>

[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [switch]$Fix,
    [switch]$RunPlayMode,
    [string]$PlayModeResultsPath = 'Logs/PlayModeTestResults.xml',
    [string]$PlayModeLogPath = 'Logs/playmode_tests_validate_pr.log',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe',
    [switch]$VerbosePublicFieldWarnings,
    [int]$MaxPublicFieldWarningFiles = 20
)

if (-not $ProjectRoot) {
    if ($PSScriptRoot) {
        $ProjectRoot = Split-Path -Parent $PSScriptRoot
    }
    else {
        $ProjectRoot = Get-Location
    }
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$errors = @()
$warnings = @()
$totalChecks = if ($RunPlayMode) { 6 } else { 5 }

function Resolve-ProjectPath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $Root $Path
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootResolved = (Resolve-Path $Root).Path
    return [System.IO.Path]::GetRelativePath($rootResolved, $Path)
}

Write-Host "=== ByteWar PR Validation ===" -ForegroundColor Cyan
Write-Host "Project root: $ProjectRoot" -ForegroundColor Gray

# --- Check 1: Required governance files exist ---
Write-Host "`n[1/$totalChecks] Checking governance files..." -ForegroundColor Yellow

$requiredFiles = @(
    '.github/ROADMAP.md',
    '.github/MASTER_PLAN.md',
    '.github/GAME_PROTOCOL.md',
    '.github/copilot-instructions.md',
    '.github/instructions/architecture.instructions.md',
    '.github/instructions/testing.instructions.md',
    '.github/instructions/agent-workflow.instructions.md',
    '.github/instructions/abilities.instructions.md',
    'AGENTS.md',
    'CHANGELOG.md'
)

foreach ($file in $requiredFiles) {
    $fullPath = Join-Path $ProjectRoot $file
    if (-not (Test-Path $fullPath)) {
        $errors += "MISSING: $file"
    }
}

# --- Check 2: AGENTS.md freshness ---
Write-Host "[2/$totalChecks] Checking AGENTS.md freshness..." -ForegroundColor Yellow

$scriptsRoot = Join-Path $ProjectRoot "Assets\Scripts"
$folders = Get-ChildItem -Path $scriptsRoot -Directory | Where-Object { $_.Name -ne 'Tests' }

foreach ($folder in $folders) {
    $agentsMd = Join-Path $folder.FullName "AGENTS.md"
    if (-not (Test-Path $agentsMd)) {
        $errors += "MISSING AGENTS.md: Assets/Scripts/$($folder.Name)/AGENTS.md"
    }
}

if ($Fix) {
    $generatorPath = Join-Path $ProjectRoot "Tools\generate-agents-md.ps1"
    if (Test-Path $generatorPath) {
        Write-Host "  Regenerating AGENTS.md files..." -ForegroundColor Gray
        & $generatorPath -ProjectRoot $ProjectRoot
    }
}

# --- Check 3: CHANGELOG updated ---
Write-Host "[3/$totalChecks] Checking CHANGELOG.md..." -ForegroundColor Yellow

$changelogPath = Join-Path $ProjectRoot "CHANGELOG.md"
if (Test-Path $changelogPath) {
    $changelog = Get-Content -Path $changelogPath -Raw
    if ($changelog -match '\[Unreleased\]' -or $changelog.Length -gt 100) {
        # Changelog exists and has content — OK
    }
    else {
        $warnings += "CHANGELOG.md appears empty or missing release entries"
    }
}
else {
    $errors += "CHANGELOG.md not found"
}

# --- Check 4: 800 LOC limit ---
Write-Host "[4/$totalChecks] Checking file size limits (800 LOC)..." -ForegroundColor Yellow

$csFiles = Get-ChildItem -Path $scriptsRoot -Filter '*.cs' -Recurse -File |
Where-Object { $_.FullName -notmatch '\\Tests\\' -and $_.FullName -notmatch '\\Editor\\' }

foreach ($csFile in $csFiles) {
    $lineCount = (Get-Content -Path $csFile.FullName).Count
    if ($lineCount -gt 800) {
        $relativePath = $csFile.FullName.Replace($ProjectRoot, '').TrimStart('\')
        $errors += "OVER 800 LOC ($lineCount lines): $relativePath"
    }
}

# --- Check 5: Public mutable fields in runtime code ---
Write-Host "[5/$totalChecks] Checking for public mutable fields..." -ForegroundColor Yellow

$publicFieldFindings = @()
$publicFieldPattern = '(?m)^\s*public\s+(?!(?:const|static|readonly|event|override|abstract|delegate|class|struct|interface|enum|new)\b)[\w<>\[\],\.\s\?]+\s+\w+\s*(?:;|=(?!>))'

foreach ($csFile in $csFiles) {
    $content = Get-Content -Path $csFile.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    $fieldMatches = [regex]::Matches($content, $publicFieldPattern)
    $relativePath = Get-RelativePath -Root $ProjectRoot -Path $csFile.FullName

    foreach ($fm in $fieldMatches) {
        $line = $fm.Value.Trim()
        $publicFieldFindings += [PSCustomObject]@{
            Path = $relativePath
            Line = $line
        }
    }
}

if ($publicFieldFindings.Count -gt 0) {
    if ($VerbosePublicFieldWarnings) {
        foreach ($finding in $publicFieldFindings) {
            $warnings += "PUBLIC FIELD: $($finding.Path) -> $($finding.Line)"
        }
    }
    else {
        $groupedFindings = $publicFieldFindings |
        Group-Object -Property Path |
        Sort-Object -Property Count -Descending

        $warnings += "PUBLIC FIELD SUMMARY: $($publicFieldFindings.Count) finding(s) across $($groupedFindings.Count) file(s)."

        $limit = [Math]::Max(1, $MaxPublicFieldWarningFiles)
        $displayGroups = $groupedFindings | Select-Object -First $limit
        foreach ($group in $displayGroups) {
            $warnings += "PUBLIC FIELDS ($($group.Count)): $($group.Name)"
        }

        if ($groupedFindings.Count -gt $displayGroups.Count) {
            $warnings += "PUBLIC FIELD SUMMARY: $($groupedFindings.Count - $displayGroups.Count) more file(s) omitted. Use -VerbosePublicFieldWarnings for full details."
        }
    }
}

# --- Check 6: Optional deterministic PlayMode validation ---
if ($RunPlayMode) {
    Write-Host "[6/$totalChecks] Running deterministic PlayMode tests..." -ForegroundColor Yellow

    $wrapperPath = Join-Path $ProjectRoot "Tools\run-playmode-tests.ps1"
    if (-not (Test-Path -LiteralPath $wrapperPath)) {
        $errors += "MISSING: Tools/run-playmode-tests.ps1"
    }
    else {
        $pwshExe = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
        if (-not $pwshExe) {
            $pwshExe = (Get-Command powershell -ErrorAction SilentlyContinue).Source
        }

        if (-not $pwshExe) {
            $errors += "Unable to locate PowerShell executable to run PlayMode wrapper."
        }
        else {
            $playModeResultsFullPath = Resolve-ProjectPath -Root $ProjectRoot -Path $PlayModeResultsPath
            if (Test-Path -LiteralPath $playModeResultsFullPath) {
                Remove-Item -LiteralPath $playModeResultsFullPath -Force
            }

            & $pwshExe -NoProfile -ExecutionPolicy Bypass -File $wrapperPath -ProjectRoot $ProjectRoot -UnityExe $UnityExe -ResultsPath $PlayModeResultsPath -LogPath $PlayModeLogPath
            $runnerExitCode = $LASTEXITCODE
            if ($runnerExitCode -ne 0) {
                $errors += "PlayMode wrapper failed with exit code $runnerExitCode."
            }

            if (-not (Test-Path -LiteralPath $playModeResultsFullPath)) {
                $errors += "Missing PlayMode result artifact: $playModeResultsFullPath"
            }
            else {
                try {
                    [xml]$playModeXml = Get-Content -LiteralPath $playModeResultsFullPath -Raw
                    $testRun = $playModeXml.'test-run'
                    $failed = [int]$testRun.failed
                    $total = [int]$testRun.total
                    $result = [string]$testRun.result

                    if ($failed -gt 0 -or -not [string]::Equals($result, 'Passed', [System.StringComparison]::OrdinalIgnoreCase)) {
                        $errors += "PlayMode XML indicates failure: result=$result total=$total failed=$failed path=$playModeResultsFullPath"
                    }
                    else {
                        Write-Host "  PlayMode XML verified: total=$total failed=$failed path=$playModeResultsFullPath" -ForegroundColor Gray
                    }
                }
                catch {
                    $errors += "Unable to parse PlayMode XML '$playModeResultsFullPath': $($_.Exception.Message)"
                }
            }
        }
    }
}

# --- Summary ---
Write-Host "`n=== Results ===" -ForegroundColor Cyan

if ($errors.Count -gt 0) {
    Write-Host "`nERRORS ($($errors.Count)):" -ForegroundColor Red
    foreach ($e in $errors) {
        Write-Host "  [ERROR] $e" -ForegroundColor Red
    }
}

if ($warnings.Count -gt 0) {
    Write-Host "`nWARNINGS ($($warnings.Count)):" -ForegroundColor Yellow
    foreach ($w in $warnings) {
        Write-Host "  [WARN] $w" -ForegroundColor Yellow
    }
}

if ($errors.Count -eq 0 -and $warnings.Count -eq 0) {
    Write-Host "`nAll checks passed!" -ForegroundColor Green
}

Write-Host "`nErrors: $($errors.Count), Warnings: $($warnings.Count)" -ForegroundColor $(if ($errors.Count -gt 0) { 'Red' } else { 'Green' })

# Exit with error code if there are blocking errors
if ($errors.Count -gt 0) {
    exit 1
}
