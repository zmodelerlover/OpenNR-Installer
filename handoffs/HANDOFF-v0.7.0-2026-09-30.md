# Handoff v0.7.0: session result, what's new, library, settings and 12 languages

2026-09-30. Read after [HANDOFF-v0.6.9-2026-09-29.md](HANDOFF-v0.6.9-2026-09-29.md). Alongside this version came
add-on [v0.7.3](https://github.com/zmodelerlover/dlss5-neural-amd/releases/tag/v0.7.3), which fixes the optical flow crash
in D3D11 games.

## Payload

| Component | Before | Now |
|---|---|---|
| `addon` | 0.7.2 (`8b025a26…`) | 0.7.3 (`4b5397c0…`, 860,160 bytes), `addon/0.7.3/amd-nr.addon64` |
| `bridge` | 0.7.2 | stays 0.7.2: the add-on's v0.7.3 release publishes the same bridge bytes, so 32-bit games do not get an empty update |

`api-db.json` was regenerated (2026-09-29) and goes up with it.

## What went into the app

**Game page**
- **Last session** (`SessionLog` in Core, `SessionBox` + `GameSheet.Session.cs` in the app): reads `amd-nr.log`,
  `amd-nr-x86.log`, `dlssnr_on_amd.log`, `mochizuki_nr.log` and `ReShade.log` from the game folder. Ran (frames, network ms,
  runtime version), crash (`CRASH:`/`FAULT:`/device removed, with the log line), did not start (runtime's or
  add-on's reason), ReShade without the add-on, or no frames. Re-read when the window regains focus. lmxxf is not read: I don't know which log
  it writes.
- **What's new** (`ReleaseNotes` in Core, `NotesSheet` in the app): GitHub release notes, cached in
  `cache\notes\`, in a panel styled like the game popup. Opens from the version menu (add-on or OptiScaler, whose repository and
  tag come from the zip URL), from Settings, and once after the app updates itself (`Settings.SeenVersion`). Closes when
  switching tabs.
- **NR settings** (`SettingsTransfer`, `GameSheet.Transfer.cs`): exports/imports a `.amdnr.zip`. ReShade:
  `amd-nr.ini` and `dlssnr_on_amd.ini` in full. OptiScaler: only `[DlssNr]`, `[AmdRtgi]`, `[AmdLook]` and `[FSR-RR]` from
  `OptiScaler.ini`, as `OptiScaler.nr.ini`, applied key by key. Rejects the other route; backup in
  `backups\settings\<game>-<date>\`.
- The left column became `SheetSide.axaml` (line limit); the clicks stay in the `GameSheet` handlers.
- Before today's session, and also in this version: OptiScaler proxy name from the OptiScaler wiki, rename game,
  custom cover and banner, ReShade installed by the app itself on Vulkan and in emulators (layer in HKCU), ⓘ instead of
  paragraphs, verdict as a pill, per-game auto-update, generic rules for a wrongly detected API.

**Library**
- Filter/sort button next to the search: All, Installed, With update, Not installed, Emulators; Name, Recently
  played (the app's Play or the log dates), Recently added (`GameEntry.Added`, empty for old games).
- Steam-style list view with banner, games ignored in Settings, custom title bar.

**Languages**
- 12 languages + pirate: en, pt-BR, es, fr, de, it, ru, tr, zh-CN, ja, ko, ar (RTL via `App.Flow`), x-pirate. There is no fallback
  to English: every key must exist in all of them. CJK font per language (`UiFont` in `App.ChangeLanguage`).
- The language files other than en/pt-BR were generated from Python modules with key and placeholder checks; to change
  a phrase, edit both `.axaml` files by hand and keep the same key in all of them.

## Tests

310 tests, `tools/gate.ps1 -Ui` with no failures. uishot has `langs` (all languages at the smallest size) and the new flow
`SessionFlow` (session, filter, what's-new panel closing on tab switch). Add-on v0.7.3 ran 2,520 frames on a
D3D11 host that used to crash on the first frame; it was not tested in a real game.

## Publishing

1. add-on v0.7.3 (commit `fd37c51`), assets verified by downloading back.
2. Installer release v0.7.0 (exe, `SHA256SUMS.txt`, 7z with the json files), `check-release.ps1`.
3. `publish-payload.ps1 -From <folder with amd-nr.addon64>` (revert the `config.json` rewrite), mirror on AMD-NR-Extras.
