# Handoff v0.7.6: public danielblnc 0.5.1 as the default and the 0.6.0 supporter build (RDNA2)

2026-10-01. Read after [HANDOFF-v0.7.5-2026-10-01.md](HANDOFF-v0.7.5-2026-10-01.md).

## Public 0.5.1 becomes the runtime of both routes

danielblnc published [Alpha 0.5.1](https://github.com/danielblnc/DLSS-NR-on-AMD/releases/tag/v0.5.1) on 2026-10-01. The public
setup is byte for byte the supporter one (`b00818c7…`), with the same `version.dll` `493b4a3b…` that the add-on (v0.7.2+) and
OptiScaler (0.4.5-amd-nr+) already ran.

- ReShade route: `runtime` 0.5.1, patched `dlssnr_amd_pass1.dll` `af67f066…` (38,569,472 bytes), at `runtime/0.5.1/` on HF.
  `Engine.RuntimeSha` points to it and the patched 0.5.0 (`c808cdb0…`) went to `EarlierRuntimeShas`.
- OptiScaler route: release `0.4.7-amd-nr` with `opti-runtime` 0.5.1 (`dlssnr_amd_runtime-0.5.1.dll`, the raw `version.dll`).

## 0.6.0 supporter build

Not distributed. Adds RX 6000 (RDNA2), which needs AMD's HIP SDK 7.2 runtime (their driver only ships HIP 6.4),
and fixes shimmering in dark areas and small lights before the upscale. `version.dll` `195c4a89…`, 56,677,888 bytes, in the
setup's `.rdata` at `0x262017`.

- `user_runtimes` with only 0.6.0, `addon_since` 0.7.6, patches from `runtime-patches.json` (`0x64ed`, `0xa8c2`, output `430be589…`).
- `AcceptedRuntimes`: 0.6.0 from OptiScaler 0.4.7 on. `KnownRuntimePrefixes` gained `195c4a891b6eac4c`.
- The size window for the author's `version.dll` was 7 to 40 MB; 0.6.0 is 56.7 MB and would go unnoticed. It now goes up to 80 MB.
- `Str.SupporterPitch.0.6.0` in all 21 languages (RDNA2, HIP SDK 7.2, the shimmering).
- The GPU texts (`Str.NotRadeon`, `Str.HipMissing`) still say RDNA3 and RDNA4 and "Adrenalin ships HIP 7": they remain
  correct for the public version. When 0.6.0 becomes public, they need to mention RDNA2 and HIP SDK 7.2.

## Mapping

- opti `2f39f190` (`v0.4.7-amd-nr`): `kAmd060`, bootstrap, ANCHORS (`derive_060.py`), binary check on 8 runtimes, contracts,
  Setup tests, and 0.6.0 running without a game on an RX 9070 XT (24/24 frames per wait mode, 9.41 ms).
- add-on `0e255e9` (`v0.7.6`): 0.6.0 build in `runtime_offsets.h` (the table moved to `runtime_builds.inc` because of the line
  limit), `runtime_offsets_check.py` passing on the patched file, and 4,937 frames on a D3D11 host at 1080p with the network
  changing the image.
- Neither was played in a game with 0.6.0, nor tested on an RDNA2 card.

Tests: 323 (`UserRuntimeTests`, `SupporterRoutesTests`, `PayloadTests`, `OptiScalerVersionTests` followed the switch); the
test with the real setup (`AMDNR_TEST_RUNTIME_SETUP` pointing to the 0.6.0 setup) passes.

## Publishing

1. opti `v0.4.7-amd-nr` and add-on `v0.7.6` (already published).
2. Installer release v0.7.6 (exe, `SHA256SUMS.txt`, 7z with the json files and LEIA-ME/README), `check-release.ps1`.
3. `publish-payload.ps1 -From <folder with amd-nr.addon64, dlssnr_amd_pass1.dll and dlssnr_amd_runtime-0.5.1.dll>`. The URLs already
   point to new paths (`addon/0.7.6/`, `runtime/0.5.1/`, `opti-runtime/dlssnr_amd_runtime-0.5.1.dll`): the script uploads
   to the path the URL has. Revert the `config.json` rewrite; mirror on AMD-NR-Extras.
