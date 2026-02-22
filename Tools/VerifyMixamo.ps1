Param(
    [string]$Root = "Assets/Art/Characters/Mixamo"
)

$required = @(
    "Character.fbx",
    "Idle.fbx",
    "Walking.fbx",
    "Running.fbx",
    "Jump.fbx",
    "Attack.fbx"
)

$missing = @()
foreach ($f in $required) {
    $p = Join-Path $Root $f
    if (-not (Test-Path $p)) {
        $missing += $p
    }
}

if ($missing.Count -gt 0) {
    Write-Host "Mixamo local assets missing:" -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host " - $_" }
    Write-Host "\nFix: import the FBX files into $Root (see README.md in that folder)." -ForegroundColor Yellow
    exit 1
}

Write-Host "Mixamo local assets present (OK)." -ForegroundColor Green
exit 0
