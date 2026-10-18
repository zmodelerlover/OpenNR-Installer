#Requires -Version 7
<#
.SYNOPSIS
  Pins WrongGPU (phantoslayer21's DLSS 4.5 super resolution for RX 9000, redistributed with the author's written
  permission) in one OptiScaler release of payload.json, and NVIDIA's DLSS file its setup converts the model from.

.DESCRIPTION
  -From is a folder WrongGPU's own setup installed into (a game's), from which these are taken as they are:

      wronggpu_ffx_dx12.dll, wronggpu_runtime.dll, plugins\WrongGPU.asi, wronggpu-license.txt, wronggpu-notices.txt

  -Setup is WrongGPU-Setup-<version>.exe, which the app runs to convert the model (Work.WrongGpu.cs). Not pinned
  from the folder: wronggpu.ini (the app writes its own) and wronggpu-amd-compare.dll (AMD's FSR 4, which the app
  copies from OptiScaler's own folder, as the setup does).

  Every file is loose, under wronggpu/<version>/ in the dataset and wronggpu/<name> in the app's cache. They are
  copied into <Out> for the upload, and the commands are printed.

  -Dlss is nvngx_dlss.dll 310.9.1, downloaded from NVIDIA's own GitHub repository (-DlssUrl): pinned as the
  wronggpu-dlss component at that address, never uploaded anywhere else. The app downloads it only when the PC has
  no DLSS 310.5 or newer for the setup to convert from, and never puts it in a game.

.EXAMPLE
  .\tools\pin-wronggpu.ps1 -From E:\Games\Cyberpunk2077\bin\x64 -Setup <dir>\WrongGPU-Setup-0.5.5-ea.exe -Version 0.5.5-ea -Release 0.5.4-amd-nr -Dlss <dir>\nvngx_dlss.dll
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $From,
    [Parameter(Mandatory)] [string] $Setup,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Release,
    [string] $SetupSha = '',
    [string] $Dlss = '',
    [string] $DlssVersion = '310.9.1',
    [string] $DlssUrl = 'https://raw.githubusercontent.com/NVIDIA/DLSS/v310.9.1/lib/Windows_x86_64/rel/nvngx_dlss.dll',
    [string] $Out = '',
    [string] $Manifest = '',
    [string] $Repo = 'zmodelerlover/amd-nr'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Manifest) { $Manifest = Join-Path $root 'payload\payload.json' }
if (-not $Out) { $Out = Join-Path $root "publish-wronggpu-$Version" }   # publish-*/ is ignored by git
$base = "https://huggingface.co/datasets/$Repo/resolve/main"
function Fail($m) { Write-Host "FAIL  $m" -ForegroundColor Red; exit 1 }
function Good($m) { Write-Host "OK    $m" -ForegroundColor Green }
function Sha256File($path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $s = [IO.File]::OpenRead($path)
    try { return [Convert]::ToHexString($sha.ComputeHash($s)).ToLowerInvariant() } finally { $s.Dispose(); $sha.Dispose() }
}

if (-not (Test-Path -LiteralPath $Setup -PathType Leaf)) { Fail "$Setup is missing" }
$setupName = Split-Path -Leaf $Setup
if ($setupName -ne "WrongGPU-Setup-$Version.exe") { Fail "$setupName is not WrongGPU-Setup-$Version.exe" }
if ($SetupSha -and (Sha256File $Setup) -ne $SetupSha.ToLowerInvariant()) { Fail "$setupName does not hash to $SetupSha" }

$take = [ordered]@{
    'wronggpu_ffx_dx12.dll' = 'wronggpu_ffx_dx12.dll'; 'wronggpu_runtime.dll' = 'wronggpu_runtime.dll'
    'plugins\WrongGPU.asi' = 'WrongGPU.asi'; 'wronggpu-license.txt' = 'wronggpu-license.txt'
    'wronggpu-notices.txt' = 'wronggpu-notices.txt'
}
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$files = [System.Collections.Generic.List[object]]::new()
$sums = [System.Collections.Generic.List[string]]::new()
function Add($source, $name) {
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { Fail "$source is missing" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $Out $name) -Force
    $sha = Sha256File $source
    $files.Add([ordered]@{ name = $name; path = "wronggpu/$name"; size = (Get-Item -LiteralPath $source).Length; sha256 = $sha
                           url = "$base/wronggpu/$Version/$name" })
    $sums.Add("$sha *$name")
}
foreach ($at in $take.Keys) { Add (Join-Path $From $at) $take[$at] }
Add $Setup $setupName

$m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
$target = @($m.releases.optiscaler) | Where-Object { $_.version -eq $Release } | Select-Object -First 1
if (-not $target) { Fail "releases.optiscaler has no $Release. Run tools/pin-optiscaler.ps1 with its archive first" }
$target.components | Add-Member -NotePropertyName wronggpu -NotePropertyValue ([ordered]@{ version = $Version; files = $files.ToArray() }) -Force
if ($Dlss) {
    if (-not (Test-Path -LiteralPath $Dlss -PathType Leaf)) { Fail "$Dlss is missing" }
    $v = (Get-Item -LiteralPath $Dlss).VersionInfo
    if ("$($v.FileMajorPart).$($v.FileMinorPart).$($v.FileBuildPart)" -ne $DlssVersion) { Fail "$Dlss is $($v.FileVersion), not $DlssVersion" }
    $dlssSha = Sha256File $Dlss
    $target.components | Add-Member -NotePropertyName 'wronggpu-dlss' -NotePropertyValue ([ordered]@{ version = $DlssVersion; files = @(
        [ordered]@{ name = 'nvngx_dlss.dll'; path = 'wronggpu/nvngx_dlss.dll'; size = (Get-Item -LiteralPath $Dlss).Length
                    sha256 = $dlssSha; url = $DlssUrl }) }) -Force
    $sums.Add("$dlssSha *nvngx_dlss.dll")
}
[IO.File]::WriteAllText($Manifest, ($m | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllLines((Join-Path $Out 'SHA256SUMS.txt'), $sums)
Good "$Manifest  (wronggpu $Version in $Release$(if ($Dlss) { ", wronggpu-dlss $DlssVersion" }))"

Write-Host "`nUpload, before payload.json:" -ForegroundColor Cyan
foreach ($f in $files) {
    Write-Host "  hf upload $Repo `"$(Join-Path $Out $f.name)`" wronggpu/$Version/$($f.name) --repo-type dataset --commit-message `"WrongGPU $Version`: $($f.name)`""
}
if ($Dlss) { Write-Host "  (nvngx_dlss.dll stays at NVIDIA's address: $DlssUrl)" }
