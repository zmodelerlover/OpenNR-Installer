#Requires -Version 7
<#
.SYNOPSIS
  Pins WrongGPU (phantoslayer21's DLSS 4.5 Model M and Ray Reconstruction for RX 9000, redistributed with the
  author's written permission, kit section 10) in one OptiScaler release of payload.json, from his integration kit.

.DESCRIPTION
  -Kit is the integration kit, its zip or the folder it extracts to (WrongGPU-Integration-Kit-<version>). Every file
  is checked against the kit's SHA256SUMS.txt first, and these are taken as they are, never changed:

      files\wronggpu_runtime.dll, files\wronggpu-notices.txt,
      files\OptiScaler\custom\WrongGPU\ffx\amd_fidelityfx_upscaler_dx12.dll, files\plugins\WrongGPU-RR.asi

  The app puts the front end in the game as wronggpu_ffx_dx12.dll (the kit's route C) and the plugin in plugins\
  (Work.WrongGpu.cs). Never a model or an NVIDIA file: the player's own launcher run converts those on the PC.

  The swap from 0.5.5-ea, as its author asked: every other release's wronggpu component (0.5.5-ea's setup and DLLs)
  comes out, and so does wronggpu-dlss, NVIDIA's DLSS that setup converted from. danielblnc 0.8.0 still needs that
  file for OptiScaler\ (Work.HostDlss.cs), so it moves to host-dlss, the same file at NVIDIA's same address.

  Files are loose, under wronggpu/<version>/ in the dataset and wronggpu/<name> in the app's cache. They are copied
  into <Out> for the upload, and the commands are printed, the ones taking 0.5.5-ea down too.

.EXAMPLE
  .\tools\pin-wronggpu.ps1 -Kit E:\FDM-downloads\WrongGPU-Integration-Kit-0.6.0.zip -Release 0.6.8-opennr
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Kit,
    [Parameter(Mandatory)] [string] $Release,
    [string] $Out = '',
    [string] $Manifest = '',
    [string] $Repo = 'zmodelerlover/amd-nr'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Manifest) { $Manifest = Join-Path $root 'payload\payload.json' }
$base = "https://huggingface.co/datasets/$Repo/resolve/main"
function Fail($m) { Write-Host "FAIL  $m" -ForegroundColor Red; exit 1 }
function Good($m) { Write-Host "OK    $m" -ForegroundColor Green }
function Sha256File($path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }

if (-not (Test-Path -LiteralPath $Kit)) { Fail "$Kit is missing" }
if ((Get-Item -LiteralPath $Kit).PSIsContainer) { $dir = $Kit }
else {
    $dir = Join-Path ([IO.Path]::GetTempPath()) ('wronggpu-kit-' + [Guid]::NewGuid().ToString('N'))
    Expand-Archive -LiteralPath $Kit -DestinationPath $dir
}
$sumsFile = Get-ChildItem -LiteralPath $dir -Recurse -Filter SHA256SUMS.txt | Select-Object -First 1
if (-not $sumsFile) { Fail "no SHA256SUMS.txt in $Kit" }
$kitRoot = $sumsFile.DirectoryName
if ((Split-Path -Leaf $kitRoot) -notmatch '^WrongGPU-Integration-Kit-(.+)$') { Fail "$kitRoot is not WrongGPU-Integration-Kit-<version>" }
$Version = $Matches[1]
foreach ($line in Get-Content -LiteralPath $sumsFile.FullName) {
    if ($line -notmatch '^([0-9a-f]{64})\s+\*?(.+)$') { continue }
    $at = Join-Path $kitRoot $Matches[2]
    if (-not (Test-Path -LiteralPath $at -PathType Leaf) -or (Sha256File $at) -ne $Matches[1]) { Fail "$($Matches[2]) does not match the kit's SHA256SUMS.txt" }
}
Good "kit $Version matches its SHA256SUMS.txt"

if (-not $Out) { $Out = Join-Path $root "publish-wronggpu-$Version" }   # publish-*/ is ignored by git
$take = [ordered]@{
    'files\wronggpu_runtime.dll' = 'wronggpu_runtime.dll'
    'files\wronggpu-notices.txt' = 'wronggpu-notices.txt'
    'files\OptiScaler\custom\WrongGPU\ffx\amd_fidelityfx_upscaler_dx12.dll' = 'amd_fidelityfx_upscaler_dx12.dll'
    'files\plugins\WrongGPU-RR.asi' = 'WrongGPU-RR.asi'
}
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$files = [System.Collections.Generic.List[object]]::new()
$sums = [System.Collections.Generic.List[string]]::new()
foreach ($at in $take.Keys) {
    $source = Join-Path $kitRoot $at
    $name = $take[$at]
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { Fail "$at is not in the kit" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $Out $name) -Force
    $sha = Sha256File $source
    $files.Add([ordered]@{ name = $name; path = "wronggpu/$name"; size = (Get-Item -LiteralPath $source).Length; sha256 = $sha
                           url = "$base/wronggpu/$Version/$name" })
    $sums.Add("$sha *$name")
}

$m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
$target = @($m.releases.optiscaler) | Where-Object { $_.version -eq $Release } | Select-Object -First 1
if (-not $target) { Fail "releases.optiscaler has no $Release. Run tools/pin-optiscaler.ps1 with its archive first" }
$retired = [System.Collections.Generic.HashSet[string]]::new()
foreach ($r in @($m.releases.optiscaler)) {
    $c = $r.components
    if ($c.PSObject.Properties['wronggpu-dlss']) {
        $dlss = $c.'wronggpu-dlss'
        foreach ($f in $dlss.files) { $f.path = 'host-dlss/nvngx_dlss.dll' }
        $c.PSObject.Properties.Remove('wronggpu-dlss')
        $c | Add-Member -NotePropertyName 'host-dlss' -NotePropertyValue $dlss -Force
    }
    if ($r -ne $target -and $c.PSObject.Properties['wronggpu']) {
        foreach ($f in $c.wronggpu.files) { [void]$retired.Add($f.url) }
        $c.PSObject.Properties.Remove('wronggpu')
    }
}
$target.components | Add-Member -NotePropertyName wronggpu -NotePropertyValue ([ordered]@{ version = $Version; files = $files.ToArray() }) -Force
[IO.File]::WriteAllText($Manifest, ($m | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllLines((Join-Path $Out 'SHA256SUMS.txt'), $sums)
Good "$Manifest  (wronggpu $Version in $Release; wronggpu-dlss moved to host-dlss; 0.5.5-ea out of every other release)"

Write-Host "`nUpload, before payload.json:" -ForegroundColor Cyan
foreach ($f in $files) {
    Write-Host "  hf upload $Repo `"$(Join-Path $Out $f.name)`" wronggpu/$Version/$($f.name) --repo-type dataset --commit-message `"WrongGPU $Version`: $($f.name)`""
}
if ($retired.Count) {
    Write-Host "`nAfter payload.json is published, take 0.5.5-ea down (its author asked for that at the swap):" -ForegroundColor Cyan
    foreach ($u in $retired) {
        if ($u -match '/resolve/main/(.+)$') { Write-Host "  hf repo-files delete $Repo $($Matches[1]) --repo-type dataset" }
    }
}
