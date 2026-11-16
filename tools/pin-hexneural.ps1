#Requires -Version 7
<#
.SYNOPSIS
  Pins HexNeural (Emion's RX 6000 NR runtime, GPL) in one OptiScaler release of payload.json, from his package.

.DESCRIPTION
  -From is laid out the way a game folder receives it (the bin\ of Emion's package, without OptiScaler.dll):

      Rdna2NrRuntime.dll, nr_runtime.dll, Rdna2NrRuntime.ini
      nr_data_h15\<model>\<resolution>\*  and  nr_data_h15\<model>\<resolution>\kernels\*

  -Notice is the licence notice that goes in beside them (HexNeural-NOTICE.txt). What this does:

    1. refuses anything of NVIDIA's (nvngx*, sl.*), any other DLL, and OptiScaler.dll;
    2. leaves out empty files (the payload refuses a size of zero; the nr_data_h15 overrides.txt are empty) and
       says which;
    3. writes <Out>\hexneural-<version>.zip -- every file under the payload path the app keeps it at (folders spelt
       with dots: nr_data_h15.<model>.<res>.kernels/x.co for nr_data_h15\<model>\<res>\kernels\x.co, see
       Work.HexNeuralDestination; a payload path is two levels deep at most), in ordinal order and with a fixed
       timestamp, so the same files always make the same archive;
    4. writes releases.optiscaler[<release>].components.hexneural (the archive at hexneural/<version>/ in the
       dataset, and every file in it). With -Placeholder a release that is not there yet is made with its OptiScaler
       pinned to the placeholder hash: listed, never offered, and publish-payload refuses it until
       tools/pin-optiscaler.ps1 pins that version's real archive;
    5. prints the upload command. Nothing is uploaded here.

.EXAMPLE
  .\tools\pin-hexneural.ps1 -From D:\hexneural\pkg\bin -Notice ..\send\hexneural-emion\upload\HexNeural-NOTICE.txt -Version 2026.11.16 -Release 0.7.0-opennr
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $From,
    [Parameter(Mandatory)] [string] $Notice,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Release,
    [switch] $Placeholder,
    [string] $Out = '',
    [string] $Manifest = '',
    [string] $Url = '',
    [string] $Repo = 'zmodelerlover/amd-nr'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Manifest) { $Manifest = Join-Path $root 'payload\payload.json' }
if (-not $Out) { $Out = Join-Path $root "publish-hexneural-$Version" }   # publish-*/ is ignored by git
$base = if ($Url) { $Url.TrimEnd('/') } else { "https://huggingface.co/datasets/$Repo/resolve/main" }
function Fail($m) { Write-Host "FAIL  $m" -ForegroundColor Red; exit 1 }
function Good($m) { Write-Host "OK    $m" -ForegroundColor Green }
function Sha256File($path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $s = [IO.File]::OpenRead($path)
    try { return [Convert]::ToHexString($sha.ComputeHash($s)).ToLowerInvariant() } finally { $s.Dispose(); $sha.Dispose() }
}

$From = (Resolve-Path -LiteralPath $From).Path
$top = 'Rdna2NrRuntime.dll', 'nr_runtime.dll', 'Rdna2NrRuntime.ini'
foreach ($t in $top) { if (-not (Test-Path -LiteralPath (Join-Path $From $t) -PathType Leaf)) { Fail "$t is not in $From" } }
if (-not (Test-Path -LiteralPath $Notice -PathType Leaf)) { Fail "$Notice is missing" }
$data = Join-Path $From 'nr_data_h15'
if (-not (Test-Path -LiteralPath $data -PathType Container)) { Fail "$data is missing" }

$files = [System.Collections.Generic.List[object]]::new()
foreach ($t in $top) { $files.Add([pscustomobject]@{ Path = $t; File = (Join-Path $From $t) }) }
$files.Add([pscustomobject]@{ Path = 'HexNeural-NOTICE.txt'; File = (Resolve-Path -LiteralPath $Notice).Path })
$empty = @()
foreach ($f in Get-ChildItem -LiteralPath $data -Recurse -File) {
    $rel = [IO.Path]::GetRelativePath($From, $f.FullName) -replace '\\', '/'
    $parts = $rel -split '/'
    if ($parts.Count -lt 2 -or $parts.Count -gt 5) { Fail "$rel is deeper than the app installs" }
    if ($parts | Where-Object { $_.Contains('.') -and $_ -ne $parts[-1] }) { Fail "$rel has a dot in a folder name" }
    if ($f.Name -match '^(nvngx|sl\.)' -or $f.Extension -eq '.dll') { Fail "$rel is not a file of the runtime's data" }
    if ($f.Length -eq 0) { $empty += $rel; continue }
    $folder = ($parts[0..($parts.Count - 2)] -join '.')
    $files.Add([pscustomobject]@{ Path = "$folder/$($f.Name)"; File = $f.FullName })
}
foreach ($f in Get-ChildItem -LiteralPath $From -File) {
    if ($top -notcontains $f.Name) { Write-Host "      left out: $($f.Name)" -ForegroundColor DarkGray }
}
if ($empty.Count) { Write-Host "      left out, empty: $($empty -join ', ')" -ForegroundColor Yellow }

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$stamp = [DateTimeOffset]::new(2026, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$zipName = "hexneural-$Version.zip"
$zipPath = Join-Path $Out $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
$sorted = @($files | Sort-Object Path -Culture ([cultureinfo]::InvariantCulture))
$extract = [System.Collections.Generic.List[object]]::new()
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($f in $sorted) {
        $entry = $zip.CreateEntry($f.Path, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $stamp
        $sink = $entry.Open()
        $source = [IO.File]::OpenRead($f.File)
        try { $source.CopyTo($sink) } finally { $source.Dispose(); $sink.Dispose() }
        $extract.Add([ordered]@{ name = [IO.Path]::GetFileName($f.Path); path = $f.Path; size = (Get-Item -LiteralPath $f.File).Length; sha256 = Sha256File $f.File })
    }
} finally { $zip.Dispose(); $stream.Dispose() }
$zipSha = Sha256File $zipPath
Good "$zipPath  ($($sorted.Count) files, $((Get-Item -LiteralPath $zipPath).Length) bytes, sha256 $zipSha)"
$component = [ordered]@{
    version = $Version
    files   = @([ordered]@{ name = $zipName; size = (Get-Item -LiteralPath $zipPath).Length; sha256 = $zipSha; url = "$base/hexneural/$Version/$zipName" })
    extract = $extract.ToArray()
}

$m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
$target = @($m.releases.optiscaler) | Where-Object { $_.version -eq $Release } | Select-Object -First 1
if (-not $target) {
    if (-not $Placeholder) { Fail "releases.optiscaler has no $Release. Run tools/pin-optiscaler.ps1 with its archive first, or pass -Placeholder" }
    $opti = [ordered]@{ version = $Release; files = @([ordered]@{ name = "OptiScaler-$Release.zip"; size = 1; sha256 = ('0' * 64)
                        url = "https://github.com/MatheusFerreiraS/OpenNR-OptiScaler/releases/download/v$Release/OptiScaler-$Release.zip" }) }
    $target = [pscustomobject]@{ version = $Release; components = [pscustomobject]@{ optiscaler = $opti } }
    $m.releases.optiscaler = @($target) + @($m.releases.optiscaler)
    Good "releases.optiscaler $Release made as a placeholder: pin-optiscaler.ps1 -Version $Release fills it"
}
$target.components | Add-Member -NotePropertyName hexneural -NotePropertyValue $component -Force
[IO.File]::WriteAllText($Manifest, ($m | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllLines((Join-Path $Out 'SHA256SUMS.txt'), @("$zipSha *$zipName"))
Good "$Manifest  (hexneural $Version in $Release)"

Write-Host "`nUpload, before payload.json (needs the owner's go-ahead):" -ForegroundColor Cyan
Write-Host "  hf upload $Repo `"$zipPath`" hexneural/$Version/$zipName --repo-type dataset --commit-message `"HexNeural $Version (Emion, GPL)`""
