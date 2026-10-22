# Handoff v0.7.7: OptiScaler 0.4.8, mochizuki 0.4.8, add-on v0.7.7 and the 32-bit bridge up to date

2026-10-01. Read after [HANDOFF-v0.7.6-2026-10-01.md](HANDOFF-v0.7.6-2026-10-01.md).

## The 32-bit bridge was stuck on 0.7.2

A player saw the 32-bit bridge labeled as .7.2 and the 32-bit game receiving add-on 0.7.2. The add-on releases from
0.7.3 to 0.7.6 published the 32-bit pair (`amd-nr.addon32`, `amd-nr-host64.exe`, `payload.sha256`), but only the
`addon` component of `payload.json` was updated: `bridge` stayed on 0.7.2. That is also why the 0.6.0 supporter build did not
show up in 32-bit games (`addon_since` is compared with the bridge version on that route).

- `bridge` is now 0.7.7, with the pair from the same release as the add-on.
- `PayloadTests.TheShippedManifestAgreesWithTheAddOnsOwnConstants` fails if `bridge` and `addon` have different
  versions. When pinning a new add-on, pin the bridge from the same release.

## Add-on v0.7.7

- Style (Default, Natural, Cinematic) goes to the network's own style input, as in danielblnc's overlay;
  the colour grade that imitated NVIDIA's was removed.
- OpenGL games (Firestorm) crashed when creating contexts: the `vkCreateDevice` hook thread held the add-on
  after ReShade's `FreeLibrary` and unloaded it on exit (`amd-nr.addon64_unloaded`, `__vcrt_freefls`). It now only
  exists when ReShade runs as a Vulkan layer.

## OptiScaler 0.4.8-amd-nr and mochizuki 0.4.8-amd-nr

- danielblnc's colour like his own: Linear encoding by default and Style in the network. An `OptiScaler.ini` from before 0.4.8
  (has `AmdColourGrade`, does not have `AmdStyle`) reads the `AmdEncoding=2` that the old package wrote as Linear until the user
  saves the settings. The app did not need to change: an ini kept as the user's configuration migrates on its own.
- Watch Dogs: Legion: green screen (exposure only for HDR input) and NR below 100% (TYPELESS vectors).
- mochizuki: game exposure, `dlssnr-amd\` next to the exe (RE Engine and `_storage_`), history weight,
  colour formats and upstream's Preprocess. Component `mochizuki` 0.4.8-amd-nr (`pin-mochizuki.ps1`, 62 files,
  new `runtime_prep.spv`, prewarm list made for these 59 shaders). `mochizuki-model` stays 1.
- `opti-runtime` is 0.4.7's 0.5.1, copied by hand.

Tests: the app's (`dotnet test`) and the gate. None of this was played after packaging; the games mentioned are where each
fix was seen or where the log came from.

## Publishing

1. opti `v0.4.8-amd-nr` and add-on `v0.7.7` already published.
2. Installer release v0.7.7 (exe, `SHA256SUMS.txt`, 7z with the json files and LEIA-ME/README), `check-release.ps1`.
3. `hf upload` of the mochizuki zip (`mochizuki/0.4.8-amd-nr/`), as `pin-mochizuki.ps1` prints.
4. `publish-payload.ps1 -From <folder with amd-nr.addon64, amd-nr.addon32, amd-nr-host64.exe and payload.sha256 from v0.7.7>`:
   the URLs already point to `addon/0.7.7/` and `bridge/0.7.7/`. Revert the `config.json` rewrite; mirror on
   AMD-NR-Extras.
