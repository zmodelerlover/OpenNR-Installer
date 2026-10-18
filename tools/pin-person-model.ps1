#Requires -Version 7
<#
.SYNOPSIS
  Pins OptiScaler 0.5.4's person protection (the person-model folder) in one OptiScaler release of payload.json, and
  builds the archive that gets uploaded.

.DESCRIPTION
  -From is the person-model folder as a game receives it, beside OptiScaler.dll: person-worker.exe, ONNX Runtime
  (onnxruntime.dll), the VC++ runtime it needs, the three person models (pphumanseg.onnx, yolo11n-seg.onnx,
  yunet.onnx) and their licences. The licences ship with the models, in the same folder, so each one is required:

      onnxruntime_LICENSE.txt     ONNX Runtime, MIT
      YuNet_MIT.txt               YuNet, MIT
      PPHumanSeg_Apache-2.0.txt   PP-HumanSeg, Apache-2.0
      YOLO11_AGPL-3.0.txt         Ultralytics YOLO11n-seg, AGPL-3.0

  Plain files only, nothing deeper. What this does:

    1. writes <Out>\person-model-<version>.zip, every file as person-model/<name>, in ordinal order and with a fixed
       timestamp, so the same files make the same archive on any machine;
    2. rewrites releases.optiscaler[<version>].components.person-model (the archive and every file in it);
    3. prints the upload command, at the path its url names.

.EXAMPLE
  .\tools\pin-person-model.ps1 -From <neural-amd-opti>\exports\release-0.5.4\person-model -Version 0.5.4-amd-nr
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $From,
    [Parameter(Mandatory)] [string] $Version,
    [string] $Out = '',
    [string] $Manifest = '',
    [string] $Repo = 'zmodelerlover/amd-nr'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Manifest) { $Manifest = Join-Path $root 'payload\payload.json' }
if (-not $Out) { $Out = Join-Path $root "publish-person-model-$Version" }   # publish-*/ is ignored by git
$base = "https://huggingface.co/datasets/$Repo/resolve/main"
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
$licences = 'onnxruntime_LICENSE.txt', 'YuNet_MIT.txt', 'PPHumanSeg_Apache-2.0.txt', 'YOLO11_AGPL-3.0.txt'
foreach ($need in @('person-worker.exe', 'onnxruntime.dll') + $licences) {
    if (-not (Test-Path -LiteralPath (Join-Path $From $need) -PathType Leaf)) { Fail "$need is missing from $From" }
}
$invalid = [IO.Path]::GetInvalidFileNameChars()
$items = Get-ChildItem -LiteralPath $From -Force
if ($items | Where-Object PSIsContainer) { Fail "$From holds a folder: person-model is plain files only" }
$items = [System.Collections.Generic.List[object]]@($items | Where-Object { $_.Name -notlike '*.log' })
foreach ($i in $items) {
    if ($i.Name.Length -gt 96 -or $i.Name.IndexOfAny($invalid) -ge 0) { Fail "unusable file name: '$($i.Name)'" }
}
$items.Sort([Comparison[object]] { param($a, $b) [string]::CompareOrdinal($a.Name, $b.Name) })

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$zipName = "person-model-$Version.zip"
$zipPath = Join-Path $Out $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
$stamp = [DateTimeOffset]::new(2026, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$extract = [System.Collections.Generic.List[object]]::new()
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($i in $items) {
        $path = "person-model/$($i.Name)"
        $entry = $zip.CreateEntry($path, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $stamp
        $sink = $entry.Open()
        $source = [IO.File]::OpenRead($i.FullName)
        try { $source.CopyTo($sink) } finally { $source.Dispose(); $sink.Dispose() }
        $extract.Add([ordered]@{ name = $i.Name; path = $path; size = $i.Length; sha256 = Sha256File $i.FullName })
    }
} finally { $zip.Dispose(); $stream.Dispose() }
$zipSha = Sha256File $zipPath
$zipSize = (Get-Item -LiteralPath $zipPath).Length
Good "$zipPath  ($($items.Count) files, $zipSize bytes)"

$m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
$release = @($m.releases.optiscaler) | Where-Object { $_.version -eq $Version } | Select-Object -First 1
if (-not $release) { Fail "releases.optiscaler has no $Version. Run tools/pin-optiscaler.ps1 with its archive first" }
$component = [ordered]@{
    version = $Version
    files   = @([ordered]@{ name = $zipName; size = $zipSize; sha256 = $zipSha; url = "$base/person-model/$Version/$zipName" })
    extract = $extract.ToArray()
}
$release.components | Add-Member -NotePropertyName 'person-model' -NotePropertyValue $component -Force
[IO.File]::WriteAllText($Manifest, ($m | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
Good "$Manifest"
[IO.File]::WriteAllLines((Join-Path $Out 'SHA256SUMS.txt'), @("$zipSha *$zipName"))

Write-Host "`nUpload, before payload.json:" -ForegroundColor Cyan
Write-Host "  hf upload $Repo `"$zipPath`" person-model/$Version/$zipName --repo-type dataset --commit-message `"person protection $Version`: worker, ONNX Runtime, models and licences`""
