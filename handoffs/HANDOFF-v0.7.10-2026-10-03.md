# Handoff v0.7.10: RX 6000 with the HIP SDK, Assetto Corsa and add-on v0.7.9

2026-10-03. Read after [HANDOFF-v0.7.9-2026-10-03.md](HANDOFF-v0.7.9-2026-10-03.md).

## RX 6000 (RDNA2) with HIP SDK 7.2

Two players with an RX 6600 installed HIP SDK 7.2; danielblnc's setup worked and ours did not. The SDK installs to
`C:\Program Files\AMD\ROCm\7.2\bin`, sets `HIP_PATH` (and `HIP_PATH_72`) and does not touch `PATH`.

- App: `GpuService.FindHip7` also looks in `HIP_PATH\bin`. Before, the screen said "hip 7: not found" and "ready: False".
- Add-on v0.7.9 (`zmodelerlover/dlss5-neural-amd`, commit `551eea0`): `InitHip` loads `amdhip64_7.dll` from
  `HIP_PATH\bin` when the system does not have one; the runtime then finds the same module by name (checked with a
  test program: `devices=1`, same module). The 32-bit pair uses the same code. OptiScaler already looked in
  `HIP_PATH` (`HipRuntimeLoad.h`).
- No player with an RX 6000 has tested it yet.

## Assetto Corsa

`AssettoCorsa.exe`, at the root, is the 32-bit WPF launcher; the game is `acs.exe`, 64-bit, in the same folder. Detection
picked the launcher. `GraphicsDetector.LauncherOf` swaps a known launcher for the executable it starts, when
that file exists next to it. Test `AssettoCorsaIsTheGameBesideItsLauncher`.

The Witcher 3 (remaster) points to the launcher too; that is left for next time, the game folders still need checking.

## Outside this version

- The runtime's async mode for weaker cards: the add-on only works same-frame (with `Async` it turns
  itself off: "the engine could not run same-frame"), and OptiScaler forces inline. It needs work in the add-on
  and in the fork; another project said async helps little with stutters.
- NR/FG without an upscaler in OptiScaler: in testing (`wt\int`, branch `fi-int`).

## Publishing

1. Add-on `v0.7.9` (six files, `release/upload.txt`).
2. Installer release v0.7.10 (exe, `SHA256SUMS.txt`, 7z with the json files and the LEIA-ME/README), `check-release.ps1`.
3. `publish-payload.ps1 -From <folder with amd-nr.addon64, amd-nr.addon32, amd-nr-host64.exe and payload.sha256 from v0.7.9>`;
   revert the `config.json` rewrite; mirror on AMD-NR-Extras.

`gate.ps1 -Ui`: 0 failures, 330 tests.
