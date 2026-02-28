$ErrorActionPreference = 'Stop'

$py = '.\.venv-1\Scripts\python.exe'
if (-not (Test-Path $py)) {
    $py = '.\.venv\Scripts\python.exe'
}

# Listing-only phase; gdown may fail while resolving links, but still prints IDs.
$natureListing = & $py -m gdown --folder --remaining-ok "https://drive.google.com/drive/folders/1uoIaSvBzm8SrC7g-feRK6ewzHVUGApE0?usp=sharing" -O "Assets/Art/Premade/Quaternius/UltimateNature/FBX" 2>&1
$cropsListing = & $py -m gdown --folder --remaining-ok "https://drive.google.com/drive/folders/1r_WpDuffiJeQ3neYTvCEb0RI-yOB_afS?usp=sharing" -O "Assets/Art/Premade/Quaternius/UltimateCrops/FBX" 2>&1

function Parse-DriveEntries {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Lines,
        [Parameter(Mandatory = $true)]
        [string]$Source
    )

    $entries = @()
    foreach ($line in $Lines) {
        if ($line -notmatch '^Processing file\s+([A-Za-z0-9_-]+)\s+(.+)$') {
            continue
        }

        $id = $Matches[1]
        $name = $Matches[2]
        $ext = [System.IO.Path]::GetExtension($name).ToLowerInvariant()

        $assetType = switch ($ext) {
            '.fbx' { 'fbx'; break }
            '.png' { 'texture'; break }
            '.jpg' { 'texture'; break }
            '.jpeg' { 'texture'; break }
            '.tga' { 'texture'; break }
            '.bmp' { 'texture'; break }
            default { $null }
        }

        if ($null -eq $assetType) {
            continue
        }

        $entries += [PSCustomObject]@{
            Id     = $id
            Name   = $name
            Source = $Source
            Type   = $assetType
        }
    }

    return $entries
}

$entries = @()
$entries += Parse-DriveEntries -Lines @($natureListing) -Source 'nature'
$entries += Parse-DriveEntries -Lines @($cropsListing) -Source 'crops'

$unique = $entries | Group-Object { "{0}:{1}" -f $_.Id, $_.Name } | ForEach-Object { $_.Group[0] }

$fbxEntries = @($unique | Where-Object { $_.Type -eq 'fbx' })
$textureEntries = @($unique | Where-Object { $_.Type -eq 'texture' })
Write-Output ("Parsed asset entries: total={0} fbx={1} textures={2}" -f $unique.Count, $fbxEntries.Count, $textureEntries.Count)

$natureRoot = 'Assets/Art/Premade/Quaternius/UltimateNature/FBX'
$cropsRoot = 'Assets/Art/Premade/Quaternius/UltimateCrops/FBX'
$natureTextureRoot = 'Assets/Art/Premade/Quaternius/UltimateNature/Textures'
$cropsTextureRoot = 'Assets/Art/Premade/Quaternius/UltimateCrops/Textures'
New-Item -ItemType Directory -Force -Path $natureRoot, $cropsRoot, $natureTextureRoot, $cropsTextureRoot | Out-Null

foreach ($m in $unique) {
    if ($m.Type -eq 'texture') {
        $targetRoot = if ($m.Source -eq 'nature') { $natureTextureRoot } else { $cropsTextureRoot }
    }
    else {
        $targetRoot = if ($m.Source -eq 'nature') { $natureRoot } else { $cropsRoot }
    }

    $outPath = Join-Path $targetRoot $m.Name
    if (Test-Path $outPath) { continue }

    $url = "https://drive.google.com/uc?export=download&id=$($m.Id)"
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $outPath
    }
    catch {
        Write-Output ("FAILED {0} {1} ({2})" -f $m.Id, $m.Name, $m.Type)
    }
}

$natureCount = (Get-ChildItem $natureRoot -File -Filter *.fbx -ErrorAction SilentlyContinue).Count
$cropsCount = (Get-ChildItem $cropsRoot -File -Filter *.fbx -ErrorAction SilentlyContinue).Count
$natureTextureCount = @(Get-ChildItem $natureTextureRoot -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -match '^\.(png|jpg|jpeg|tga|bmp)$' }).Count
$cropsTextureCount = @(Get-ChildItem $cropsTextureRoot -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -match '^\.(png|jpg|jpeg|tga|bmp)$' }).Count
Write-Output ("Downloaded counts :: UltimateNature_fbx={0} UltimateCrops_fbx={1} UltimateNature_tex={2} UltimateCrops_tex={3}" -f $natureCount, $cropsCount, $natureTextureCount, $cropsTextureCount)
