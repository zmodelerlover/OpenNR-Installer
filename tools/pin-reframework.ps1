#Requires -Version 7
<#
.SYNOPSIS
  Pins REFramework (praydog, MIT) builds in payload.json's releases.reframework, from a GitHub release or nightly.

.DESCRIPTION
  REFramework publishes per-game zips on its releases (RE2.zip, RE9.zip, DMC5.zip, RE2_TDB66.zip, ...) and one zip
  for every game on its nightlies (REFramework.zip, github.com/praydog/REFramework-nightly). Either way the app
  installs one file per game, dinput8.dll, beside REFramework's LICENSE. What this does:

    1. reads the release (-Release v1.5.9.1) or the nightly (-Nightly 1437) through GitHub's API;
    2. downloads each zip it needs into <Out>\download\ and checks it against the digest GitHub gives for it;
    3. takes dinput8.dll out of it (nothing else is unpacked, nothing is run) and checks it is a 64-bit DLL that
       says it is REFramework; on a nightly every build gets the same file;
    4. fetches LICENSE at the commit the build was made from and checks it is the MIT licence;
    5. writes releases.reframework[<version>]: "reframework" (LICENSE.txt) at the release's version and one
       "reframework-<build>" per build (its dinput8.dll), files loose under reframework/<version>/ in the dataset;
    6. prints the upload commands. Nothing is uploaded here.

  The version is the release's ("1.5.9.1"), or for a nightly the last release's first three numbers with the
  nightly's number and commit ("1.5.9.1437-nightly-f9cb2ac"): it sorts after the release it follows. Builds
  default to every build api-db.json's "reframework" list names (a nightly), or every zip the release has whose
  name is a build there (a release). The app takes each game's build from the newest release that carries it,
  so a release pinning one game's build leaves the others where they were.

  -Url replaces the dataset base, for a test payload whose files sit beside the app.

.EXAMPLE
  .\tools\pin-reframework.ps1 -Nightly 1437
  .\tools\pin-reframework.ps1 -Release v1.5.9.1 -Builds RE2,RE2_TDB66
  .\tools\pin-reframework.ps1 -Nightly 1437 -Builds RE9 -Manifest <test>\app\payload.json
#>
[CmdletBinding()]
param(
    [string] $Release = '',
    [int] $Nightly = 0,
    [string[]] $Builds = @(),
    [string] $Base = '',
    [string] $Out = '',
    [string] $Manifest = '',
    [string] $ApiDb = '',
    [string] $Url = '',
    [string] $Repo = 'zmodelerlover/amd-nr'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Manifest) { $Manifest = Join-Path $root 'payload\payload.json' }
if (-not $ApiDb) { $ApiDb = Join-Path $root 'payload\api-db.json' }
$dataset = if ($Url) { $Url.TrimEnd('/') } else { "https://huggingface.co/datasets/$Repo/resolve/main" }
function Fail($m) { Write-Host "FAIL  $m" -ForegroundColor Red; exit 1 }
function Good($m) { Write-Host "OK    $m" -ForegroundColor Green }
function Sha256File($path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $s = [IO.File]::OpenRead($path)
    try { return [Convert]::ToHexString($sha.ComputeHash($s)).ToLowerInvariant() } finally { $s.Dispose(); $sha.Dispose() }
}
function Api($path) { Invoke-RestMethod -Headers @{ 'User-Agent' = 'pin-reframework'; Accept = 'application/vnd.github+json' } "https://api.github.com/repos/$path" }

if ([bool]$Release -eq [bool]$Nightly) { Fail 'give -Release <tag> or -Nightly <number>, one of them' }
$listed = @(([IO.File]::ReadAllText($ApiDb) | ConvertFrom-Json).reframework | ForEach-Object { $_.build } | Where-Object { $_ })
if ($listed.Count -eq 0) { Fail "$ApiDb has no reframework list" }

if ($Nightly) {
    $prefix = 'nightly-{0:D5}-' -f $Nightly
    $rel = (Api 'praydog/REFramework-nightly/releases?per_page=100') | ForEach-Object { $_ } | Where-Object { $_.tag_name.StartsWith($prefix) } | Select-Object -First 1
    if (-not $rel) { Fail "no nightly $prefix* in the newest 100 of praydog/REFramework-nightly" }
    $commit = $rel.tag_name.Substring($prefix.Length)
    if (-not $Base) { $Base = (Api 'praydog/REFramework/releases/latest').tag_name }
    $three = ($Base.TrimStart('v', 'V') -split '[.-]' | Select-Object -First 3) -join '.'
    $version = "$three.$Nightly-nightly-$($commit.Substring(0, 7))"
    if (-not $Builds) { $Builds = $listed }
    $zips = @{ '*' = 'REFramework.zip' }
} else {
    $rel = Api "praydog/REFramework/releases/tags/$Release"
    $commit = $Release
    $version = $Release.TrimStart('v', 'V')
    $names = @($rel.assets | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.name) })
    if (-not $Builds) { $Builds = $names | Where-Object { $listed -contains $_ } }
    $zips = @{}
    foreach ($b in $Builds) { $zips[$b] = "$b.zip" }
}
if ($Builds.Count -eq 0) { Fail 'no build to pin' }
$Builds = @($Builds | ForEach-Object { $_.Trim().ToUpperInvariant() } | Sort-Object -Unique)
$unknown = $Builds | Where-Object { $listed -notcontains $_ }
if ($unknown) { Write-Host "note  not in api-db.json's list yet: $($unknown -join ', ')" -ForegroundColor Yellow }
if ($version -notmatch '^\d+\.\d+\.\d+(\.\d+)?(-[0-9A-Za-z.-]+)?$') { Fail "unusable version $version" }
if (-not $Out) { $Out = Join-Path $root "publish-reframework-$version" }   # publish-*/ is ignored by git
New-Item -ItemType Directory -Force -Path (Join-Path $Out 'download') | Out-Null

$mark = [Text.Encoding]::ASCII.GetBytes('REFramework: Unable to load the original dinput8')
function Take($zipPath, $to) {
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entry = @($zip.Entries | Where-Object { $_.FullName -eq 'dinput8.dll' })
        if ($entry.Count -ne 1) { Fail "$zipPath has no dinput8.dll at its root" }
        if ($entry[0].Length -gt 256MB) { Fail "$zipPath's dinput8.dll is $($entry[0].Length) bytes" }
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $to) | Out-Null
        $sink = [IO.File]::Create($to); $source = $entry[0].Open()
        try { $source.CopyTo($sink) } finally { $source.Dispose(); $sink.Dispose() }
    } finally { $zip.Dispose() }
    $bytes = [IO.File]::ReadAllBytes($to)
    $pe = [BitConverter]::ToInt32($bytes, 0x3c)
    if ($bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a -or [BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664) { Fail "$to is not a 64-bit DLL" }
    if ([Text.Encoding]::ASCII.GetString($bytes).IndexOf([Text.Encoding]::ASCII.GetString($mark)) -lt 0) { Fail "$to does not say it is REFramework" }
}

$dlls = @{}
foreach ($key in $zips.Keys) {
    $asset = $rel.assets | Where-Object { $_.name -eq $zips[$key] } | Select-Object -First 1
    if (-not $asset) { Fail "$($rel.tag_name) has no $($zips[$key])" }
    $zipPath = Join-Path $Out "download\$($asset.name)"
    Invoke-WebRequest -UseBasicParsing -Uri $asset.browser_download_url -OutFile $zipPath
    if ($asset.digest -and $asset.digest -ne "sha256:$(Sha256File $zipPath)") { Fail "$($asset.name) does not match GitHub's $($asset.digest)" }
    $sub = if ($key -eq '*') { 'dinput8.dll' } else { "$key/dinput8.dll" }
    $dll = Join-Path $Out $sub
    Take $zipPath $dll
    $dlls[$key] = @{ File = $dll; Path = $sub }
    Good "$($asset.name) -> $dll"
}

$license = Join-Path $Out 'LICENSE.txt'
Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/praydog/REFramework/$commit/LICENSE" -OutFile $license
if (-not ([IO.File]::ReadAllText($license).StartsWith('MIT License'))) { Fail "LICENSE at $commit is not the MIT licence" }
Good "LICENSE at $commit is MIT"

function Pin($file, $at) { [ordered]@{ name = [IO.Path]::GetFileName($file); path = "reframework/$([IO.Path]::GetFileName($file))"
    size = (Get-Item -LiteralPath $file).Length; sha256 = (Sha256File $file); url = "$dataset/reframework/$version/$at" } }
$components = [ordered]@{ reframework = [ordered]@{ version = $version; files = @(Pin $license 'LICENSE.txt') } }
foreach ($b in $Builds) {
    $d = if ($dlls.ContainsKey('*')) { $dlls['*'] } else { $dlls[$b] }
    $components["reframework-$($b.ToLowerInvariant())"] = [ordered]@{ version = $version; files = @(Pin $d.File $d.Path) }
}

$m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
if (-not $m.releases) { $m | Add-Member -NotePropertyName releases -NotePropertyValue ([pscustomobject]@{}) }
$list = @($m.releases.reframework | Where-Object { $_ -and $_.version -ne $version })
$list = @([pscustomobject]@{ version = $version; components = $components }) + $list
$m.releases | Add-Member -NotePropertyName reframework -NotePropertyValue $list -Force
[IO.File]::WriteAllText($Manifest, ($m | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
Good "$Manifest  (reframework ${version}: $($Builds -join ', '))"

Write-Host "`nUpload, before payload.json (each needs the owner's go-ahead):" -ForegroundColor Cyan
Write-Host "  hf upload $Repo `"$license`" reframework/$version/LICENSE.txt --repo-type dataset --commit-message `"REFramework $version licence`""
foreach ($d in $dlls.Values) {
    Write-Host "  hf upload $Repo `"$($d.File)`" reframework/$version/$($d.Path) --repo-type dataset --commit-message `"REFramework $version`""
}
