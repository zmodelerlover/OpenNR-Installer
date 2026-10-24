# Handoff v0.6.4: OptiScaler 0.4.3 and the fixed mochizuki

2026-09-27. Read after [HANDOFF-v0.6.3-2026-09-26.md](HANDOFF-v0.6.3-2026-09-26.md). Only the
OptiScaler route got a new version; the add-on stays v0.6.9 and the ReShade route's runtime stays the patched
0.4.1.

## What changed in the payload

| Component | Before | Now | Where in the dataset |
|---|---|---|---|
| release `0.4.3-amd-nr` | did not exist | OptiScaler 0.4.3 (`e31a2637…`), `opti-runtime` 0.4.1 (`823063eb…`, the same as 0.4.2's), lmxxf weights from 0.4.2 | nothing new besides mochizuki |
| `mochizuki` of 0.4.3 | the 0.4.0 build (`682f2087…`) | `MochizukiNrRuntime.dll` `4c3b0b3a…`, archive `f57b9d00…` | `mochizuki/0.4.3-amd-nr/mochizuki-0.4.3-amd-nr.zip` |

- The mochizuki shaders are the same 54 files as 0.4.0's, checked one by one, so the 0.4.0 prewarm
  manifest still applies. The model is version 1, already published.
- The ReShade route takes mochizuki from the newest release (`WithMochizuki`), so it also gets the
  fix without changing the add-on.
- The default runtime stays 0.4.1. The 0.4.2 and 0.4.3 builds danielblnc gives to supporters are not
  in the payload or the dataset, and must not go in: OptiScaler 0.4.3 accepts them when the user brings them.

## What OptiScaler 0.4.3 brings

- Accepts the 0.4.2 and 0.4.3 supporter builds. Just swap `dlssnr_amd_pass1.dll`: the host copies pass 1
  to pass 2 and 3 when they are missing or belong to another runtime.
- "DLSS 5 mode" (`AmdQuality`, 0.4.2 or newer) and "Tone curve" (`AmdToneCurve`, 0.4.0 or newer).
- mochizuki no longer turns off below 1080p: the driver refused the shared handle of the small
  buffers (up to 4.2 MB); now none is below 16 MB, and a refusal is retried at 1, 2, 5, 10,
  30 and 60 s.

## Code

- `Work.OptiScaler.KnownRuntimePrefixes` recognizes 0.4.2 (`8aa2dcc5…`) and 0.4.3 (`d1e32086…`), and the
  warning about danielblnc's `version.dll` in the game folder now looks at files from 7 to 14 MB. Before, only 7 to
  8 MB, so no 0.4.x runtime (9.9 MB and 12.8 MB) triggered the warning.
- `OptiScalerVersionTests`: 0.4.3 comes first with `opti-runtime` 0.4.1; 0.4.2 also has 0.4.1; the
  mochizuki of 0.4.3 differs from 0.4.0 through 0.4.2, which remain identical to each other.

## Publishing

1. opti `v0.4.3-amd-nr` (zip `e31a2637…`, downloaded back and verified).
2. `tools/pin-optiscaler.ps1` with the zip downloaded from the release; `opti-runtime` copied from 0.4.2.
3. `tools/pin-mochizuki.ps1 -Version 0.4.3-amd-nr` with the build from opti's commit `e3048774`; the archive
   went up to HF and was downloaded back.
4. End to end with empty cache, everything downloaded from the `payload.json` addresses: 64-bit route and 32-bit bridge
   with and without mochizuki, OptiScaler 0.4.3 with mochizuki (the three pass files with `823063eb…`, mochizuki
   `4c3b0b3a…`) and the uninstalls. Passed.
5. `gate.ps1 -Ui` with no failures.
6. Installer release v0.6.4, `publish-payload.ps1`, mirror on AMD-NR-Extras.

## Tested beforehand

- In Cyberpunk 2077: runtime 0.4.3, DLSS 5 mode, Tone curve and the mochizuki fix (going down from
  1080p to 640x360 in FSR without losing the effect).
- The copy of pass 1 to pass 2 and 3 was not tested in game before the release.
