<#
.SYNOPSIS
    Auto-generates AGENTS.md files for each folder under Assets/Scripts/ based on code analysis.

.DESCRIPTION
    Scans every subfolder of Assets/Scripts/ (excluding Tests), extracts class names, base classes,
    implemented interfaces, NetworkBehaviour usage, and RPC methods, then generates an AGENTS.md file.
    Existing hand-written sections (## Invariants, ## Tests, ## UX correctness, etc.) are preserved.

.PARAMETER ProjectRoot
    Path to the Unity project root. Defaults to the repo root relative to this script.

.PARAMETER DryRun
    If set, prints generated content without writing files.

.EXAMPLE
    .\generate-agents-md.ps1
    .\generate-agents-md.ps1 -DryRun
    .\generate-agents-md.ps1 -ProjectRoot "C:\Projects\SurvivalRPG"
#>

[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [switch]$DryRun
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

$scriptsRoot = Join-Path $ProjectRoot "Assets\Scripts"

if (-not (Test-Path $scriptsRoot)) {
    Write-Error "Scripts root not found: $scriptsRoot"
    return
}

function Get-CSharpMetadata {
    param([string]$FilePath)

    $content = Get-Content -Path $FilePath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return $null }

    $results = @()

    # Match class/struct declarations with optional base class and interfaces
    $classPattern = '(?:public|internal|private|protected)\s+(?:abstract\s+|sealed\s+|static\s+|partial\s+)*(?:class|struct)\s+(\w+)(?:<[^>]+>)?(?:\s*:\s*(.+?))?(?:\s*\{|\s*where)'
    $matches = [regex]::Matches($content, $classPattern)

    foreach ($m in $matches) {
        $className = $m.Groups[1].Value
        $inheritance = if ($m.Groups[2].Success) { $m.Groups[2].Value.Trim() } else { "" }

        $baseClass = ""
        $interfaces = @()

        if ($inheritance) {
            $parts = $inheritance -split ',' | ForEach-Object { $_.Trim() }
            foreach ($part in $parts) {
                # Clean up generic constraints
                $clean = ($part -split '\s+where\s+')[0].Trim()
                if (-not $clean) { continue }
                if ($clean -match '^I[A-Z]') {
                    $interfaces += $clean
                } elseif (-not $baseClass) {
                    $baseClass = $clean
                } else {
                    $interfaces += $clean
                }
            }
        }

        $isNetworkBehaviour = ($baseClass -eq 'NetworkBehaviour') -or ($baseClass -eq 'NetworkAnimator')
        $hasServerRpc = [regex]::IsMatch($content, '\[ServerRpc[^\]]*\]')
        $hasClientRpc = [regex]::IsMatch($content, '\[ClientRpc[^\]]*\]')

        $rpcMethods = @()
        if ($hasServerRpc) {
            $serverRpcs = [regex]::Matches($content, '(?:public|private|protected)\s+void\s+(\w+ServerRpc)\s*\(')
            foreach ($rpc in $serverRpcs) { $rpcMethods += $rpc.Groups[1].Value }
        }
        if ($hasClientRpc) {
            $clientRpcs = [regex]::Matches($content, '(?:public|private|protected)\s+void\s+(\w+ClientRpc)\s*\(')
            foreach ($rpc in $clientRpcs) { $rpcMethods += $rpc.Groups[1].Value }
        }

        $results += [PSCustomObject]@{
            ClassName       = $className
            BaseClass       = $baseClass
            Interfaces      = $interfaces
            IsNetworkBehaviour = $isNetworkBehaviour
            RpcMethods      = $rpcMethods
            FileName        = (Split-Path -Leaf $FilePath)
        }
    }

    return $results
}

function Get-PreservedSections {
    param([string]$AgentsMdPath)

    if (-not (Test-Path $AgentsMdPath)) { return @{} }

    $content = Get-Content -Path $AgentsMdPath -Raw
    $sections = @{}

    # Match ## sections that are hand-written (Invariants, Tests, UX correctness, Rule, etc.)
    $sectionPattern = '(?m)^(## (?:Invariants|Tests|UX correctness|Rule|Legality|Performance|Required validations)[^\n]*)\n([\s\S]*?)(?=\n## |\z)'
    $matches = [regex]::Matches($content, $sectionPattern)

    foreach ($m in $matches) {
        $header = $m.Groups[1].Value.Trim()
        $body = $m.Groups[2].Value.TrimEnd()
        $sections[$header] = $body
    }

    return $sections
}

function Format-AgentsMd {
    param(
        [string]$FolderName,
        [PSCustomObject[]]$Classes,
        [hashtable]$PreservedSections
    )

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine(('# {0} - Agent Guidance' -f $FolderName))
    [void]$sb.AppendLine("")

    # Auto-generated class table
    if ($Classes -and $Classes.Count -gt 0) {
        [void]$sb.AppendLine("## Key classes (auto-generated)")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine('| Class | Base | Interfaces | Network | RPCs |')
        [void]$sb.AppendLine('|-------|------|-----------|---------|------|')

        foreach ($cls in $Classes) {
            $ifaces = if ($cls.Interfaces.Count -gt 0) { ($cls.Interfaces -join ', ') } else { '-' }
            $net = if ($cls.IsNetworkBehaviour) { 'Yes' } else { '-' }
            $rpcs = if ($cls.RpcMethods.Count -gt 0) { ($cls.RpcMethods -join ', ') } else { '-' }
            $row = '| `{0}` | `{1}` | {2} | {3} | {4} |' -f $cls.ClassName, $cls.BaseClass, $ifaces, $net, $rpcs
            [void]$sb.AppendLine($row)
        }
        [void]$sb.AppendLine("")
    }

    # Preserved hand-written sections
    foreach ($key in ($PreservedSections.Keys | Sort-Object)) {
        [void]$sb.AppendLine($key)
        [void]$sb.AppendLine($PreservedSections[$key])
        [void]$sb.AppendLine("")
    }

    return $sb.ToString().TrimEnd() + "`n"
}

# Get all script subfolders (depth 1 only, excluding Tests)
$folders = Get-ChildItem -Path $scriptsRoot -Directory | Where-Object { $_.Name -ne 'Tests' }

$generated = 0
$skipped = 0

foreach ($folder in $folders) {
    $csFiles = Get-ChildItem -Path $folder.FullName -Filter '*.cs' -Recurse -File
    if ($csFiles.Count -eq 0) {
        Write-Verbose "Skipping empty folder: $($folder.Name)"
        $skipped++
        continue
    }

    $allClasses = @()
    foreach ($csFile in $csFiles) {
        $meta = Get-CSharpMetadata -FilePath $csFile.FullName
        if ($meta) { $allClasses += $meta }
    }

    $agentsMdPath = Join-Path $folder.FullName "AGENTS.md"
    $preserved = Get-PreservedSections -AgentsMdPath $agentsMdPath

    $output = Format-AgentsMd -FolderName $folder.Name -Classes $allClasses -PreservedSections $preserved

    if ($DryRun) {
        Write-Host "=== $($folder.Name)/AGENTS.md ===" -ForegroundColor Cyan
        Write-Host $output
    } else {
        Set-Content -Path $agentsMdPath -Value $output -Encoding UTF8 -NoNewline
        Write-Host "[OK] $($folder.Name)/AGENTS.md" -ForegroundColor Green
    }
    $generated++
}

Write-Host "`nGenerated: $generated, Skipped: $skipped" -ForegroundColor Yellow
