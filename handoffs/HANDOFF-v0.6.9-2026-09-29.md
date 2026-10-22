# Handoff v0.6.9: danielblnc 0.5.0 as the default runtime and OptiScaler 0.4.6

2026-09-29. Read after [HANDOFF-v0.6.8-2026-09-28.md](HANDOFF-v0.6.8-2026-09-28.md). danielblnc released 0.5.0
to everyone ([Alpha 0.5.0](https://github.com/danielblnc/DLSS-NR-on-AMD/releases/tag/v0.5.0)); the public setup ships the
same `version.dll` as the supporter build (`cddfb09e…`).

## What changed in the payload

| Component | Before | Now | Where in the dataset |
|---|---|---|---|
| `runtime` (ReShade route) | patched 0.4.3 (`f3d9f2e5…`) | 0.5.0 patched for the add-on (`c808cdb0…`, 38,703,616 bytes) | `runtime/0.5.0/dlssnr_amd_pass1.dll` |
| release `0.4.6-amd-nr` | did not exist | OptiScaler 0.4.6 (`125543e3…`, from GitHub), `opti-runtime` 0.5.0 (`cddfb09e…`), lmxxf weights, mochizuki and model from 0.4.4 | `opti-runtime/dlssnr_amd_runtime-0.5.0.dll` |
| `user_runtimes` | 0.5.0 and 0.5.1 | only 0.5.1 | — |

The add-on (v0.7.2) and the bridge do not change: the add-on has accepted the patched 0.5.0 since v0.7.0. The weights are the same.

## Code

- `Engine.RuntimeSha` moves to the patched 0.5.0; the patched 0.4.3 goes into `EarlierRuntimeShas`, so an
  old install is still recognized. `Work.RuntimeSize` 38,703,616.
- Tests: the real payload with 0.4.6 and 0.5.0 first, and only 0.5.1 as a supporter build. 291 in total; the test
  with the real 0.5.1 setup passed.

## Publishing

1. opti `v0.4.6-amd-nr` (zip `125543e3…`, downloaded back and verified).
2. `pin-optiscaler.ps1` with the downloaded zip; 0.5.0 extracted from the public setup with `extract_runtime.py` and patched with
   the add-on's `patch_runtime.py` (`c808cdb0…`, its `kSha256`).
3. Release v0.6.9, `publish-payload.ps1`, mirror on AMD-NR-Extras.

None of this was tested in game with 0.5.0 through the installer route.
