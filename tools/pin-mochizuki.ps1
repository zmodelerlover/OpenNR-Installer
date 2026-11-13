#Requires -Version 7
<#
.SYNOPSIS
  Pins the mochizuki runtime of one OptiScaler release in payload.json from a folder of its final
  files, and builds the archive that gets uploaded.

.DESCRIPTION
  -From is laid out the way a game folder receives it:

      MochizukiNrRuntime.dll
      dlssnr-amd\dlssnr.bin                  the model
      dlssnr-amd\dlssnr-small.bin            the second model (NVIDIA 310.9.16, optional; OptiScaler 0.5.4 on)
      dlssnr-amd\prewarm\manifest.txt        the prewarm list, made for these shaders
      dlssnr-amd\shaders\...                 files, and one level of folders under it
      dlssnr-amd\aco\...                     ACO's networks (optional), three folder levels (aco\optimized\shaders\temporal)
      dlssnr-amd\int4\...                    the optimized network's int4 levels (optional), two folder levels, and
      dlssnr-amd\int4-performance\...        its weights made at first use from dlssnr.bin: no dlssnr-int4.bin ships
      dlssnr-amd\int4-ultra\...              from mochizuki 0.0.8.3 on
      dlssnr-amd\shaders-rdna3\...           the RX 7000 network (optional), one level of folders under it
      dlssnr-amd\shaders-rdna2\...           the RX 6000 network (optional), one level of folders under it
      dlssnr-amd\shaders-rdna2-int8\...      its INT8 form (optional, with shaders-rdna2), in the RX 6000 archive
      dlssnr-amd\prewarm\manifest-rdna3.txt  the RX 7000 network's prewarm list (optional, with shaders-rdna3)
      dlssnr-amd\prewarm\manifest-rdna2.txt  the RX 6000 network's prewarm list (optional, with shaders-rdna2)
      dlssnr-amd\prewarm\manifest-rdna2-int8.txt  the INT8 form's (optional, with shaders-rdna2-int8)

  Anything else at the top of -From (OptiScaler.dll, notes, a build's .lib, .exp and obj\) is ignored.

  -License is DLSSNR-AMD's MIT licence (third_party\mochizuki\LICENSE in neural-amd-opti). The
  archive ships code built from DLSSNR-AMD, so it carries its notice, as
  dlssnr-amd\LICENSE-DLSSNR-AMD.txt: beside the shaders and never among them, because the prewarm
  list is checked against every file in shaders\. What this does:

    1. refuses what the runtime writes while a game runs (pipeline.cache, *.tmp, *.log) and what the
       app could not install (anything deeper, a folder name with a dot, a name over 96 characters);
    2. checks the prewarm list was made for these shaders: the file count on its "shaders" line;
    3. writes <Out>\mochizuki-<version>.zip -- the runtime, the prewarm list, the licence and the
       shaders under the payload paths the app keeps them at (dlssnr-amd.shaders.runtime/x.spv for
       dlssnr-amd\shaders\runtime\x.spv, see Work.MochizukiDestination), in ordinal order and with a
       fixed timestamp, so the same files always make the same archive on any machine;
    4. rewrites releases.optiscaler[<version>].components.mochizuki (the archive and every file in it),
       mochizuki-model (dlssnr.bin, loose) and, when -From has it, mochizuki-model-small (dlssnr-small.bin,
       loose) in payload.json;
       the RX 7000 network (shaders-rdna3) goes in an archive and a component of its own,
       mochizuki-rdna3-<version>.zip and releases.optiscaler[<version>].components.mochizuki-rdna3: the installers
       already published install every file of the mochizuki component on every RX 9000, and never ask for a
       component they do not know, so only those with the RX 7000 opt-in fetch it; the RX 6000 network
       (shaders-rdna2) goes the same way, as mochizuki-rdna2-<version>.zip and mochizuki-rdna2. Each network's
       prewarm list (manifest-rdna3.txt, manifest-rdna2.txt) rides in its network's archive, checked like manifest.txt;
    5. prints the upload command for each file it pinned, at the path its url names.

  The model has an address of its own per model version, because published addresses are
  overwritable and versions that coexist need names that coexist. The same bytes keep the version
  already pinned; other bytes need -ModelVersion. The second model goes the same way, at
  mochizuki/model-small-<version>/dlssnr-small.bin, with -SmallModelVersion. Both are NVIDIA-derived and
  hosted loose like this, never inside the archive.

.EXAMPLE
  .\tools\pin-mochizuki.ps1 -From <release build>\candidate\runtime -License <neural-amd-opti>\third_party\mochizuki\LICENSE
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $From,
    [string] $License = '',
    [string] $Version = '0.4.0-amd-nr',
    [string] $ModelVersion = '',
    [string] $SmallModelVersion = '',
    [string] $Out = '',
    [string] $Manifest = '',
    [string] $Repo = 'zmodelerlover/amd-nr'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Manifest) { $Manifest = Join-Path $root 'payload\payload.json' }
if (-not $Out) { $Out = Join-Path $root "publish-mochizuki-$Version" }   # publish-*/ is ignored by git
$base = "https://huggingface.co/datasets/$Repo/resolve/main"
$zero = '0' * 64
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
$runtime = Join-Path $From 'MochizukiNrRuntime.dll'
$data = Join-Path $From 'dlssnr-amd'
$model = Join-Path $data 'dlssnr.bin'
$prewarm = Join-Path $data 'prewarm\manifest.txt'
$shaders = Join-Path $data 'shaders'
foreach ($need in $runtime, $model, $prewarm) { if (-not (Test-Path -LiteralPath $need -PathType Leaf)) { Fail "$need is missing" } }
if (-not (Test-Path -LiteralPath $shaders -PathType Container)) { Fail "$shaders is missing" }
$ignored = Get-ChildItem -LiteralPath $From -Force | Where-Object { $_.Name -notin 'MochizukiNrRuntime.dll', 'dlssnr-amd' }
if ($ignored) { Note "ignored at the top of -From: $(($ignored | ForEach-Object Name) -join ', ')" }

# DLSSNR-AMD is MIT: whoever passes its code on passes its notice on with it.
if (-not $License) {
    Fail "pass -License with DLSSNR-AMD's LICENSE (third_party\mochizuki\LICENSE in neural-amd-opti): the archive ships code built from it and must carry its notice"
}
if (-not (Test-Path -LiteralPath $License -PathType Leaf)) { Fail "$License is missing" }
$License = (Resolve-Path -LiteralPath $License).Path
$notice = [IO.File]::ReadAllText($License)
if ($notice -notmatch 'MIT License' -or $notice -notmatch 'mochizuki0323' -or $notice -notmatch 'Permission is hereby granted') {
    Fail "$License is not DLSSNR-AMD's MIT licence: it lacks 'MIT License', 'mochizuki0323' or the permission notice"
}

# --- 1. only what ships, only where the app can put it ------------------------------------------
$invalid = [IO.Path]::GetInvalidFileNameChars()
function Plain($name, $what) {
    if ($name.Length -eq 0 -or $name.Length -gt 96 -or $name -in '.', '..' -or $name.IndexOfAny($invalid) -ge 0) { Fail "unusable $what name: '$name'" }
}
function Written($name) { $name -eq 'pipeline.cache' -or $name -like '*.tmp' -or $name -like '*.log' }

$top = Get-ChildItem -LiteralPath $data -Force
foreach ($item in $top) {
    $ok = ($item.PSIsContainer -and $item.Name -in 'prewarm', 'shaders', 'shaders-rdna3', 'shaders-rdna2', 'shaders-rdna2-int8', 'aco', 'int4', 'int4-performance', 'int4-ultra') -or
          (-not $item.PSIsContainer -and $item.Name -in 'dlssnr.bin', 'dlssnr-small.bin')
    # mochizuki 0.0.8.3 on makes the int4 weights at first use (aco-cache\dlssnr-optimized-int4.nrb).
    if ($item.Name -eq 'dlssnr-int4.bin') { Fail "dlssnr-amd\dlssnr-int4.bin no longer ships: the runtime makes the int4 weights itself. Leave it out of -From" }
    if (-not $ok) { Fail "dlssnr-amd\$($item.Name) is not part of a release$(if (Written $item.Name) { ': the runtime wrote it while a game ran' })" }
}
$extra = Get-ChildItem -LiteralPath (Join-Path $data 'prewarm') -Force | Where-Object { $_.Name -notin 'manifest.txt', 'manifest-rdna3.txt', 'manifest-rdna2.txt', 'manifest-rdna2-int8.txt' }
if ($extra) { Fail "dlssnr-amd\prewarm holds more than its lists: $(($extra | ForEach-Object Name) -join ', ')" }

$files = [System.Collections.Generic.List[object]]::new()   # payload path -> file on disk
$files.Add([pscustomobject]@{ Path = 'MochizukiNrRuntime.dll'; File = $runtime })
$files.Add([pscustomobject]@{ Path = 'dlssnr-amd.prewarm/manifest.txt'; File = $prewarm })
$files.Add([pscustomobject]@{ Path = 'dlssnr-amd/LICENSE-DLSSNR-AMD.txt'; File = $License })
$shaderCount = 0
foreach ($item in Get-ChildItem -LiteralPath $shaders -Force) {
    if (-not $item.PSIsContainer) {
        if (Written $item.Name) { Fail "dlssnr-amd\shaders\$($item.Name) was written by the runtime, it does not ship" }
        Plain $item.Name 'file'
        $files.Add([pscustomobject]@{ Path = "dlssnr-amd.shaders/$($item.Name)"; File = $item.FullName })
        $shaderCount++
        continue
    }
    Plain $item.Name 'folder'
    if ($item.Name.Contains('.')) { Fail "dlssnr-amd\shaders\$($item.Name): a folder name with a dot cannot be spelled in a payload path" }
    foreach ($inner in Get-ChildItem -LiteralPath $item.FullName -Force) {
        if ($inner.PSIsContainer) { Fail "dlssnr-amd\shaders\$($item.Name)\$($inner.Name) is deeper than the app installs" }
        if (Written $inner.Name) { Fail "dlssnr-amd\shaders\$($item.Name)\$($inner.Name) was written by the runtime, it does not ship" }
        Plain $inner.Name 'file'
        $files.Add([pscustomobject]@{ Path = "dlssnr-amd.shaders.$($item.Name)/$($inner.Name)"; File = $inner.FullName })
        $shaderCount++
    }
}
# dlssnr-amd\aco\: ACO's networks (OptiScaler's MochizukiAco), files and three levels of folders under it
# (records\, shaders\temporal\, int4\shaders\temporal\, optimized\shaders\temporal\), spelled with dots like the shaders.
function AddTree($dir, $payload, $levels) {
    foreach ($item in Get-ChildItem -LiteralPath $dir -Force) {
        $at = "$dir\$($item.Name)".Substring($data.Length + 1)
        if (Written $item.Name) { Fail "dlssnr-amd\$at was written by the runtime, it does not ship" }
        Plain $item.Name $(if ($item.PSIsContainer) { 'folder' } else { 'file' })
        if (-not $item.PSIsContainer) { $files.Add([pscustomobject]@{ Path = "$payload/$($item.Name)"; File = $item.FullName }); continue }
        if ($levels -eq 0) { Fail "dlssnr-amd\$at is deeper than the app installs" }
        if ($item.Name.Contains('.')) { Fail "dlssnr-amd\${at}: a folder name with a dot cannot be spelled in a payload path" }
        AddTree $item.FullName "$payload.$($item.Name)" ($levels - 1)
    }
}
$aco = Join-Path $data 'aco'
if (Test-Path -LiteralPath $aco) { AddTree $aco 'dlssnr-amd.aco' 3 }
# The optimized network's int4 levels (MochizukiOptimizedLayers): tables, settings and shaders in int4\,
# int4-performance\ and int4-ultra\ (two levels: vit\, swin\, shaders\temporal\). The runtime makes the int4 weights
# from dlssnr.bin with int4-ultra's tables, so aco\optimized\ is no use without int4-ultra\.
foreach ($level in 'int4', 'int4-performance', 'int4-ultra') {
    $tree = Join-Path $data $level
    if (Test-Path -LiteralPath $tree) { AddTree $tree "dlssnr-amd.$level" 2 }
}
if ((Test-Path -LiteralPath (Join-Path $data 'aco\optimized')) -and -not (Test-Path -LiteralPath (Join-Path $data 'int4-ultra'))) {
    Fail "dlssnr-amd\aco\optimized\ without dlssnr-amd\int4-ultra\: the runtime makes the optimized network's int4 weights from its tables"
}
# The RX 7000 and RX 6000 networks: their shaders, runtime\ and temporal\, and their prewarm lists, each in an archive
# of its own (see 4.).
$fp16 = foreach ($tag in 'rdna3', 'rdna2', 'rdna2-int8') {
    $tree = Join-Path $data "shaders-$tag"
    $list = Join-Path $data "prewarm\manifest-$tag.txt"
    $hasTree = Test-Path -LiteralPath $tree
    if ((Test-Path -LiteralPath $list) -and -not $hasTree) { Fail "dlssnr-amd\prewarm\manifest-$tag.txt without dlssnr-amd\shaders-$tag" }
    if (-not $hasTree) { continue }
    $before = $files.Count
    AddTree $tree "dlssnr-amd.shaders-$tag" 1
    $count = $files.Count - $before
    if (Test-Path -LiteralPath $list) { $files.Add([pscustomobject]@{ Path = "dlssnr-amd.prewarm/manifest-$tag.txt"; File = $list }) }
    else { Note "dlssnr-amd\shaders-$tag has no prewarm list: the network works, its first start compiles twice" }
    [pscustomobject]@{ Tag = $tag; Count = $count; List = $(if (Test-Path -LiteralPath $list) { $list }) }
}
foreach ($f in $files) { if ($f.Path.Length -gt 128) { Fail "payload path over 128 characters: $($f.Path)" } }
# Ordinal, not Sort-Object's culture order: the archive's bytes must not depend on the machine's language.
$files.Sort([Comparison[object]] { param($a, $b) [string]::CompareOrdinal($a.Path, $b.Path) })
function OfNetwork($path, $tag) {
    $path.StartsWith("dlssnr-amd.shaders-$tag", [StringComparison]::Ordinal) -or $path.StartsWith("dlssnr-amd.prewarm/manifest-$tag", [StringComparison]::Ordinal)
}
$rdna3Files = @($files | Where-Object { OfNetwork $_.Path 'rdna3' })
$rdna2Files = @($files | Where-Object { OfNetwork $_.Path 'rdna2' })
$files = @($files | Where-Object { -not (OfNetwork $_.Path 'rdna3') -and -not (OfNetwork $_.Path 'rdna2') })

# --- 2. the prewarm lists are for these shaders ---------------------------------------------------
function CheckList($list, $folder, $count) {
    $head = (Get-Content -LiteralPath $list -TotalCount 4) -join "`n"
    if ($head -notmatch '(?m)^mochizuki-prewarm \d+') { Fail "$list is not a mochizuki prewarm list" }
    if ($head -notmatch '(?m)^shaders [0-9a-f]+ (\d+)$') { Fail "$list has no shaders line" }
    if ([int]$Matches[1] -ne $count) {
        Fail "$(Split-Path -Leaf $list) was made for $($Matches[1]) shader files and dlssnr-amd\$folder has $count. The runtime would ignore it; make it again from these shaders"
    }
    Good "$(Split-Path -Leaf $list) made for these $count shader files"
}
CheckList $prewarm 'shaders' $shaderCount
foreach ($n in $fp16) { if ($n.List) { CheckList $n.List "shaders-$($n.Tag)" $n.Count } }

# --- 3. the archive --------------------------------------------------------------------------------
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$stamp = [DateTimeOffset]::new(2026, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
# One archive: the files in the order given, a fixed timestamp, and its extract list.
function Archive($zipName, $list) {
    $zipPath = Join-Path $Out $zipName
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    $extract = [System.Collections.Generic.List[object]]::new()
    $stream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($f in $list) {
            $entry = $zip.CreateEntry($f.Path, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $stamp
            $sink = $entry.Open()
            $source = [IO.File]::OpenRead($f.File)
            try { $source.CopyTo($sink) } finally { $source.Dispose(); $sink.Dispose() }
            $extract.Add([ordered]@{ name = [IO.Path]::GetFileName($f.Path); path = $f.Path; size = (Get-Item -LiteralPath $f.File).Length; sha256 = Sha256File $f.File })
        }
    } finally { $zip.Dispose(); $stream.Dispose() }
    return [pscustomobject]@{ Name = $zipName; Path = $zipPath; Sha = (Sha256File $zipPath); Size = (Get-Item -LiteralPath $zipPath).Length; Extract = $extract.ToArray() }
}
$zipName = "mochizuki-$Version.zip"
$main = Archive $zipName $files
$zipPath = $main.Path; $zipSha = $main.Sha; $zipSize = $main.Size; $extract = $main.Extract
Good "$zipPath  ($($files.Count) files, $zipSize bytes)"
$rdna3Zip = if ($rdna3Files.Count) { Archive "mochizuki-rdna3-$Version.zip" $rdna3Files }
if ($rdna3Zip) { Good "$($rdna3Zip.Path)  ($($rdna3Files.Count) files, $($rdna3Zip.Size) bytes)" }
$rdna2Zip = if ($rdna2Files.Count) { Archive "mochizuki-rdna2-$Version.zip" $rdna2Files }
if ($rdna2Zip) { Good "$($rdna2Zip.Path)  ($($rdna2Files.Count) files, $($rdna2Zip.Size) bytes)" }

# --- 4. payload.json -------------------------------------------------------------------------------
$m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
$releases = @($m.releases.optiscaler)
$release = $releases | Where-Object { $_.version -eq $Version } | Select-Object -First 1
if (-not $release) { Fail "releases.optiscaler has no $Version. Run tools/pin-optiscaler.ps1 with its archive first" }

# One loose model: the version already pinned when the bytes are the same, a new one only when asked for.
function ModelPin($file, $component, $folder, $wanted, $flag) {
    $name = Split-Path -Leaf $file
    $sha = Sha256File $file
    $size = (Get-Item -LiteralPath $file).Length
    $pinned = $releases | ForEach-Object { $_.components.PSObject.Properties[$component] } | Where-Object { $_ } |
        ForEach-Object { $_.Value } | Select-Object -First 1
    $version = if ($wanted) { $wanted } elseif ($pinned) { $pinned.version } else { '1' }
    if ($pinned -and $pinned.version -eq $version) {
        if ($pinned.files[0].sha256 -eq $sha) { Note "$name is the model already pinned as version $version"; return $pinned }
        if ($wanted) { Fail "$component version $version is already pinned to other bytes; a published address never gets new bytes" }
        Fail "$name is not the model pinned as version $version. Pass $flag with a new version for it"
    }
    return [ordered]@{ version = $version; files = @([ordered]@{
        name = $name; path = "dlssnr-amd/$name"; size = $size; sha256 = $sha
        url = "$base/mochizuki/$folder-$version/$name" }) }
}
$modelComponent = ModelPin $model 'mochizuki-model' 'model' $ModelVersion '-ModelVersion'
$modelSha = $modelComponent.files[0].sha256
$modelSize = $modelComponent.files[0].size
$small = Join-Path $data 'dlssnr-small.bin'
$smallComponent = if (Test-Path -LiteralPath $small) { ModelPin $small 'mochizuki-model-small' 'model-small' $SmallModelVersion '-SmallModelVersion' }

$old = $release.components.PSObject.Properties['mochizuki']
if ($old -and ($old.Value.files | Where-Object { $_.sha256 -ne $zero -and $_.sha256 -ne $zipSha })) {
    Write-Host "      mochizuki $Version was pinned to another archive before. If that one was ever uploaded and" -ForegroundColor Yellow
    Write-Host "      its payload.json published, the same address now serves other bytes: publish a new version instead." -ForegroundColor Yellow
}
$component = [ordered]@{
    version = $Version
    files   = @([ordered]@{ name = $zipName; size = $zipSize; sha256 = $zipSha; url = "$base/mochizuki/$Version/$zipName" })
    extract = $extract
}
$release.components | Add-Member -NotePropertyName mochizuki -NotePropertyValue $component -Force
$release.components | Add-Member -NotePropertyName 'mochizuki-model' -NotePropertyValue $modelComponent -Force
if ($smallComponent) { $release.components | Add-Member -NotePropertyName 'mochizuki-model-small' -NotePropertyValue $smallComponent -Force }
elseif ($release.components.PSObject.Properties['mochizuki-model-small']) { $release.components.PSObject.Properties.Remove('mochizuki-model-small') }
foreach ($n in @(@{ Name = 'mochizuki-rdna3'; Zip = $rdna3Zip }, @{ Name = 'mochizuki-rdna2'; Zip = $rdna2Zip })) {
    if ($n.Zip) {
        $release.components | Add-Member -NotePropertyName $n.Name -NotePropertyValue ([ordered]@{
            version = $Version
            files   = @([ordered]@{ name = $n.Zip.Name; size = $n.Zip.Size; sha256 = $n.Zip.Sha; url = "$base/mochizuki/$Version/$($n.Zip.Name)" })
            extract = $n.Zip.Extract
        }) -Force
    }
    elseif ($release.components.PSObject.Properties[$n.Name]) { $release.components.PSObject.Properties.Remove($n.Name) }
}
[IO.File]::WriteAllText($Manifest, ($m | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
Good "$Manifest"
Note "mochizuki $Version`: $zipName $zipSha"
Note "mochizuki-model $($modelComponent.version): dlssnr.bin $modelSize bytes $modelSha"

$sums = @("$zipSha *$zipName", "$modelSha *dlssnr.bin")
if ($rdna3Zip) {
    Note "mochizuki-rdna3 $Version`: $($rdna3Zip.Name) $($rdna3Zip.Sha)"
    $sums += "$($rdna3Zip.Sha) *$($rdna3Zip.Name)"
}
if ($rdna2Zip) {
    Note "mochizuki-rdna2 $Version`: $($rdna2Zip.Name) $($rdna2Zip.Sha)"
    $sums += "$($rdna2Zip.Sha) *$($rdna2Zip.Name)"
}
if ($smallComponent) {
    Note "mochizuki-model-small $($smallComponent.version): dlssnr-small.bin $($smallComponent.files[0].size) bytes $($smallComponent.files[0].sha256)"
    $sums += "$($smallComponent.files[0].sha256) *dlssnr-small.bin"
}
[IO.File]::WriteAllLines((Join-Path $Out 'SHA256SUMS.txt'), $sums)

# --- 5. where each goes ----------------------------------------------------------------------------
$modelInRepo = $modelComponent.files[0].url.Substring($base.Length + 1)
Write-Host "`nUpload, before payload.json:" -ForegroundColor Cyan
Write-Host "  hf upload $Repo `"$zipPath`" mochizuki/$Version/$zipName --repo-type dataset --commit-message `"mochizuki $Version`: runtime, shaders, prewarm list and licence`""
if ($rdna3Zip) {
    Write-Host "  hf upload $Repo `"$($rdna3Zip.Path)`" mochizuki/$Version/$($rdna3Zip.Name) --repo-type dataset --commit-message `"mochizuki $Version`: the RX 7000 network (experimental)`""
}
if ($rdna2Zip) {
    Write-Host "  hf upload $Repo `"$($rdna2Zip.Path)`" mochizuki/$Version/$($rdna2Zip.Name) --repo-type dataset --commit-message `"mochizuki $Version`: the RX 6000 network (experimental)`""
}
Write-Host "  hf upload $Repo `"$model`" $modelInRepo --repo-type dataset --commit-message `"mochizuki model $($modelComponent.version)`""
if ($smallComponent) {
    $smallInRepo = $smallComponent.files[0].url.Substring($base.Length + 1)
    Write-Host "  hf upload $Repo `"$small`" $smallInRepo --repo-type dataset --commit-message `"mochizuki model-small $($smallComponent.version) (310.9.16)`""
}
$left = foreach ($c in $release.components.PSObject.Properties) {
    if (@($c.Value.files) + @($c.Value.extract) | Where-Object { $_ -and $_.sha256 -eq $zero }) { $c.Name }
}
if ($left) { Write-Host "`n      still placeholders in $Version`: $($left -join ', ') -- the app hides this version until they are pinned" -ForegroundColor Yellow }
else { Good "nothing in $Version is a placeholder any more" }
