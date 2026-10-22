# Handoff v0.6.3: danielblnc's runtime 0.4.1, and the mochizuki box that did not show up

2026-09-26. Read after [HANDOFF-v0.6.2-2026-09-26.md](HANDOFF-v0.6.2-2026-09-26.md). Two
subjects: Daniel's new runtime on both routes, and the fix for v0.6.2, which never showed the
mochizuki box on the ReShade route.

## mochizuki on the ReShade route (v0.6.2 bug)

Two causes, both fixed:

1. **The box was inside `OptiPanel`** in `GameSheet.axaml`. On the ReShade route that panel is
   hidden, so `MochizukiSection.IsVisible = true` showed nothing. The section now sits
   below both route panels and serves both.
2. **`AddonReleases.With` dropped the `Releases`.** The sheet picks the add-on from the GitHub list, and
   v0.6.8 is there, so the choice always went through `With`, which rebuilt the manifest without
   `Releases`. Then `WithMochizuki()` could not find the build (it lives in the OptiScaler releases). Now
   `With` keeps `Releases`.

Tests: `ReShadeMochizukiTests.AnAddOnPickedFromItsReleasesStillCarriesTheMochizukiBuild` (fails without
fix 2) and the flow `tools/uishot/Flows.Mochizuki.cs`, which now switches to the ReShade route and requires
`IsEffectivelyVisible` (fails without fix 1: `IsVisible` alone proves nothing, which is what
let the bug through). The user confirmed on a test build that the box shows up and installs.

## What changed in the payload

| Component | Before | Now | Where in the dataset |
|---|---|---|---|
| `runtime` (ReShade route) | patched 0.4.0, `ff6feffa…` | patched 0.4.1, `c8808716…`, 9,916,928 bytes | `runtime/0.4.1/dlssnr_amd_pass1.dll` |
| `addon` | 0.6.8 | 0.6.9, `f5007509…` | `addon/0.6.9/amd-nr.addon64` |
| `bridge` | 0.6.8 | 0.6.9: `633f8a30…`, `eb0577c4…`, `payload.sha256` `8e6f3799…` | `bridge/0.6.9/…` |
| release `0.4.2-amd-nr` | did not exist | OptiScaler 0.4.2 (`ea8aff0a…`) with its **own** `opti-runtime` 0.4.1 (`823063eb…`), mochizuki and lmxxf weights the same as 0.4.1's | `opti-runtime/dlssnr_amd_runtime-0.4.1.dll` |

- The `opti-runtime` 0.4.1 lives inside the release for the same reason as 0.4.0 in v0.6.1: earlier
  OptiScalers reject 0.4.1 by SHA. 0.4.1-amd-nr stays on 0.4.0.
- Add-on 0.6.9 rejects runtime 0.4.0 and 0.6.8 rejects 0.4.1, so add-on and runtime switch
  together, when the new `payload.json` goes up. Same limitation as v0.6.1: anyone who picks an old
  add-on in the menu gets the new runtime, and the panel says which runtime that add-on needs.
- Bridge 0.6.9 uses protocol v4 (the status carries the runtime line). `amd-nr.addon32` and
  `amd-nr-host64.exe` always come from the same release, as before.

## Code

- `Engine.RuntimeSha` is the patched 0.4.1 (`c8808716…`).
- `Work.OptiScaler.KnownRuntimePrefixes` recognizes 0.4.1 (`823063eb…`) and the patched 0.4.1.
- `OptiScalerVersionTests`: 0.4.2 comes first with `opti-runtime` 0.4.1; 0.4.1 stays with 0.4.0.

## Publishing

1. opti `v0.4.2-amd-nr` (zip `ea8aff0a…`), add-on `v0.6.9` (six assets downloaded back and verified).
2. `tools/pin-optiscaler.ps1` with the zip downloaded from the release; mochizuki copied from 0.4.1.
3. The six new files on HF in versioned paths, downloaded back and verified, the GitHub
   mirrors too.
4. End to end with this version's engine, empty cache, everything downloaded from the `payload.json` addresses:
   64-bit route (runtime `c8808716…` in the game) with and without mochizuki, 32-bit bridge with and without mochizuki,
   OptiScaler 0.4.2 with mochizuki (the three pass files with `823063eb…`) and the uninstalls. Passed.
5. `gate.ps1 -Ui` with no failures.
6. Installer release v0.6.3, `publish-payload.ps1`, mirror on AMD-NR-Extras.

## Tested beforehand

- OptiScaler 0.4.2 with runtime 0.4.1 in Cyberpunk 2077; add-on 0.6.9 in ETS2 (64-bit) and in GTA IV
  (32-bit bridge), with the "danielblnc 0.4.1: rede X ms" line checked against the runtime log.
- The mochizuki box on the ReShade route, by the user, on a test build of this version.
