# Handoff v0.6.1: danielblnc's runtime 0.4.0 on both routes

Published on 2026-09-26. The only subject is the runtime: DLSS-NR-on-AMD v0.4.0, 42% faster than
0.3.3 according to the author. The weights did not change (`6bf8dc93…`), so the update only downloads a new runtime.

## What changed in the payload

| Component | Before | Now | Where in the dataset |
|---|---|---|---|
| `runtime` (ReShade route) | patched 0.3.0, `70af3fb7…`, 7,290,880 bytes | patched 0.4.0, `ff6feffa…`, 10,027,008 bytes | `runtime/0.4.0/dlssnr_amd_pass1.dll` |
| `addon` | 0.6.5 | 0.6.7, `48eb86e6…` | `addon/0.6.7/amd-nr.addon64` |
| `bridge` | 0.6.6 | 0.6.7: `bcaf1d5a…`, `c83dd632…`, `payload.sha256` `c494ad1a…` | `bridge/0.6.7/…` |
| global `opti-runtime` | 0.3.1 | **stays 0.3.1** | `dlssnr_amd_runtime-0.3.1.dll` |
| release `0.4.1-amd-nr` | did not exist | OptiScaler 0.4.1 (`409d5b00…`) with its **own** `opti-runtime` 0.4.0 (`d62be3d8…`), plus the mochizuki and lmxxf weights pins copied from 0.4.0 | `opti-runtime/dlssnr_amd_runtime-0.4.0.dll` |

- **Why the `opti-runtime` 0.4.0 lives inside the release.** OptiScaler 0.4.0 and earlier
  reject runtime 0.4.0 by SHA, because they do not have the `kAmd040` table. `PayloadManifest.With`
  overlays a release's components on top of the global ones: only those who choose 0.4.1 get runtime 0.4.0.
- **Versioned paths.** The new files were uploaded to paths that no published manifest
  pointed to. The switch happened all at once when the new `payload.json` went up, with no window in which an
  old installer would download new bytes against old pins.
- **Mirrors.** The add-on and the bridge have the assets of release v0.6.7 of `zmodelerlover/dlss5-neural-amd`
  as a mirror, verified by the same hash.

## Code

- `Engine.RuntimeSha` is now the patched runtime 0.4.0 (`ff6feffa…`). It is the pair that add-on 0.6.7 accepts.
- `Work.OptiScaler.KnownRuntimePrefixes` recognizes versions 0.3.3 (`907b30a6…`), 0.4.0 (`d62be3d8…`)
  and patched 0.4.0 (`ff6feffa…`) left in the game folder as `version.dll`.
- Tests: the shipped manifest offers 0.4.1 first, with `opti-runtime` 0.4.0; 0.4.0 stays
  with 0.3.1; 0.4.1's mochizuki is the same as 0.4.0's.

## Publishing

1. The six new files were uploaded to the versioned paths and downloaded back. The hashes
   matched, including on the GitHub mirrors.
2. Release `v0.6.1` with the exe (`7d07e8a8…`), the `SHA256SUMS.txt` and the `AMD-NR-ReShade-Installer-v0.6.1.7z`.
   `check-release.ps1` answered "Versions agree".
3. `publish-payload.ps1` without `-From`, which downloaded back and verified all published files.
4. Mirror on AMD-NR-Extras.

## Tested beforehand

- In-game A/B against runtime 0.3.x: OptiScaler in Cyberpunk 2077 and the 32-bit add-on in GTA IV.
  Both were approved.
- The gate (`tools\gate.ps1 -Ui`) passed with no failures.

## Known limitation

The add-on's version menu reads the GitHub releases and knows nothing about the runtime. Anyone who picks add-on
v0.6.6 or earlier gets the global runtime 0.4.0, which those add-ons reject by size: the effect
stays off, and the panel says "a different build". Use the newest add-on version.
