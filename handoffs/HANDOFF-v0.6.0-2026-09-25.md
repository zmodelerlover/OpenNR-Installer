# Handoff v0.6.0: the mochizuki runtime on the OptiScaler route

2026-09-25. Read after [HANDOFF-v0.5.0-2026-09-23.md](HANDOFF-v0.5.0-2026-09-23.md), which covers the
OptiScaler version menu and lmxxf. This file covers only what v0.6.0 adds.

---

## 1. What went in

- **mochizuki, the third NR runtime on the OptiScaler route** (experimental). It is the Vulkan port of the
  DLSS 5 network made by mochizuki0323 (DLSSNR-AMD), compiled by neural-amd-opti as
  `MochizukiNrRuntime.dll`, which reads a `dlssnr-amd\` folder next to itself: `shaders\` (54 files, with
  `runtime\` and `temporal\`), `prewarm\manifest.txt` and the `dlssnr.bin` model (147,756,560 bytes). It only
  runs on RDNA4 (`VK_KHR_cooperative_matrix` + `VK_EXT_shader_float8`, AMD driver 25.10+).
- **OptiScaler 0.4.0-amd-nr** in the version list, with the pins still as placeholders (§3): the app
  does not offer the version until the publishing stage fills them in.
- **On the card**, below the version menu, the **NR runtime** section appears when the chosen version
  carries mochizuki: a checkbox, unchecked by default and remembered per game (`GameEntry.Mochizuki`,
  `bool?`). Checked, the installation downloads `mochizuki` and `mochizuki-model`, puts the files in the game and
  writes `[DlssNr] NrBackend=mochizuki` into `OptiScaler.ini`. **Unchecked, the next installation removes** what
  a previous installation put in for mochizuki (§4). With no recorded choice (`null`: game added
  again, `games.json` set aside, another PC) the checkbox follows the folder (`Work.HasMochizuki`), so an
  update does not take mochizuki away from whoever had it.
- **RDNA4:** `GpuService` now reads the device id (`SystemState.Rdna4`: Navi 48/44 by id, or
  "RX 9xxx" / "AI PRO R9xxx" in the name). Known card that is not RDNA4: the checkbox is disabled and the note
  says why. Unknown card: the checkbox stays available and the note warns. The installation logs and the
  problem report record `rdna4`, and the report now includes `mochizuki_nr.log`.

## 2. How the payload stores mochizuki

Two components inside `releases.optiscaler[0.4.0-amd-nr]`, never in `components` (v0.4.0 and the
first-run wizard would download everything that is there):

| component | version | what |
|---|---|---|
| `mochizuki` | `0.4.0-amd-nr` | `mochizuki-0.4.0-amd-nr.zip` with `extract` pinning the 57 files (runtime, prewarm, 54 shaders and the DLSSNR-AMD MIT license in `dlssnr-amd/LICENSE-DLSSNR-AMD.txt`) |
| `mochizuki-model` | `1` | loose `dlssnr.bin`, like `dlssnr_on_amd_weights.bin` |

- **Paths with dots.** The runtime reads files three levels below the game folder, and every app already
  published (v0.5.0 and v0.5.1 validate `releases` too) rejects the whole manifest if a path has
  more than two levels. So the payload writes the folders under `dlssnr-amd` with dots:
  `dlssnr-amd.shaders.runtime/cascade_blur.spv` in the zip and in the cache, `dlssnr-amd\shaders\runtime\...` in the
  game (`Work.MochizukiDestination`). The model does not need it: `dlssnr-amd/dlssnr.bin`. The zip uses the same
  layout as the cache, so extraction finds each entry by its exact path (there are two
  `shader-constants.txt`).
- **On demand.** Both are in `PayloadManifest.OnDemand`; v0.5.x never downloads them (its
  `ComponentsFor` does not know the names) and installs 0.4.0 with daniel and lmxxf.
- **Pins:** `PayloadPins.MochizukiFiles`, only when both components exist (a runtime without the model
  is useless). "Outdated" compares the mochizuki files only in a folder that has them.
- The model is already actually pinned (`2b41c888…`, it is the same in every snapshot since 2026-09-24); the rest
  of 0.4.0 is placeholder.

## 3. Placeholders

A 64-zero pin (`PayloadManifest.PlaceholderSha`) = bytes that do not exist yet. A release with
any placeholder **is not offered** (`PayloadManifest.IsPlaceholder`, in `Offered`), so the
repository's `payload.json` can carry 0.4.0 with the layout written and the app remains installable.
Old apps do not know the rule, which is why:

- `tools/publish-payload.ps1` refuses to publish a manifest with a placeholder (before any upload);
- `tools/check-release.ps1` rejects a build whose `payload.json` (embedded and next to the exe) has one.

The pins come from the files, via script:

- `tools/pin-optiscaler.ps1 -Zip <OptiScaler-X-amd-nr.zip downloaded from the release>`: zip + `extract`
  (everything except `Licenses/`, `README.md`, `Setup*`, `Uninstall*`, `SHA256SUMS.txt` at the root; `D3D12Core`
  goes to `D3D12_OptiScaler/`). It checks the zip's own `SHA256SUMS.txt` and rejects names that v0.5.x
  would refuse to install. Tested: it regenerated exactly the 55 pins of 0.3.0 from the published zip.
- `tools/pin-mochizuki.ps1 -From <final folder> -License <DLSSNR-AMD LICENSE>`: rejects
  `pipeline.cache`, `*.tmp`, logs, deep folders or folders with dots; checks that `prewarm\manifest.txt` was
  made for these shaders (count on the `shaders` line); requires the DLSSNR-AMD MIT license (the zip
  carries its code) and puts it in `dlssnr-amd/LICENSE-DLSSNR-AMD.txt`, outside `shaders\` (prewarm
  checks the hash of the file list of `shaders\`); builds the zip in ordinal order, deterministically
  on any machine; rewrites both components; prints the upload commands.
  A model with different bytes requires a new `-ModelVersion`. Run on `release\candidate\runtime`: 57
  files, prewarm for 54 shaders, `.lib`/`.exp`/`obj` ignored.
- `publish-payload.ps1` now re-uploads a file to the path its `url` already has, instead of
  always to the root by name.

## 4. What changed in the engine

- `Engine.IsAllowed`: `MochizukiNrRuntime.dll` in the list, and any plain file in `dlssnr-amd\`
  up to two levels below (`IsMochizukiData`). The folder name belongs to the runtime.
- **The runtime rewrites `prewarm\manifest.txt`** when the machine's driver is not the one of the shipped
  manifest (cache UUID) or when new pipelines appear. Without handling, every reinstall would give
  "File changed since install" and the uninstall would keep it as "modified". `Engine.IsRuntimeMaintained`
  marks that file: `Transaction.Apply` keeps what the runtime did when the pin has not changed and replaces it
  (without backup) when it has; the uninstall removes it even if changed (`Ours`). It also applies to the entry that
  is not ours: whoever copied mochizuki by hand before (as the OptiScaler 0.4.0 notes teach) has the
  identical copy recorded as `Owned=false`; the runtime rewrites the list and the reinstall must not
  fail because of it. Same pin: it stays as is. New pin: the list becomes ours (the bytes
  recorded were the ones the app pins, there is nothing of anyone's to keep). No old name is
  affected.
- **Unchecking removes.** `Transaction.Apply` gained `retire` (`Transaction.Retire.cs`, `PlanRetire`):
  recorded names that the route no longer wants in the folder are removed in the same transaction, planned before the
  journal and rolled back together. As in uninstall: what the app wrote goes away, what it displaced comes back from
  backup, a file changed afterwards stays (with a warning), a file the game is holding aborts the installation before
  writing. An `Owned=false` entry (identical copy that was already there) only leaves the record; the file belongs to the
  person. The OptiScaler route passes every recorded mochizuki name (`MochizukiRecorded`): unchecked,
  everything goes, with `pipeline.cache`, the `.tmp` files and the empty folders (`AfterMochizukiRetired`); checked,
  only what an old build had and the new one does not goes. Without this the folder would stay "outdated"
  forever: `PayloadMovedOn` compared the files left behind with the new pins and no update
  fixed it. A person's `OptiScaler.ini` that still says `NrBackend=mochizuki` after this produces a
  warning.
- **Uninstall:** `mochizuki_nr.log` was added to `Droppings`; `AfterMochizukiUninstall` deletes
  `dlssnr-amd\pipeline.cache` and the `.tmp` files from interrupted writes only after the runtime has left, and prunes
  the empty folders bottom-up. A foreign file inside `dlssnr-amd` stays.
- **INI:** `PickMochizukiInIni` uses `Engine.SetIni` (the same in-place edit as for `ReShade.ini`) on
  the package's `OptiScaler.ini`, when it is the installation that writes it. An ini that belongs to the person (it was already
  there, or OptiScaler saved settings into it afterwards) is not written by the installation, as before, and the
  report says to choose mochizuki in the Neural tab.
- `Work.Preflight`/`Install` gained `mochizuki: bool` (the ReShade routes ignore it). Without mochizuki
  in the chosen version, asking for it is an error before anything is written.

## 5. Evidence

- 254 green tests (there were 240). The 14 in `tests/AmdNr.Core.Tests/MochizukiTests.cs`: offer and pins
  (only with the model), placeholder hidden, destinations and `IsAllowed`, deep path rejected by
  `Parse`, installation with the single ini line swapped, without mochizuki nothing changes, version without mochizuki
  rejected, person's ini preserved (both cases), rewritten prewarm kept/replaced, uninstall that
  restores the folder byte for byte, runtime held by the game; and, from this round: unchecked against new pins
  (runtime held by the game refuses without writing anything; afterwards everything goes, `PayloadMovedOn` false, final
  folder identical), person's ini that still names mochizuki (warning), hand copy with the list
  rewritten (reinstalls, updates, uninstalls restoring the copy) and hand copy unchecked (the copy
  stays). `TheShippedManifest…` checks the repository's 0.4.0.
- `tools/gate.ps1 -Ui`: build, tests, line limit, render and flows OK. The flow
  (`tools/uishot/Flows.Mochizuki.cs`) opens the card, picks the version that carries mochizuki, checks the box,
  installs, checks files and ini; forgets the choice and sees the box follow the folder; unchecks, installs and
  checks that it was removed and that the folder does not stay outdated; checks and installs again; the uninstall flow
  checks that everything was removed.
- End to end with the **release candidate** (`opti/exports/mochizuki-work/installer-e2e`, read without
  changing `release\candidate`): the `OptiScaler-0.4.0-amd-nr.zip` from `candidate\dist` pinned by
  `pin-optiscaler.ps1` (55 files, sums match), the runtime, the shaders and the prewarm from
  `candidate\runtime` with the model copied from wave 2 (`stage-rc`), pinned by `pin-mochizuki.ps1`
  with the license; files served from disk through the app's "already on this machine" path. Fake folder:
  installation with mochizuki (57 files byte for byte), reinstall with the list rewritten, installation
  unchecked (everything goes, ini goes back to the package's), checked again, uninstall, folder identical to
  before. Second folder with a hand copy: installs, reinstalls with the list rewritten, unchecks, uninstalls,
  and the person's copy stays. Log: `installer-e2e\e2e-run.log`.
- Compatibility: the v0.5.1 engine, taken from the tag (`installer-e2e\compat-v051`), reads the new payload,
  offers 0.4.0, installs and uninstalls the candidate's 0.4.0 without mochizuki. Reading the payload with
  placeholders it also offers 0.4.0: that is why no placeholder can be published.

## 6. Publishing

The complete runbook is in `opti/exports/mochizuki-work/release/INSTALLER_PUBLISH_RUNBOOK.md`. The usual
order still holds: OptiScaler release on GitHub → pins → tests → build → installer release
→ mochizuki files at the pinned addresses → `publish-payload.ps1` → verification by downloading back.

## 7. What was left out

- An `OptiScaler.ini` in which OptiScaler has already saved settings does not receive `NrBackend`; the person
  chooses in the menu (the combo lists the installed runtimes). With mochizuki unchecked, such an ini that
  still names it only produces a warning.
- RDNA4 detection is by known device id or by name. A future RDNA4 card with a different name
  falls into "not RDNA4" only if the id is also unknown.
- Known card that is not RDNA4 with mochizuki installed (card swapped): the checkbox is disabled and the
  next installation removes mochizuki, which does not run on that card.

## 8. Published

On 2026-09-25, in this order:

- **OptiScaler 0.4.0-amd-nr**: release `v0.4.0-amd-nr` of `MatheusFerreiraS/neural-amd-opti`, commit
  `171475e1`. `OptiScaler-0.4.0-amd-nr.zip`: 141,518,665 bytes, sha256
  `9c47e4991ce1fd9924d63834136345616a0b9662460fb9dd9bd022b4827a40a8`, 55 files pinned inside it.
- **mochizuki 0.4.0-amd-nr**: `mochizuki-0.4.0-amd-nr.zip`, 6,263,288 bytes, sha256
  `26170b9b80e0aae1f26d50e558bd07c6561f6ce8daca632ea7b4daf391166f95` (runtime, 54 shaders, prewarm list and
  license). Model 1: `dlssnr.bin`, 147,756,560 bytes, sha256
  `2b41c888cf4155b8958c665ba64018ab0bd25c85fc71a2b6db86d0d04d1f7fbd`. Both downloaded back and checked
  against the pins before the payload pointed to them.
- **Installer v0.6.0**: commits `87a13c3`, `dd43578`, `67324d4`, tag `v0.6.0`. Release with
  `AMD-NR-ReShade-Installer.exe` (sha256 `848f7c8cf494d77594acefe4cf3230f8bcb67e1d6f3077411c2d8840949a41a6`),
  `SHA256SUMS.txt` and `AMD-NR-ReShade-Installer-v0.6.0.7z` (16,113,123 bytes). `check-release.ps1`:
  "Versions agree."
- **payload.json** published by `publish-payload.ps1`, which downloaded back all published files
  ("every published file matches"). Mirror in `zmodelerlover/AMD-NR-Extras`, commit `8ca3d96`.

Checks after publishing:

- The live `payload.json` is byte for byte the repository's, and equal to the mirror's. It offers
  `0.4.0-amd-nr` first.
- End-to-end with the published list and an empty cache, everything downloaded from the pinned addresses: installs with
  mochizuki, reinstalls, installs unchecked, checks again, uninstalls, and the hand-made copy case.
  23 PASS, 0 FAIL ("END-TO-END: every check passed"; the log is in `installer-e2e\e2e-live.log`).
- Before publishing: `gate.ps1 -Ui` with no failures, the end-to-end served from disk and the v0.5.1 engine
  (offers 0.4.0 and installs without mochizuki).
