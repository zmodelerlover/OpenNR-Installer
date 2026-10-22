# Handoff v0.6.5: OptiScaler 0.4.4 and the author's version.dll as the runtime

2026-09-27. Read after [HANDOFF-runtime-0.4.2-2026-09-27.md](HANDOFF-runtime-0.4.2-2026-09-27.md). The
OptiScaler route got a new version and the app learned to reuse the runtime that danielblnc's setup leaves in the
game folder. The add-on stays v0.6.9 and the ReShade route's runtime stays the patched 0.4.1.

## What changed in the payload

| Component | Before | Now | Where in the dataset |
|---|---|---|---|
| release `0.4.4-amd-nr` | did not exist | OptiScaler 0.4.4 (`5671297e…`), `opti-runtime` 0.4.2 (`8aa2dcc5…`, the same as 0.4.3's), lmxxf weights from 0.4.3 | nothing new besides mochizuki |
| `mochizuki` of 0.4.4 | did not exist | `MochizukiNrRuntime.dll` `7e1f721e…`, archive `2e628e4d…` | `mochizuki/0.4.4-amd-nr/mochizuki-0.4.4-amd-nr.zip` |

- The mochizuki shaders changed: 58 files, four new (`g_ffwd3w`, `g_fswinpds128`, `g_fswinpup128`,
  `runtime/runtime_encode_in`). The prewarm manifest was redone for them in a cold run at 1080p and
  then at 1440p, because `ffwd3w` only shows up from 1440p upward: 37 lines. With it the network was ready in
  6.0 s on a fresh exe, versus 23.0 s without.
- The mochizuki build came from opti's `v0.4.4-amd-nr` tag, and its output is byte-for-byte identical to the new
  goldens (`golden-2026-09-27` in opti). The model stays version 1, already published.
- The ReShade route takes mochizuki from the newest release (`WithMochizuki`), so it also gets the faster one.
  The ABI did not change.

## What OptiScaler 0.4.4 brings

- Faster mochizuki: -0.48 to -0.58 ms at 1080p and -1.03 to -1.10 ms at 1440p on the game's queue (RX 9070 XT).
  The pre-block input is rounded to nearest-even as in the original network, the only change in the image.
- The package's `Setup.bat` writes `auto` to `AmdEncoding` and `AmdEveryFrame`, instead of forcing Linear with
  history off. The app never ran `Setup.bat`, so its installs do not change.
- Stabilizer after the runtime (danielblnc and mochizuki) and effect-only smoothing in lmxxf, off by
  default.
- Accepts the 0.5.0 supporter build (`cddfb09e…`, 38.7 MB) and recommends the public 0.4.2.

## Code

- **The author's `version.dll` becomes the runtime.** Before, a danielblnc `version.dll` in the game folder (his
  setup loads the runtime that way) blocked the OptiScaler route, and the user had to remove the file and run
  again. Now, when the chosen OptiScaler runs that build, it becomes the install's runtime
  (`dlssnr_amd_pass1-3.dll`) instead of the download, and the `version.dll` goes to the backup; uninstall gives
  it back byte for byte. A build the chosen OptiScaler does not run (0.5.0 on 0.4.3, 0.2.17) still
  blocks, with the message saying so.
- **Updates:** without `version.dll`, the pass 1 already in the folder stays when it is a build the OptiScaler
  runs and newer than the payload's (0.4.3 or 0.5.0 versus 0.4.2), and the folder does not show up as
  outdated because of it. One older than the payload's is replaced on the next update.
- `Work.OptiScaler`: `AcceptedRuntimes` table (SHA, runtime version, first OptiScaler version whose
  `AmdLayout.h` accepts it: 0.3.0 and 0.3.1 since 0.1.0, 0.4.0 since 0.4.1, 0.4.1 since 0.4.2, 0.4.2 and 0.4.3 since
  0.4.3, 0.5.0 since 0.4.4), `OwnRuntime`, `OwnRuntimeIsCurrent`; `CheckRuntimeAsVersionDll` informs instead of
  blocking when the build fits, and looks at files from 7 to 40 MB. A new runtime accepted by a new opti is one
  line in that table.
- `Transaction.Apply`: `displace` parameter, someone else's file moved out of the folder with a backup. The manifest
  entry owns the name with an empty content hash, and uninstall restores the backup. If the user puts
  another `version.dll` in and installs again, that one becomes the original and the previous backup is discarded.
- `Engine.Allowed` gained `version.dll` (no route writes that name; it is only moved and given back).
- `PayloadPins.OptiScalerVersion` / `OptiRuntimeVersion`, read from the payload.
- The app still downloads the payload's runtime when it uses the game's (the download is planned before looking at the
  folder); it just does not install it.
- A manifest with `version.dll` is not read by earlier app builds (the name was not on the list), which
  treat it as a folder without a manifest of its own. The auto-updater makes this unlikely.

## Tests

- `ADisplacedFileGoesToTheBackupAndUninstallPutsItBack`, `AnAuthorRuntimeIsRunOnlyByAnOptiScalerThatAcceptsIt`
  and the test of the real `payload.json` with 0.4.4 first: 263 in total.
- Local and temporary, outside the repository: the real 0.4.1, 0.4.3 and 0.5.0 `version.dll` files with a fake
  payload (install, update, outdated, uninstall).

## Publishing

1. opti `v0.4.4-amd-nr` (zip `5671297e…`, downloaded back and verified).
2. `tools/pin-optiscaler.ps1` with the zip downloaded from the release; `opti-runtime` 0.4.2 copied from 0.4.3.
3. `tools/pin-mochizuki.ps1 -Version 0.4.4-amd-nr` with the tag's build; the archive went up to HF and was
   downloaded back (`2e628e4d…`).
4. End to end with empty cache, everything downloaded from the `payload.json` addresses (temporary test
   `ZzLiveE2E`): OptiScaler 0.4.3 installed and flagged as outdated, update to 0.4.4 with
   mochizuki (the three pass files `8aa2dcc5…`, mochizuki `7e1f721e…`, prewarm manifest), uninstall; and 0.4.4
   on top of the author's 0.4.3 and 0.5.0 `version.dll`, which became the pass files, did not flag the folder as
   outdated and came back on uninstall. Passed.
5. `gate.ps1 -Ui` with no failures.
6. Installer release v0.6.5, `publish-payload.ps1`, mirror on AMD-NR-Extras.

## Tested beforehand

- Nothing in this release was tested in game. The mochizuki and stabilizer measurements come from a harness without
  a game (details in opti's `handoff/README.md`, section 0.4.4).
