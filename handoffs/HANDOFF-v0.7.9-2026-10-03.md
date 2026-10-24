# Handoff v0.7.9: public danielblnc 0.6.0 as the runtime of both routes

2026-10-03. Read after [HANDOFF-v0.7.8-2026-10-02.md](HANDOFF-v0.7.8-2026-10-02.md).

danielblnc published [Alpha 0.6.0](https://github.com/danielblnc/DLSS-NR-on-AMD/releases/tag/v0.6.0) on 2026-10-03. The public
setup (`20636c95…`, 59,841,536 bytes) is byte for byte the supporter one, with the same `version.dll` `195c4a89…` that the add-on
(v0.7.6+) and OptiScaler (0.4.7-amd-nr+) already ran.

## Payload

- ReShade route (64-bit and 32-bit bridge): `runtime` 0.6.0, `dlssnr_amd_pass1.dll` with the patches from `runtime-patches.json`
  (`430be589…`, 56,677,888 bytes), at `runtime/0.6.0/` on HF. Same weights (`6bf8dc93…`).
- OptiScaler route: `opti-runtime` 0.6.0 (`dlssnr_amd_runtime-0.6.0.dll`, the raw `version.dll`) in releases 0.4.7, 0.4.8 and
  0.4.9-amd-nr, the ones that accept 0.6.0. 0.4.6 stays on 0.5.0.
- `user_runtimes` empty: the "danielblnc runtime" block disappears from the sheet (`RuntimeSection.IsVisible = listed.Count > 0`).
  Anyone who already had the 0.6.0 supporter build installed keeps the same file the payload now delivers.
- Apps v0.7.6 to v0.7.8 already work with this payload: the pins come from the manifest and the pinned add-on (v0.7.8) accepts `430be589…`.

## App

- `Engine.RuntimeSha` = `430be589…`; `af67f066…` (patched 0.5.1) went to `EarlierRuntimeShas`; `RuntimeSize` 56,677,888.
- `Str.HipMissing` and `Str.NotRadeon` in all 21 languages mention RDNA2 and HIP SDK 7.2 for the RX 6000. `FindHip7` did not
  change: it looks for `amdhip64_7.dll` in the system folder and on PATH.
- README: requirements with RDNA2, and the supporter builds section says the list is empty.

## Tests

`gate.ps1 -Ui`: 0 failures, 329 tests. `PayloadTests`, `OptiScalerVersionTests` (0.6.0 on 0.4.7 to 0.4.9, 0.5.0 on 0.4.6),
`SupporterRoutesTests` (0.5.1 is now outdated) and `UserRuntimeTests` (empty list; the real-setup test uses the
0.6.0 entry as it was on the list) followed along. With `AMDNR_TEST_RUNTIME_SETUP` pointing to the public setup, the
patch gives exactly `430be589…`. In opti, the D3D11 final-image route ran with 0.6.0 on an RX 9070 XT
(`AMD runtime 0.6.0`, smoke with no failures).

## Publishing

1. Installer release v0.7.9 (exe, `SHA256SUMS.txt`, 7z with the json files and LEIA-ME/README), `check-release.ps1`.
2. `publish-payload.ps1 -From <folder with dlssnr_amd_pass1.dll (430be589) and dlssnr_amd_runtime-0.6.0.dll (195c4a89)>`.
   Revert the `config.json` rewrite; mirror on AMD-NR-Extras.
