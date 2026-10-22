# Handoff v0.7.3: seven languages, RDR1 through OptiScaler, OptiScaler logs in the report and add-on v0.7.4

2026-09-30. Read after [HANDOFF-v0.7.2-2026-09-30.md](HANDOFF-v0.7.2-2026-09-30.md).

## Red Dead Redemption through OptiScaler

`PlayRDR.exe` (the Rockstar Games Launcher redirector) lives in the game folder, so it loads the proxy DLL before
`RDR.exe`, under any name. OptiScaler installed itself into it and broke the Rockstar SDK (error 25D11006, "not signed
in"). With `[ProcessFilter] TargetProcessName=rdr.exe` OptiScaler stays in pass-through in every process that is not the
game (the fork's `dllmain.cpp`, before opening the log). The player confirmed the game started working with this; the
Social Club overlay stays off, and that is left for another day.

`Work.OptiScaler.cs`: `Launchers` lists launchers that sit next to the game (today only `PlayRDR.exe` -> `rdr.exe`). When
both are in the folder, the package's `OptiScaler.ini` goes in with `TargetProcessName`; a user's ini is not touched, and the
install log says what to put in it. The ini is configuration, so it does not count toward "update available".
Test: `RedDeadRedemptionsLauncherBesideTheGameLeavesOptiScalerPassiveInIt`.

## Half-deleted install

`Scanning.Presence`: the manifest is read before `IsInstalled`, so a folder with the manifest and part of the files
(a runtime deleted by hand, for example) still counts as installed, as `Transaction.Apply` already treated it. Before,
it showed up as not installed and the next install rejected the manifest that was there. Test:
`AnInstallMissingItsRuntimeIsStillOneWhileItsOtherFilesAreThere`.

## Report

`SupportReport.RuntimeEvidence` includes `OptiScaler.log`, `OptiScaler.ini`, `amd_bridge.log` and `amd_presr.log`. Reports from the
OptiScaler route came without any runtime log (RDR1, Uncharted).

## Languages

Polish, Romanian, Hungarian, Croatian, Lithuanian, Ukrainian and Hindi, 415 keys each, generated like the others
(`gen.py lang_xx`). Counts in the three-plural languages use the "Label: {0}" format; the game name goes in with the word
"game" declined next to it, or after a colon in Hungarian. Hindi uses Nirmala UI (`UiFont`). `uishot langs` passes
on all of them; at 980 px the library header gets tight in Hungarian and Lithuanian as it already did in German and Spanish.

## Add-on v0.7.4

Commit `3e8688c`: the add-on moves to the swapchain a game creates after a refused resize (Where Winds Meet: "no
frames yet" and crash on reopening), and the log states the HRESULT and the removal reason when the D3D11 depth view
fails. Reproduced and verified on a D3D11 host that does the same resize; the player has not confirmed yet.

## Publishing

1. Add-on v0.7.4, assets from `release/upload.txt`.
2. `publish-payload.ps1 -From <folder with amd-nr.addon64> -SetVersion addon=0.7.4`. **Before that, change the addon URL in
   `payload.json` to `addon/<new version>/`**: the script uploads to the path the URL already has. This time it uploaded 0.7.4
   to `addon/0.7.3/`; 0.7.3 was put back in its own path (downloaded from the GitHub release) and 0.7.4 republished at
   `addon/0.7.4/`. Revert the `config.json` rewrite.
3. Installer release v0.7.3 (exe, `SHA256SUMS.txt`, 7z with the json files), `check-release.ps1`; mirror on AMD-NR-Extras.

## Open

- One Piece Odyssey: mochizuki crashes while building the network at 4K on the player's driver 32.0.32015; here (32.0.31041, same
  runtime, same settings) it builds in 15 s and runs. danielblnc works there.
- Uncharted: Legacy of Thieves: no symptom or `OptiScaler.log` yet. `u4.exe` opens and closes before `tll.exe`,
  the same pattern as `PlayRDR.exe`.
