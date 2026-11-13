#Requires -Version 7
<#
.SYNOPSIS
  Pins OpenRR (OpenNR-Denoiser's Ray Reconstruction runtime) in one OptiScaler release of payload.json, and NVIDIA's
  nvngx_dlssd.dll 310.9.1 it builds its network from.

.DESCRIPTION
  -From is laid out the way a game folder receives it (OpenNR-Denoiser's out\<round>-stage):

      OpenNrRrRuntime.dll
      opennr-rr\shaders\*.spv
      opennr-rr\shaders-gfx11\*.spv   (RX 7000 kernels, OpenRR 0.1.6 on; optional)

  Anything else there (INSTALAR.txt, notes) is ignored. What this does:

    1. writes <Out>\openrr-<version>.zip -- the runtime and the kernels under the payload paths the app keeps them at
       (opennr-rr.shaders/x.spv for opennr-rr\shaders\x.spv, see Work.OpenRrDestination: a payload path is two levels
       deep at most), in ordinal order and with a fixed timestamp, so the same files always make the same archive;
    2. writes releases.optiscaler[<release>].components.openrr (the archive and every file in it);
       with shaders-gfx11\, <Out>\openrr-gfx11-<version>.zip (opennr-rr.shaders-gfx11/x.spv) as the openrr-gfx11 component,
       which the installers already out there never ask for;
    3. with -Dlssd, checks it is nvngx_dlssd.dll 310.9.1, copies it into <Out> and pins it loose as openrr-dlssd at
       openrr-dlssd/310.9.1/nvngx_dlssd.dll in the dataset;
    4. prints the upload commands. Nothing is uploaded here.

  -Url replaces the dataset base, for a test payload whose files sit beside the app.

.EXAMPLE
  .\tools\pin-openrr.ps1 -From <OpenNR-Denoiser>\out\perf-18-stage -Version 0.1.0-perf18 -Release 0.6.0-opennr -Dlssd <dir>\nvngx_dlssd.dll
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $From,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Release,
    [string] $Dlssd = '',
    [string] $DlssdVersion = '310.9.1',
    [string] $Out = '',
    [string] $Manifest = '',
    [string] $Url = '',
    [string] $Repo = 'zmodelerlover/amd-nr'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Manifest) { $Manifest = Join-Path $root 'payload\payload.json' }
if (-not $Out) { $Out = Join-Path $root "publish-openrr-$Version" }   # publish-*/ is ignored by git
$base = if ($Url) { $Url.TrimEnd('/') } else { "https://huggingface.co/datasets/$Repo/resolve/main" }
function Fail($m) { Write-Host "FAIL  $m" -ForegroundColor Red; exit 1 }
function Good($m) { Write-Host "OK    $m" -ForegroundColor Green }
function Sha256File($path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $s = [IO.File]::OpenRead($path)
    try { return [Convert]::ToHexString($sha.ComputeHash($s)).ToLowerInvariant() } finally { $s.Dispose(); $sha.Dispose() }
}

$runtime = Join-Path $From 'OpenNrRrRuntime.dll'
if (-not (Test-Path -LiteralPath $runtime -PathType Leaf)) { Fail "$runtime is missing" }
function Kernels($dir, $payload) {
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { Fail "$dir is missing" }
    if (Get-ChildItem -LiteralPath $dir -Directory) { Fail "$dir has folders: the app installs its .spv files only" }
    $k = @(Get-ChildItem -LiteralPath $dir -File)
    if ($k.Count -eq 0) { Fail "$dir has no kernels" }
    $odd = $k | Where-Object { $_.Extension -ne '.spv' -or $_.Name.Length -gt 96 }
    if ($odd) { Fail "not a kernel the app installs: $($odd.Name -join ', ')" }
    $k | ForEach-Object { [pscustomobject]@{ Path = "$payload/$($_.Name)"; File = $_.FullName } }
}

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$stamp = [DateTimeOffset]::new(2026, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
# a component: the archive in <Out> and every file in it, under the payload paths, at openrr/<version>/ in the dataset
function Pack($zipName, $files) {
    $files = @($files | Sort-Object Path -Culture ([cultureinfo]::InvariantCulture))
    $zipPath = Join-Path $Out $zipName
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    $extract = [System.Collections.Generic.List[object]]::new()
    $stream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($f in $files) {
            $entry = $zip.CreateEntry($f.Path, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $stamp
            $sink = $entry.Open()
            $source = [IO.File]::OpenRead($f.File)
            try { $source.CopyTo($sink) } finally { $source.Dispose(); $sink.Dispose() }
            $extract.Add([ordered]@{ name = [IO.Path]::GetFileName($f.Path); path = $f.Path; size = (Get-Item -LiteralPath $f.File).Length; sha256 = Sha256File $f.File })
        }
    } finally { $zip.Dispose(); $stream.Dispose() }
    Good "$zipPath  ($($files.Count) files)"
    $script:uploads += $zipPath
    [ordered]@{
        version = $Version
        files   = @([ordered]@{ name = $zipName; size = (Get-Item -LiteralPath $zipPath).Length; sha256 = (Sha256File $zipPath); url = "$base/openrr/$Version/$zipName" })
        extract = $extract.ToArray()
    }
}

$m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
$target = @($m.releases.optiscaler) | Where-Object { $_.version -eq $Release } | Select-Object -First 1
if (-not $target) { Fail "releases.optiscaler has no $Release. Run tools/pin-optiscaler.ps1 with its archive first" }
$uploads = @()
$target.components | Add-Member -NotePropertyName openrr -NotePropertyValue (Pack "openrr-$Version.zip" (
    @([pscustomobject]@{ Path = 'OpenNrRrRuntime.dll'; File = $runtime }) + @(Kernels (Join-Path $From 'opennr-rr\shaders') 'opennr-rr.shaders'))) -Force
# RX 7000 kernels (OpenRR 0.1.6 on): a component of their own, which only the installers that know it ask for
$gfx11 = Join-Path $From 'opennr-rr\shaders-gfx11'
if (Test-Path -LiteralPath $gfx11) {
    $target.components | Add-Member -NotePropertyName 'openrr-gfx11' -NotePropertyValue (Pack "openrr-gfx11-$Version.zip" (
        Kernels $gfx11 'opennr-rr.shaders-gfx11')) -Force
}

if ($Dlssd) {
    if (-not (Test-Path -LiteralPath $Dlssd -PathType Leaf)) { Fail "$Dlssd is missing" }
    if ((Split-Path -Leaf $Dlssd) -ne 'nvngx_dlssd.dll') { Fail "$Dlssd is not nvngx_dlssd.dll" }
    $v = (Get-Item -LiteralPath $Dlssd).VersionInfo
    if ("$($v.FileMajorPart).$($v.FileMinorPart).$($v.FileBuildPart)" -ne $DlssdVersion) { Fail "$Dlssd is $($v.FileVersion), not $DlssdVersion" }
    Copy-Item -LiteralPath $Dlssd -Destination (Join-Path $Out 'nvngx_dlssd.dll') -Force
    $target.components | Add-Member -NotePropertyName 'openrr-dlssd' -NotePropertyValue ([ordered]@{ version = $DlssdVersion; files = @(
        [ordered]@{ name = 'nvngx_dlssd.dll'; path = 'openrr-dlssd/nvngx_dlssd.dll'; size = (Get-Item -LiteralPath $Dlssd).Length
                    sha256 = (Sha256File $Dlssd); url = "$base/openrr-dlssd/$DlssdVersion/nvngx_dlssd.dll" }) }) -Force
}
[IO.File]::WriteAllText($Manifest, ($m | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
Good "$Manifest  (openrr $Version in $Release$(if (Test-Path -LiteralPath $gfx11) { ', openrr-gfx11' })$(if ($Dlssd) { ", openrr-dlssd $DlssdVersion" }))"

Write-Host "`nUpload, before payload.json (each needs the owner's go-ahead; nvngx_dlssd.dll is NVIDIA's):" -ForegroundColor Cyan
foreach ($z in $uploads) {
    Write-Host "  hf upload $Repo `"$z`" openrr/$Version/$(Split-Path -Leaf $z) --repo-type dataset --commit-message `"OpenRR $Version`""
}
if ($Dlssd) {
    Write-Host "  hf upload $Repo `"$(Join-Path $Out 'nvngx_dlssd.dll')`" openrr-dlssd/$DlssdVersion/nvngx_dlssd.dll --repo-type dataset --commit-message `"nvngx_dlssd.dll $DlssdVersion for OpenRR`""
}
