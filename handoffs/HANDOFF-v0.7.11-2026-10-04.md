# Handoff v0.7.11: danielblnc version per game, RX 9000 policy, OptiScaler 0.4.10, mochizuki 0.4.10 and add-on v0.7.10

2026-10-04. Read after [HANDOFF-v0.7.10-2026-10-03.md](HANDOFF-v0.7.10-2026-10-03.md). The details of everything in this
round (add-on, fork and the mochizuki investigation on driver 32015) are in
`neural-amd-opti/handoff/HANDOFF-fixes-2026-10-03.md`.

## danielblnc runtime version per game

- `payload.json` gained `releases.runtime`: 0.6.0, 0.5.1, 0.5.0 and 0.4.3, each with the `runtime` (patched pass1 +
  weights, for the add-on) and the `opti-runtime` (raw, for OptiScaler). The files were already on HF.
- `PayloadManifest.WithRuntime(version, async)`, `RuntimeVersions()`, `RuntimeChoice` (survives an add-on/OptiScaler
  version `With()` applied afterwards), `PayloadPins.RuntimeChosen` / `RuntimeAsync`.
- `Work.RuntimeRunsOn` only offers what the chosen add-on/OptiScaler runs.
- Sheet: "danielblnc version" row (`DanielVersionRow`, `RuntimeOption.cs`), saved in `GameStore.DanielRuntime`.
- Policy (`Work.Runtimes.cs`): RX 9000 sees all of them, **0.4.3 default** with a green Recommended badge, the others with an
  orange Unstable badge and installed in async (`Work.GoesInAsync`: `Inline=0` in `amd-nr.ini`, `AmdAsync=true` in
  `OptiScaler.ini`, only that key). RX 6000/7000: only 0.6.0, no async.

## RX 9000: mochizuki in the add-on, lmxxf in OptiScaler

No experimental checkbox: on RX 9000 mochizuki always goes in (`WantsMochizuki`). Add-on v0.7.10 runs mochizuki
when there is no `NrBackend`; a new/our `OptiScaler.ini` gets `NrBackend=lmxxf`. It was only enabled now because
mochizuki 0.4.10 builds the network again on driver 32.0.32015 (0.4.9 closed the game).

## Other

- A locked file names the process (`Engine.Holders.cs`, Restart Manager), on install, pre-check and uninstall.
- Warning about another mod's `d3d11.dll` on the D3D10/11 routes (`Work.CheckForeignD3d11`).
- OptiScaler `0.4.10-amd-nr` (`pin-optiscaler.ps1`). The script does not copy the `opti-runtime`: it was put in by hand, 0.4.9's
  (0.6.0, `195c4a89…`). Without it the release would fall back to the top-level 0.3.1. `lmxxf-gfx1200` 0.4.10 (same kernels, new zip).
- mochizuki `0.4.10-amd-nr` (`pin-mochizuki.ps1`, 68 files): shaders fixed for driver 32015, prewarm done in
  Cyberpunk with these shaders. `mochizuki-model` stays 1.
- Add-on and bridge v0.7.10 (protocol v6).
- Tests: `RuntimeChoiceTests`, `HeldFilesTests`; `OptiScalerVersionTests` and `UserRuntimeTests` expect 0.4.10.

## Verified

`gate.ps1 -Ui`: build, tests, line-limit, ui and flows, 0 failures.

## Publishing (done in this order)

1. opti `v0.4.10-amd-nr` (zip) and add-on `v0.7.10` (six files).
2. `hf upload` of `mochizuki-0.4.10-amd-nr.zip` and `lmxxf-gfx1200-0.4.10-amd-nr.zip`.
3. Installer release v0.7.11 (exe, `SHA256SUMS.txt`, 7z with the json files and the LEIA-ME/README), `check-release.ps1`.
4. `publish-payload.ps1 -From <folder with the v0.7.10 files>`; revert the `config.json` rewrite; mirror on
   AMD-NR-Extras.
