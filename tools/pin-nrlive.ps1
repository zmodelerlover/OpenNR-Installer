#Requires -Version 7
<#
.SYNOPSIS
  Pins an NRLive release (Caliel's window scaler, GPL-3.0, github.com/Caliel666/NRLive) in payload.json, in place
  of the placeholder the NRLive page waits on.

.DESCRIPTION
  -Zip is the zip NRLive's release publishes (NRLive.zip since 3.1.0, NRLiveUI.zip before): NRLive.exe, its runtime and version.txt at the root, and
  no NVIDIA model (its CI refuses to package one). Its version.txt has to say -Version.

  The release becomes releases.nrlive[<Version>] with the zip at
  huggingface.co/datasets/<Repo>/resolve/main/nrlive/<Version>/<zip name> and NRLive's own GitHub asset as its
  mirror. The mochizuki models of the release it replaces (with none, of OptiScaler's newest release) are kept
  beside it (the app links them into opennr\dlssnr-amd\ for NR); -NoModels leaves them out. The zip is copied into <Out> for the upload, and the command is
  printed. tools/publish-payload.ps1 refuses the payload until this has run.

.EXAMPLE
  .\tools\pin-nrlive.ps1 -Zip <dir>\NRLive.zip -Version 3.1.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Zip,
    [Parameter(Mandatory)] [string] $Version,
    [switch] $NoModels,
    [string] $Int4 = '',
    [string] $Int4Version = '1',
    [string] $Out = '',
    [string] $Manifest = '',
    [string] $Repo = 'zmodelerlover/amd-nr'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Manifest) { $Manifest = Join-Path $root 'payload\payload.json' }
if (-not $Out) { $Out = Join-Path $root "publish-nrlive-$Version" }   # publish-*/ is ignored by git
function Fail($m) { Write-Host "FAIL  $m" -ForegroundColor Red; exit 1 }
function Good($m) { Write-Host "OK    $m" -ForegroundColor Green }

if (-not (Test-Path -LiteralPath $Zip -PathType Leaf)) { Fail "$Zip is missing" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($Zip)
try {
    $names = $archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
    foreach ($need in 'NRLive.exe', 'version.txt') { if ($names -notcontains $need) { Fail "$Zip has no $need at its root" } }
    # By file name: opennr/dlssnr-amd/int4/ holds mochizuki's own .bin tables, which belong in the package.
    if ($names | Where-Object { ($_ -split '/')[-1] -like 'dlssnr*.bin' }) { Fail "$Zip carries an NVIDIA model; NRLive's package must not" }
    $reader = [IO.StreamReader]::new(($archive.Entries | Where-Object { $_.FullName -eq 'version.txt' }).Open())
    $inside = $reader.ReadToEnd().Trim(); $reader.Dispose()
    if ($inside -ne $Version) { Fail "version.txt in $Zip says $inside, not $Version" }
} finally { $archive.Dispose() }

$asset = Split-Path -Leaf $Zip
$sha = (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item -LiteralPath $Zip).Length
New-Item -ItemType Directory -Force -Path $Out | Out-Null
Copy-Item -LiteralPath $Zip -Destination (Join-Path $Out $asset) -Force

$m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
$previous = @($m.releases.nrlive) | Select-Object -First 1
# No NRLive pin yet (master ships without one): the models OptiScaler's newest release pins.
if (-not $previous) { $previous = @($m.releases.optiscaler) | Select-Object -First 1 }
$components = [ordered]@{ nrlive = [ordered]@{ version = $Version; files = @([ordered]@{
    name = $asset; size = $size; sha256 = $sha
    url = "https://huggingface.co/datasets/$Repo/resolve/main/nrlive/$Version/$asset"
    mirrors = @("https://github.com/Caliel666/NRLive/releases/download/$Version/$asset") }) } }
if (-not $NoModels -and $previous) {
    foreach ($model in 'mochizuki-model', 'mochizuki-model-small', 'mochizuki-int4') {
        if ($previous.components.$model) { $components[$model] = $previous.components.$model }
    }
}
# int4's weights, loose under an address of their own, beside the models in opennr\dlssnr-amd.
if ($Int4) {
    if (-not (Test-Path -LiteralPath $Int4 -PathType Leaf)) { Fail "$Int4 is missing" }
    if ((Split-Path -Leaf $Int4) -ne 'dlssnr-int4.bin') { Fail "$Int4 is not dlssnr-int4.bin" }
    $int4Dest = "mochizuki/int4-$Int4Version/dlssnr-int4.bin"
    $components['mochizuki-int4'] = [ordered]@{ version = $Int4Version; files = @([ordered]@{
        name = 'dlssnr-int4.bin'; path = 'dlssnr-amd/dlssnr-int4.bin'; size = (Get-Item -LiteralPath $Int4).Length
        sha256 = (Get-FileHash -LiteralPath $Int4 -Algorithm SHA256).Hash.ToLowerInvariant()
        url = "https://huggingface.co/datasets/$Repo/resolve/main/$int4Dest" }) }
}
$release = [ordered]@{ version = $Version; components = $components }
$m.releases | Add-Member -NotePropertyName nrlive -NotePropertyValue @($release) -Force
[IO.File]::WriteAllText($Manifest, ($m | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
Good "$Manifest  (nrlive $Version, $size bytes, sha256 $sha$(if ($components.Count -gt 1) { ', with the mochizuki models' }))"

Write-Host "`nUpload, before payload.json:" -ForegroundColor Cyan
Write-Host "  hf upload $Repo `"$(Join-Path $Out $asset)`" nrlive/$Version/$asset --repo-type dataset --commit-message `"NRLive $Version`""
if ($Int4) { Write-Host "  hf upload $Repo `"$Int4`" $int4Dest --repo-type dataset --commit-message `"int4 weights $Int4Version`"" }
