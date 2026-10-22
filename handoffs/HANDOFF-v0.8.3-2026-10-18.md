# Handoff v0.8.3: OptiScaler 0.5.4, mochizuki with the 310.9.16 model, person protection, WrongGPU, 32-bit D3D11/OpenGL and add-on v0.7.13

2026-10-18. Everything ready and committed on `master` (`421ae4e`, `e4809e6`, `774458a` and this handoff). **Nothing has been sent**:
no push, tag, HF upload or release. The publishing order is at the end.

## What went in

### 32-bit routing (OptiScaler 0.5.4)

- `Work.OptiX86Wide` = 0.5.4 (`OptiRunsX86Wide`), alongside `OptiBridges` 0.5.2. `GraphicsDetection.OptiX86WideApis`
  = D3D11, D3D10, D3D9, OpenGL; `PresetFor/OptiApi/CanRunOptiScalerWith/RecommendedFor/NeedsRendererSwitchFor` gained
  `x86Wide`. 32-bit D3D11 or OpenGL game with 0.5.4 chosen (or the newest) -> OptiScaler; 32-bit D3D8 stays on ReShade.
- Proxy: `x86\dxgi.dll` for D3D10/D3D11, `x86\opengl32.dll` for OpenGL, `x86\d3d9.dll` for D3D9 (`OptiX86Proxy`),
  next to the exe; OptiScaler, host and the rest in `OptiScaler64\` as in the 0.5.2 32-bit routes.
- New `OptiScaler.ini` for a 32-bit game: `[DlssNr] Enabled=true` (`NrOn`, now applies to every 32-bit game),
  `PresentWithoutUpscaler=true` and `NrBackend` (mochizuki on RX 9000). Version < 0.5.4 rejects 32-bit D3D11/GL with the
  message that asks for 0.5.4 (`X86Refused`).
- Log `opengl32-x86.log` in the report and in the `OptiScaler64\` cleanup (`dxgi-x86.log` was already there).

### New components in the payload (only inside `releases.optiscaler[0.5.4]`)

| component | version | file(s) | sha256 |
|---|---|---|---|
| `optiscaler` | 0.5.4-amd-nr | `OptiScaler-0.5.4-amd-nr.zip` (GitHub) | `10eeb820…bb950b4` |
| `lmxxf-gfx1200` | 0.5.4-amd-nr | lmxxf 0.43 kernels | `1bd429fc…ad7f7` |
| `optiscaler-x86` | 0.5.4-amd-nr | 4 files, with `opengl32.dll` | `e1fb4200…688da` |
| `lmxxf-weights` | 1 | the same as before | `7f6cdd1c…` |
| `mochizuki` | 0.5.4-amd-nr | 790 files (101 shaders with `shaders\renodx\`, ACO, int4, prewarm of 33 pipelines) | `ba6ccba9…0723f` |
| `mochizuki-model` | 1 | `dlssnr.bin` (already on HF) | `2b41c888…7fbd` |
| `mochizuki-model-small` | 1 | loose `dlssnr-small.bin`, `mochizuki/model-small-1/` | `7e966e26…6e33` |
| `person-model` | 0.5.4-amd-nr | `person-model-0.5.4-amd-nr.zip`, 12 files with the 4 licenses | `5ae1a050…720a` |
| `wronggpu` | 0.5.5-ea | 5 WrongGPU files + `WrongGPU-Setup-0.5.5-ea.exe`, loose | setup `f7b167b5…6363` |
| `wronggpu-dlss` | 310.9.1 | `nvngx_dlss.dll` at NVIDIA's GitHub address (`raw.githubusercontent.com/NVIDIA/DLSS/v310.9.1/...`) | `3975567b…3983` |
| `opti-runtime` | 0.6.0 | placed by hand (the script does not copy it), the same as 0.5.3's | `195c4a89…5721` |

- Add-on and bridge at the top of the payload: `0.7.13` (`addon/0.7.13/...`, `bridge/0.7.13/...`, mirrors in the v0.7.13 release).
  addon64 `bf7e8702…`, addon32 `f8fefa0d…`, host64 `f55a89d0…`, payload.sha256 `781706b5…`.
- The `person-model` zip is **not** the candidate from `opti\exports\release-0.5.4\hf\` (`2bc55970…`): it was rebuilt by the
  new `tools\pin-person-model.ps1` (deterministic) and it is the one the payload pins. Upload the one from
  `publish-person-model-0.5.4-amd-nr\`. The other zips produced the same hashes as the candidates.
- Old installers (v0.8.2) read 0.5.4 and install without the components they do not know (no new model, no
  person protection, no WrongGPU); mochizuki 0.5.4 runs 310.8 in that case.

### mochizuki 0.5.4 on both routes

- `MochizukiComponents` = runtime + model + new model; `Pins().MochizukiFiles` includes `dlssnr-small.bin` when the
  component exists; `WithMochizuki()` (ReShade route) loads the new model from the same release. The ReShade route now
  installs the whole mochizuki 0.5.4 (renodx, ACO, int4, both models) with add-on/bridge 0.7.13, which shows
  "Old model" when both models are there.
- `IsMochizukiData` already accepted `dlssnr-amd\shaders\renodx\` and `dlssnr-small.bin` (up to 6 levels); a new test covers it.
- Uninstall: besides `pipeline.cache`, `aco-cache`, `int4\pipeline.cache` and `int4\pipeline-binaries\`, now also
  `build.pending` and `build.failed`.
- `pin-mochizuki.ps1` accepts `dlssnr-amd\dlssnr-small.bin` and pins it as `mochizuki-model-small` (`-SmallModelVersion`,
  same rule as model 1: same bytes keep the version, new bytes require a new version). No model goes into the zip.

### Person protection

- Component `person-model`, folder `person-model\` next to OptiScaler (64-bit) or inside `OptiScaler64\`
  (32-bit). It is part of `OptiFiles` (goes with OptiScaler), `Engine.OwnedFolders` gained `person-model`.
- It is left out together with lmxxf on a card that is not RDNA4 (`WithoutLmxxf`), and such an installation removes what was there.
- Licenses in the folder itself (`onnxruntime_LICENSE.txt` MIT, `YuNet_MIT.txt`, `PPHumanSeg_Apache-2.0.txt`,
  `YOLO11_AGPL-3.0.txt`) and listed in the README (file table and Credits). `pin-person-model.ps1` rejects a
  folder missing any of them.
- `person-worker.log`/`.1.log` are removed on uninstall and included in the report.

### WrongGPU 0.5.5-ea (cleared by the author for this version)

- `Work.WrongGpu.cs`. **WrongGPU (DLSS 4.5 Model M, RX 9000)** checkbox in the OptiScaler panel (`GameSheet.WrongGpu.cs`),
  off by default, visible only with a version that carries the component, an RX 9000 card and a 64-bit game; choice saved in
  `GameEntry.WrongGpu` (null follows the folder, like mochizuki). New strings in the 21 languages.
- Installation: before the transaction, it runs its setup in a `%TEMP%\amd-nr-wronggpu-<guid>\` with a fake `Game.exe` (`MZ`),
  a package with `OptiScaler.dll` + `OptiScaler.ini` and the exe itself copied there (the staging path exceeds 260
  and CreateProcess failed): `/silent /dir=... /optiscaler=... /experimental`, then `/silent /uninstall /dir=...`,
  and deletes the folder. The model stays in `%LOCALAPPDATA%\WrongGPU\model\model.wgcm`. The DLSS for the temporary folder: the
  downloaded `wronggpu-dlss` (only when `Work.WrongGpuNeedsDlss`: no model on the PC and no `nvngx_dlss.dll` 310.5+ in the
  game) or the game's own. If the setup does not generate the model, nothing is written and the report says why.
- Files in the game: `wronggpu_ffx_dx12.dll`, `wronggpu_runtime.dll`, `plugins\WrongGPU.asi`, `wronggpu-license.txt`,
  `wronggpu-notices.txt`, its own `wronggpu.ini` (`runtime_options=model=M;executor=dx12`; one that already exists and is not
  ours stays) and `wronggpu-amd-compare.dll` = copy of the package's `OptiScaler\amd_fidelityfx_upscaler_dx12.dll` (that is what
  the setup does; we do not host that one). All in `Engine.Allowed`.
- After the transaction, `OptiScaler.ini` (ours or the player's) receives only `[Libraries] FfxDx12SRPath=<absolute
  path of wronggpu_ffx_dx12.dll>` and `[Plugins] LoadAsiPlugins=true`. Unchecked or uninstalled: the files are removed,
  the logs (`wronggpu-asi.log`, `wronggpu-fsr.log`, `wronggpu-diagnostics-*.jsonl`) and the empty `plugins` folder too, and
  the two keys go back to `auto` if they pointed to ours.
- How to turn it off in a future version: remove the `wronggpu` (and `wronggpu-dlss`) component from the release in the payload; the checkbox
  disappears on its own (`CarriesWrongGpu`).
- `tools\pin-wronggpu.ps1 -From <folder where the setup installed> -Setup <exe> -SetupSha ... -Version 0.5.5-ea -Release
  0.5.4-amd-nr -Dlss <nvngx_dlss.dll 310.9.1>`; pinned from Cyberpunk (`E:\Games\Cyberpunk2077\bin\x64`, read
  only) and from the scratchpad setup. Files for upload in `publish-wronggpu-0.5.5-ea\`.

## Verified

- `dotnet test`: 398 passed (new: `Opti054Tests`, `WrongGpuTests`; `OptiScalerVersionTests` and `UserRuntimeTests`
  expect 0.5.4).
- `tools\gate.ps1 -Ui`: build, test, line-limit, publish, ui, flows, 0 failures.
- e2e (`scratchpad\rel054\e2e`, temporary `AMDNR_HOME` seeded from local files and from the real cache read-only):
  OptiScaler with mochizuki on dx11, dx12+DLSS, vulkan, opengl, dx9, dx10, x86-dx9, x86-dx10, **x86-dx11**, **x86-gl**
  install (right proxy, `Enabled=true` on 32-bit, new model, 21 renodx shaders, 12 from person-model) and uninstall
  leaving only the configuration manifest and the ini; x86-dx8 stays on ReShade. **dx12-wronggpu with the real setup**:
  model reused, right files and keys, unchecking removes everything and returns the keys to `auto`, uninstall
  clean; no WrongGPU entry was left in the registry. **ReShade** dx11, dx12, x86-dx11 and x86-dx8 with add-on/bridge
  0.7.13 and mochizuki 0.5.4 (two models, renodx, ACO, int4) install and uninstall cleanly.
- Tested by hand before (2026-10-18): the setup accepts a 2-byte `Game.exe` and a package with only dll + ini; with no model on the PC
  it converts from the `nvngx_dlss.dll` placed in the folder ("this game"). The `nvngx_dlss.dll` from NVIDIA's GitHub is identical to
  Witcher 3's 310.9.1.
- `check-release.ps1` now fails only on what depends on publishing (the newest release is still v0.8.2); it has to
  pass fully after step 5.

## Artifacts

- Installer: `opti\exports\release-0.5.4\installer\` (build of `6df14c0`): `AMD-NR-ReShade-Installer.exe`
  `9598d6fd7e55c75c95e7f7b65e3d60998f5371e44de73f6d22c267c36db8bd9e` (23710832 bytes), `SHA256SUMS.txt`,
  `AMD-NR-ReShade-Installer-v0.8.3.7z` (exe + `config.json`, `payload.json`, `api-db.json`). Release notes:
  `opti\exports\release-0.5.4\notes-installer-0.8.3.md`. Discord changelog: `nr-bot\changelogs\2026-10-18_v0.8.3.md`.
- For HF: `publish-lmxxf-gfx1200-0.5.4-amd-nr\`, `publish-optiscaler-x86-0.5.4-amd-nr\`,
  `publish-mochizuki-0.5.4-amd-nr\`, `publish-person-model-0.5.4-amd-nr\`, `publish-wronggpu-0.5.5-ea\` (at the root of the
  installer, ignored by git) and `opti\exports\release-0.5.4\hf\dlssnr-small.bin`.

## Publishing order (not executed)

Each step depends on the previous one. Run from PowerShell.

1. **opti**: `opti\exports\release-0.5.4\PUBLICAR.md` step 1 (push of `dlss-neural-rendering`, tag
   `v0.5.4-amd-nr` at `6ef8eb7fd`, `gh release create` with the zip and `notes-opti-0.5.4.md`). Download the zip back and
   check `10eeb820beb43942659a4477f7d37988806660b3b540526e0e3c70c34bb950b4`.
2. **add-on v0.7.13**: `reshade\release\v0.7.13\PUBLICAR.md` steps 3 and 4 (push, tag, release with the 6 files).
3. **Hugging Face** (`zmodelerlover/amd-nr`, `--repo-type dataset`), from inside `AMD-NR-ReShade-Installer`:
   - `hf upload zmodelerlover/amd-nr publish-lmxxf-gfx1200-0.5.4-amd-nr\lmxxf-gfx1200-0.5.4-amd-nr.zip lmxxf-gfx1200/0.5.4-amd-nr/lmxxf-gfx1200-0.5.4-amd-nr.zip --repo-type dataset --commit-message "lmxxf RX 9060 kernels 0.5.4-amd-nr"`
   - `hf upload zmodelerlover/amd-nr publish-optiscaler-x86-0.5.4-amd-nr\optiscaler-x86-0.5.4-amd-nr.zip optiscaler-x86/0.5.4-amd-nr/optiscaler-x86-0.5.4-amd-nr.zip --repo-type dataset --commit-message "OptiScaler 32-bit proxies and host 0.5.4-amd-nr"`
   - `hf upload zmodelerlover/amd-nr publish-mochizuki-0.5.4-amd-nr\mochizuki-0.5.4-amd-nr.zip mochizuki/0.5.4-amd-nr/mochizuki-0.5.4-amd-nr.zip --repo-type dataset --commit-message "mochizuki 0.5.4-amd-nr: runtime, shaders, prewarm list and licence"`
   - `hf upload zmodelerlover/amd-nr ..\opti\exports\release-0.5.4\hf\dlssnr-small.bin mochizuki/model-small-1/dlssnr-small.bin --repo-type dataset --commit-message "mochizuki model-small 1 (310.9.16)"`
   - `hf upload zmodelerlover/amd-nr publish-person-model-0.5.4-amd-nr\person-model-0.5.4-amd-nr.zip person-model/0.5.4-amd-nr/person-model-0.5.4-amd-nr.zip --repo-type dataset --commit-message "person protection 0.5.4-amd-nr: worker, ONNX Runtime, models and licences"`
   - WrongGPU, the six from `publish-wronggpu-0.5.5-ea\` into `wronggpu/0.5.5-ea/<name>`: `wronggpu_ffx_dx12.dll`
     (`3511adb5…`), `wronggpu_runtime.dll` (`331bc00e…`), `WrongGPU.asi` (`e9aab52c…`), `wronggpu-license.txt`
     (`7a215435…`), `wronggpu-notices.txt` (`86671867…`), `WrongGPU-Setup-0.5.5-ea.exe` (`f7b167b5…`). Example:
     `hf upload zmodelerlover/amd-nr publish-wronggpu-0.5.5-ea\wronggpu_ffx_dx12.dll wronggpu/0.5.5-ea/wronggpu_ffx_dx12.dll --repo-type dataset --commit-message "WrongGPU 0.5.5-ea: wronggpu_ffx_dx12.dll"`
     (the six exact commands are in the output of `pin-wronggpu.ps1`; `SHA256SUMS.txt` in the same folder).
   - `nvngx_dlss.dll` is **not** uploaded: it stays at NVIDIA's address. `dlssnr.bin` (model 1) is already there.
   - Check each one back by hash (step 5 does this for all of them).
4. **Installer on GitHub**: `git push origin master`; `gh release create v0.8.3 installer\AMD-NR-ReShade-Installer.exe installer\SHA256SUMS.txt installer\AMD-NR-ReShade-Installer-v0.8.3.7z --repo zmodelerlover/AMD-NR-ReShade-Installer --title "v0.8.3" --notes-file notes-installer-0.8.3.md`
   (from inside `opti\exports\release-0.5.4`); `pwsh tools\check-release.ps1 -Exe publish-v0.8.3\AMD-NR-ReShade-Installer.exe` in full.
5. **Payload**: `.\tools\publish-payload.ps1 -Repo zmodelerlover/amd-nr -From E:\Projetos\opti_rr_dlss5\reshade\release\v0.7.13 -SetVersion addon=0.7.13 -SetVersion bridge=0.7.13`
   (uploads add-on/bridge to `addon/0.7.13/` and `bridge/0.7.13/`, the payload and the api-db, and downloads everything back). Revert the
   rewrite of `config.json`; commit "Publish the payload: OptiScaler 0.5.4-amd-nr, mochizuki 0.5.4, WrongGPU
   0.5.5-ea and add-on v0.7.13". If `payload.json` changed, rebuild the `.7z` with it and `gh release upload v0.8.3 <7z>
   --clobber` (the updater does not pin the 7z).
6. **AMD-NR-Extras**: copy `payload\payload.json` (and `api-db.json` if it changed), commit and push; check that it is equal to the one on HF.
7. **Discord**: `python post_changelog.py changelogs\2026-10-18_v0.8.3.md --preview` (#admin), look it over, delete the preview
   and post; along with the opti 0.5.4 changelog (`notes-opti-0.5.4.md`) and the add-on v0.7.13 one. Threads from the list in
   opti's `PUBLICAR.md` (KCD2, FH6, the ones from 2026-10-18), mentioning whoever reported.

## Open

- Add-on v0.7.13 was tested with the `mochizuki-runtime-011` export; the ReShade route now ships the runtime from release
  0.5.4 (`a3a35268…`). Same ABI (the e2e only installs, it does not run): the in-game test from the add-on checklist applies.
- WrongGPU in game via the installer not yet tested (only in Cyberpunk installed by hand). The setup writes its own log to
  `%LOCALAPPDATA%\WrongGPU\setup.log` and reuses the model that already exists; with an old model without "output filter"
  it looks for a DLSS again: if it does not find one, the installation fails with the setup's message (the NVIDIA download only
  happens when there is no model at all).
- Person protection in a 32-bit game goes to `OptiScaler64\` as opti's `PUBLICAR.md` asks, but it runs before the
  upscaler and a 32-bit game has no upscaler: that is 22 MB that probably serve no purpose there.
- `Enabled=true` now applies to every new `OptiScaler.ini` for a 32-bit game, D3D9/D3D10 included.
