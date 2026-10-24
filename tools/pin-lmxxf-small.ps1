#Requires -Version 7
<#
.SYNOPSIS
  Pins lmxxf 0.44's Small model (native-game-tiled-assets\small\) in one OptiScaler release of payload.json, and
  builds the archive that gets uploaded.

.DESCRIPTION
  -From is the small folder as a game receives it, inside native-game-tiled-assets\: model-manifest.json and the 93
  weights it lists, made from NVIDIA's nvngx_dlssnr.dll 310.9.16 by neural-amd-opti's tools\make-lmxxf-small.py. The
  manifest names each file with its SHA-256, so every one is checked against it, and the model and source DLL
  against what lmxxf's runtime accepts (Development\HIP\small_model_assets.h). What this does:

    1. writes <Out>\lmxxf-weights-small-<weights version>.zip, every file as native-game-tiled-assets.small/<name>, in
       ordinal order and with a fixed timestamp, so the same files make the same archive on any machine. The dot
       stands for the folder below (Work.OptiDestination): every installer out there refuses a whole manifest with a
       path three levels deep;
    2. rewrites releases.optiscaler[<version>].components.lmxxf-weights-small (the archive and every file in it);
    3. prints the upload command, at the path its url names.

  The weights are NVIDIA-derived and change only with a new model, so they have an address per weights version, as
  mochizuki's models do: the same bytes keep the version already pinned, other bytes need -WeightsVersion. Later
  OptiScaler releases borrow the component (tools/pin-optiscaler.ps1).

.EXAMPLE
  .\tools\pin-lmxxf-small.ps1 -From <work>\native-game-tiled-assets\small -Version 0.5.6-opennr
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $From,
    [Parameter(Mandatory)] [string] $Version,
    [string] $WeightsVersion = '',
    [string] $Out = '',
    [string] $Manifest = '',
    [string] $Repo = 'zmodelerlover/amd-nr'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Manifest) { $Manifest = Join-Path $root 'payload\payload.json' }
$base = "https://huggingface.co/datasets/$Repo/resolve/main"
$sourceDll = '2bc19bc09a7b89dd44f73e2ac0108fcc586e2154bdd0830f242f4f040e28789f'
function Fail($m) { Write-Host "FAIL  $m" -ForegroundColor Red; exit 1 }
function Good($m) { Write-Host "OK    $m" -ForegroundColor Green }
function Note($m) { Write-Host "      $m" -ForegroundColor DarkGray }
function Sha256File($path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $s = [IO.File]::OpenRead($path)
    try { return [Convert]::ToHexString($sha.ComputeHash($s)).ToLowerInvariant() } finally { $s.Dispose(); $sha.Dispose() }
}

if (-not (Test-Path -LiteralPath $From -PathType Container)) { Fail "$From is not a folder" }
$From = (Resolve-Path -LiteralPath $From).Path
$listPath = Join-Path $From 'model-manifest.json'
if (-not (Test-Path -LiteralPath $listPath -PathType Leaf)) { Fail "model-manifest.json is missing from $From" }
$list = [IO.File]::ReadAllText($listPath) | ConvertFrom-Json
if ($list.model -ne 'small' -or $list.source_dll_sha256 -ne $sourceDll) { Fail "$listPath is not the Small model of nvngx_dlssnr.dll 310.9.16" }
$names = @($list.files.PSObject.Properties.Name)
if ($names.Count -ne 93) { Fail "model-manifest.json lists $($names.Count) files, not 93" }
foreach ($n in $names) {
    $f = Join-Path $From $n
    if (-not (Test-Path -LiteralPath $f -PathType Leaf)) { Fail "$n is missing from $From" }
    if ((Sha256File $f) -ne $list.files.$n.sha256) { Fail "$n is not the file model-manifest.json lists" }
}
$items = [System.Collections.Generic.List[object]]@(Get-ChildItem -LiteralPath $From -Force)
if ($items | Where-Object PSIsContainer) { Fail "$From holds a folder: the Small model is plain files only" }
if ($items.Count -ne $names.Count + 1) { Fail "$From holds files model-manifest.json does not list" }
$items.Sort([Comparison[object]] { param($a, $b) [string]::CompareOrdinal($a.Name, $b.Name) })

$extract = [System.Collections.Generic.List[object]]::new()
foreach ($i in $items) {
    $extract.Add([ordered]@{ name = $i.Name; path = "native-game-tiled-assets.small/$($i.Name)"; size = $i.Length; sha256 = Sha256File $i.FullName })
}
$m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
$release = @($m.releases.optiscaler) | Where-Object { $_.version -eq $Version } | Select-Object -First 1
if (-not $release) { Fail "releases.optiscaler has no $Version. Run tools/pin-optiscaler.ps1 with its archive first" }
# The same bytes keep the version (and the address) already pinned anywhere in the payload.
$same = @($m.releases.optiscaler) | ForEach-Object { $_.components.'lmxxf-weights-small' } | Where-Object {
    $_ -and (($_.extract | ForEach-Object { "$($_.path) $($_.sha256)" }) -join "`n") -eq (($extract | ForEach-Object { "$($_.path) $($_.sha256)" }) -join "`n")
} | Select-Object -First 1
if ($same -and (-not $WeightsVersion -or $WeightsVersion -eq $same.version)) {
    $release.components | Add-Member -NotePropertyName 'lmxxf-weights-small' -NotePropertyValue $same -Force
    [IO.File]::WriteAllText($Manifest, ($m | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    Good "$Manifest (lmxxf-weights-small $($same.version), already uploaded)"
    exit 0
}
$taken = @($m.releases.optiscaler) | ForEach-Object { $_.components.'lmxxf-weights-small'.version } | Where-Object { $_ }
if (-not $WeightsVersion) { $WeightsVersion = if ($taken) { Fail "other Small weights are pinned already: pass -WeightsVersion (taken: $($taken -join ', '))" } else { '1' } }
if ($taken -contains $WeightsVersion) { Fail "weights version $WeightsVersion is pinned to other bytes" }

if (-not $Out) { $Out = Join-Path $root "publish-lmxxf-weights-small-$WeightsVersion" }   # publish-*/ is ignored by git
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$zipName = "lmxxf-weights-small-$WeightsVersion.zip"
$zipPath = Join-Path $Out $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
$stamp = [DateTimeOffset]::new(2026, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($i in $items) {
        $entry = $zip.CreateEntry("native-game-tiled-assets.small/$($i.Name)", [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $stamp
        $sink = $entry.Open()
        $source = [IO.File]::OpenRead($i.FullName)
        try { $source.CopyTo($sink) } finally { $source.Dispose(); $sink.Dispose() }
    }
} finally { $zip.Dispose(); $stream.Dispose() }
$zipSha = Sha256File $zipPath
$zipSize = (Get-Item -LiteralPath $zipPath).Length
Good "$zipPath  ($($items.Count) files, $zipSize bytes)"

$component = [ordered]@{
    version = $WeightsVersion
    files   = @([ordered]@{ name = $zipName; size = $zipSize; sha256 = $zipSha; url = "$base/lmxxf-weights-small/$WeightsVersion/$zipName" })
    extract = $extract.ToArray()
}
$release.components | Add-Member -NotePropertyName 'lmxxf-weights-small' -NotePropertyValue $component -Force
[IO.File]::WriteAllText($Manifest, ($m | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
Good "$Manifest"
[IO.File]::WriteAllLines((Join-Path $Out 'SHA256SUMS.txt'), @("$zipSha *$zipName"))

Write-Host "`nUpload, before payload.json:" -ForegroundColor Cyan
Write-Host "  hf upload $Repo `"$zipPath`" lmxxf-weights-small/$WeightsVersion/$zipName --repo-type dataset --commit-message `"lmxxf 0.44 Small model weights $WeightsVersion (NVIDIA 310.9.16)`""
