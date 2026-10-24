# Handoff v0.6.8: OptiScaler 0.4.5, add-on v0.7.2 and the 0.5.1 supporter build

2026-09-28. Read after [HANDOFF-v0.6.6-2026-09-27.md](HANDOFF-v0.6.6-2026-09-27.md) and the v0.6.7 commits.

## What changed in the payload

| Component | Before | Now |
|---|---|---|
| release `0.4.5-amd-nr` | did not exist | OptiScaler 0.4.5 (`16300032…`, from GitHub), `opti-runtime` 0.4.3, lmxxf weights, mochizuki and model from 0.4.4 (the runtime did not change) |
| `addon` / `bridge` | 0.7.1 | 0.7.2 (`8b025a26…`, `ac0ac755…`, `b8dc7246…`), only 0.5.1 added |
| `user_runtimes` | 0.5.0 | 0.5.0 and 0.5.1 (`493b4a3b…`, patched `af67f066…`, `addon_since` 0.7.2) |

0.5.1 is a danielblnc supporter build: **it is not distributed**, neither here nor on HF. The payload only says how
to recognize and patch the file the user already has.

## Code

- `Work.Runtimes`: `KnownRuntimePrefixes` and `AcceptedRuntimes` with 0.5.1, run by OptiScaler from 0.4.5 on.
- Tests: the real payload with 0.4.5 first and both supporter builds; the test with the real setup
  (`AMDNR_TEST_RUNTIME_SETUP`) uses the first OptiScaler that runs the build and passed with the 0.5.0 setup and the
  0.5.1 one.

## The rest of the release

- opti `v0.4.5-amd-nr`: the fix for ReShade as `dxgi.dll` (mochizuki and lmxxf stayed off) and `kAmd051`.
- add-on `v0.7.2`: 0.5.1 in `runtime_offsets.h` and in `runtime-patches.json`. Built on this machine with the
  FidelityFX SDK v2.3.0 from GitHub and a portable GCC 16 for the gates (the MinGW 6.3 on PATH does not compile C++20).
  `framecheck` ran 20 frames with the patched 0.5.1 without a game.
- None of this was tested in game with 0.5.1.
