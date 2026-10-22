# Handoff v0.5.0: OptiScaler versions and the lmxxf runtime

2026-09-23. Read after [HANDOFF-2026-09-23.md](HANDOFF-2026-09-23.md), which covers the OptiScaler route
of v0.4.0.

---

## 1. What went in

- **Version selector on the OptiScaler route.** The version section, which used to disappear on this route, is now
  called "OptiScaler version" and lists every version that `payload.json` carries, from newest
  to oldest. The choice is saved per game (`GameEntry.OptiScalerVersion`, separate from
  `AddonVersion`) and follows the same rule as the add-on (`AddonReleases.Preferred`): the version the
  game was installed with, then the session's, then the newest.
- **OptiScaler 0.2.0** (neural-amd-opti `v0.2.0-amd-nr`): lmxxf runtime, effect strength, colour
  grade and XeSS multi frame generation. It is the default version.
- **Everything lmxxf needs goes along:** `LmxxfNrRuntime.dll`, `lmxxf-modules\` and `shaders\`
  come out of the release zip; the weights (`native-game-tiled-assets\`, 460 files, 591 MB) come out of
  `native-game-tiled-assets.zip` in the HF dataset. lmxxf only runs on RDNA4 (gfx1201); the installation
  report says so and says how to switch the runtime in the OptiScaler menu.

## 2. How the payload stores more than one version

`payload.json` gained a top-level key, `releases`:

```json
"releases": { "optiscaler": [ { "version": "0.2.0-amd-nr", "components": {
    "optiscaler":    { "version": "0.2.0-amd-nr", "published": "2026-09-23", "files": [zip], "extract": [55] },
    "lmxxf-weights": { "version": "1", "files": [zip], "extract": [460] } } } ] }
```

- `components.optiscaler` stays at 0.1.1. It is what **v0.4.0** reads, and it ignores `releases`
  (unknown key). If 0.2.0 went into `components`, v0.4.0 would try to install it and its
  `Transaction.Apply` would reject the lmxxf names; and the 228 MB of weights would enter its
  first-run wizard, which downloads every component outside `OnDemand`.
- `PayloadManifest.Offered(component)` merges the version from `components` and the ones from `releases`, sorts
  by the numeric part (`0.2.0-amd-nr` becomes `0.2.0`). `With(release)` swaps the components that the
  version carries and keeps the rest (runtime 0.3.1, danielblnc's weights). `Newest(...)` is the default.
- `Parse` validates the components in `releases` like the others (https, SHA-256, path of at most
  two levels) and requires each version to carry its own component at its own version.
- "Outdated" (`PinnedForOptiScaler`) compares against the newest version. Whoever installed 0.1.1 on
  purpose sees the warning, same as someone who pins an old add-on.

## 3. What changed in the engine

- **`Engine.IsAllowed`** replaces `Engine.Allowed.Contains` in the transaction and in `Manifest.Decode`.
  Besides the fixed list (which gained `LmxxfNrRuntime.dll`), it accepts a plain file directly inside
  `lmxxf-modules/` and `native-game-tiled-assets/` (the names come from upstream and change), and only the
  `shaders/native_*.hlsl` in the `shaders/` folder, which is a name other programs use. Nothing
  deeper, nothing that goes up.
- The manifest entry limit is no longer `Allowed.Count` and became `MaxManifestEntries`
  (4096): a 0.2.0 installation has 520 entries.
- **Extraction:** `EnsureAsync` opens the zip once for all entries (before, once per
  entry, which with 460 entries from a 228 MB zip meant minutes of disk). Each entry goes to
  a `.part` with the hash computed along the way, without passing through memory (one weight is 201 MB). The
  entry is found first by the path it is stored under, then by the name as a path, and only
  last by the bare name: the package has `README.md` at the root and in `lmxxf-modules/`.
- **Weights without `HIP\`:** the zip has `native-game-tiled-assets/HIP/gfx120x/`, modules the runtime
  never reads (its own come from `lmxxf-modules`, which has `SHA256SUMS`). Apart from those, everything is two
  levels deep. The runtime reads every `.f32/.f16/.i32` in the folder at once, so the rest goes whole.
- **Uninstall:** removes `lmxxf-modules\`, `native-game-tiled-assets\` and `shaders\` when they end up
  empty, and before that deletes `shaders\shader-cache\` if it only contains `.dxbc` (the runtime compiles those
  during gameplay; nothing records them).
- **Memory:** the engine reads each whole file before writing, so installing 0.2.0 reaches
  1.1 GB of working set for about 6 s. I did not touch the transaction; if it becomes a problem, the way forward is for it to accept
  files on disk instead of `byte[]`.

## 4. Evidence

- 195 green tests (there were 188). The new ones are in `tests/AmdNr.Core.Tests/OptiScalerVersionTests.cs`:
  versions offered and swapped, `releases` validated, `IsAllowed`, installing and uninstalling the whole 0.2.0
  (with `shader-cache`), another program's `shaders\` folder preserved, zip entry by
  path, and the real `payload.json`: every version opens and every file in it passes `IsAllowed`.
- End to end with the real files (harness in `opti/exports/installer-test/harness`, test cache
  seeded with the two zips): installed 0.1.1, updated to 0.2.0 on top of it (the
  untouched `OptiScaler.ini` was replaced by the new one; `dxgi.dll` too), did not flag it as
  outdated, uninstalled everything and restored the original `dxgi.dll`.
- The Cyberpunk card rendered without a window (Avalonia headless): "OptiScaler version" with
  `v0.2.0-amd-nr - 2026-09-23` and `v0.1.1-amd-nr - 2026-09-23`, the lmxxf weights note only on 0.2.0.

## 5. Publishing a new OptiScaler from now on

1. neural-amd-opti release with the zip (never re-upload an asset under a published tag).
2. A new item in `releases.optiscaler`, with `components.optiscaler` at the same version, the
   zip URL and the `extract` recomputed from the zip (name, path of at most two levels, size, SHA-256).
   The script used for 0.2.0 did exactly that from the zip: list what
   `PACKAGE_RELEASE.ps1` puts outside `Licenses/`, `README.md`, `Setup*` and `SHA256SUMS.txt`, and
   map `OptiScaler/D3D12_OptiScaler/D3D12Core.dll` to `D3D12_OptiScaler/D3D12Core.dll`.
3. If the version needs new weights, a new component inside it (new version, new file
   on HF).
4. `dotnet test`: the test on the real `payload.json` catches names the transaction would reject.
5. `tools/publish-payload.ps1 -Repo zmodelerlover/amd-nr`, which now walks `releases` on upload
   and on verification, and rejects a release component whose content changed without a new version.

Moving `components.optiscaler` to 0.2.0 only makes sense once nobody uses v0.4.0 anymore.

## 6. Release v0.5.0

Done in the usual order: installer (exe, `SHA256SUMS.txt`) first, then
`publish-payload.ps1` (with `-From` pointing to a folder containing only `native-game-tiled-assets.zip`),
then the new `payload.json` copied into `publish-v0.5.0\` and the `.7z` assembled and attached. The
`config.json` that the script rewrites was reverted. Extras received the new `payload.json`.

- The `.7z` carries an `AMD-NR-ReShade-Installer-v0.5.0\` folder with the exe, the three json files and a
  `LEIA-ME.txt`/`README.txt` rewritten for v0.5.0 (the v0.4.0 ones were still from the 2026-09-15 beta).
- `tools/check-release.ps1` has to run in PowerShell 7: in 5.1 `Get-FileHash` does not load
  when `PSModulePath` points to the 7 modules. It gave "Versions agree".
- After publishing, the harness ran again with an empty cache and the manifest read from HF: it downloaded
  the 0.2.0 zip from GitHub and the weights from HF, checked everything, installed 0.1.1, updated to
  0.2.0 and uninstalled.
- The in-game proof through the window is still missing, like the v0.4.0 one in Cyberpunk. There the folder has a
  manual test installation; `opti/exports/cyberpunk-test/remove-test.ps1` undoes it first.
