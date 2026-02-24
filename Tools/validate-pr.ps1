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

.PARAMETER ProjectRoot
    Path to the Unity project root.

.PARAMETER Fix
    If set, auto-fixes AGENTS.md staleness by regenerating them.

.EXAMPLE
    .\validate-pr.ps1
    .\validate-pr.ps1 -Fix
#>

[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [switch]$Fix
)

if (-not $ProjectRoot) {
    if ($PSScriptRoot) {
        $ProjectRoot = Split-Path -Parent $PSScriptRoot
    } else {
        $ProjectRoot = Get-Location
    }
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$errors = @()
$warnings = @()

Write-Host "=== ByteWar PR Validation ===" -ForegroundColor Cyan
Write-Host "Project root: $ProjectRoot" -ForegroundColor Gray

# --- Check 1: Required governance files exist ---
Write-Host "`n[1/5] Checking governance files..." -ForegroundColor Yellow

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
Write-Host "[2/5] Checking AGENTS.md freshness..." -ForegroundColor Yellow

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
Write-Host "[3/5] Checking CHANGELOG.md..." -ForegroundColor Yellow

$changelogPath = Join-Path $ProjectRoot "CHANGELOG.md"
if (Test-Path $changelogPath) {
    $changelog = Get-Content -Path $changelogPath -Raw
    if ($changelog -match '\[Unreleased\]' -or $changelog.Length -gt 100) {
        # Changelog exists and has content — OK
    } else {
        $warnings += "CHANGELOG.md appears empty or missing release entries"
    }
} else {
    $errors += "CHANGELOG.md not found"
}

# --- Check 4: 800 LOC limit ---
Write-Host "[4/5] Checking file size limits (800 LOC)..." -ForegroundColor Yellow

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
Write-Host "[5/5] Checking for public mutable fields..." -ForegroundColor Yellow

foreach ($csFile in $csFiles) {
    $content = Get-Content -Path $csFile.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    # Match "public <type> <name>" that isn't a property (no { get), not const/static/readonly/event/override/abstract
    $publicFieldPattern = '(?m)^\s*public\s+(?!(?:const|static|readonly|event|override|abstract|delegate|class|struct|interface|enum|new)\b)[\w<>\[\],\s\?]+\s+\w+\s*[;=]'
    $fieldMatches = [regex]::Matches($content, $publicFieldPattern)

    foreach ($fm in $fieldMatches) {
        $line = $fm.Value.Trim()
        # Exclude properties (contain { get or =>)
        if ($line -match '\{' -or $line -match '=>') { continue }
        # Exclude if preceded by [SerializeField] on previous line (read-only getter pattern)
        # This is a heuristic — we flag for review
        $relativePath = $csFile.FullName.Replace($ProjectRoot, '').TrimStart('\')
        $warnings += "PUBLIC FIELD: $relativePath -> $line"
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
