#Requires -Version 7
<#
.SYNOPSIS
  Pins OpenRR (OpenNR-Denoiser's Ray Reconstruction runtime) in one OptiScaler release of payload.json, and NVIDIA's
  nvngx_dlssd.dll 310.9.1 it builds its network from.

.DESCRIPTION
  -From is laid out the way a game folder receives it (OpenNR-Denoiser's out\<round>-stage):

      OpenNrRrRuntime.dll
      opennr-rr\shaders\*.spv

  Anything else there (INSTALAR.txt, notes) is ignored. What this does:

    1. writes <Out>\openrr-<version>.zip -- the runtime and the kernels under the payload paths the app keeps them at
       (opennr-rr.shaders/x.spv for opennr-rr\shaders\x.spv, see Work.OpenRrDestination: a payload path is two levels
       deep at most), in ordinal order and with a fixed timestamp, so the same files always make the same archive;
    2. writes releases.optiscaler[<release>].components.openrr (the archive and every file in it);
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
$shaders = Join-Path $From 'opennr-rr\shaders'
if (-not (Test-Path -LiteralPath $runtime -PathType Leaf)) { Fail "$runtime is missing" }
if (-not (Test-Path -LiteralPath $shaders -PathType Container)) { Fail "$shaders is missing" }
if (Get-ChildItem -LiteralPath $shaders -Directory) { Fail "$shaders has folders: the app installs its .spv files only" }
$kernels = @(Get-ChildItem -LiteralPath $shaders -File | Sort-Object { $_.Name } -Culture ([cultureinfo]::InvariantCulture))
if ($kernels.Count -eq 0) { Fail "$shaders has no kernels" }
$odd = $kernels | Where-Object { $_.Extension -ne '.spv' -or $_.Name.Length -gt 96 }
if ($odd) { Fail "not a kernel the app installs: $($odd.Name -join ', ')" }

$files = @([pscustomobject]@{ Path = 'OpenNrRrRuntime.dll'; File = $runtime }) +
    @($kernels | ForEach-Object { [pscustomobject]@{ Path = "opennr-rr.shaders/$($_.Name)"; File = $_.FullName } })
$files = $files | Sort-Object Path -Culture ([cultureinfo]::InvariantCulture)

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$zipName = "openrr-$Version.zip"
$zipPath = Join-Path $Out $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
$stamp = [DateTimeOffset]::new(2026, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
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
$zipSha = Sha256File $zipPath
Good "$zipPath  ($($files.Count) files)"

$m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
$target = @($m.releases.optiscaler) | Where-Object { $_.version -eq $Release } | Select-Object -First 1
if (-not $target) { Fail "releases.optiscaler has no $Release. Run tools/pin-optiscaler.ps1 with its archive first" }
$target.components | Add-Member -NotePropertyName openrr -NotePropertyValue ([ordered]@{
    version = $Version
    files   = @([ordered]@{ name = $zipName; size = (Get-Item -LiteralPath $zipPath).Length; sha256 = $zipSha; url = "$base/openrr/$Version/$zipName" })
    extract = $extract.ToArray()
}) -Force

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
Good "$Manifest  (openrr $Version in $Release$(if ($Dlssd) { ", openrr-dlssd $DlssdVersion" }))"

Write-Host "`nUpload, before payload.json (each needs the owner's go-ahead; nvngx_dlssd.dll is NVIDIA's):" -ForegroundColor Cyan
Write-Host "  hf upload $Repo `"$zipPath`" openrr/$Version/$zipName --repo-type dataset --commit-message `"OpenRR $Version`""
if ($Dlssd) {
    Write-Host "  hf upload $Repo `"$(Join-Path $Out 'nvngx_dlssd.dll')`" openrr-dlssd/$DlssdVersion/nvngx_dlssd.dll --repo-type dataset --commit-message `"nvngx_dlssd.dll $DlssdVersion for OpenRR`""
}
