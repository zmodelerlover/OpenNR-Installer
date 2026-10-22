# Handoff v0.7.8: add-on v0.7.8 (D3D10, OpenGL 32-bit), OptiScaler 0.4.9, mochizuki 0.4.9 and favorites

2026-10-02. Read after [HANDOFF-dx10-gl32-2026-10-01.md](HANDOFF-dx10-gl32-2026-10-01.md), which has the details of the
D3D10 and OpenGL 32-bit routes, the per-game rules (`GameQuirks`) and the favorites.

## What ships

- **Add-on v0.7.8** (`addon` and `bridge` 0.7.8 together): D3D10 64/32-bit, OpenGL 32-bit, the fix for
  ReShade 6.8.0's `D3D10CompileShader` (Just Cause 2) and the stand-down on GPU stall (`StallStandDownMs`).
  `host_check --cross` on the release binaries: all routes equal within noise, except x86-d3d9, which differs
  only by the capture's BGRA order (already documented).
- **OptiScaler 0.4.9-amd-nr**: lmxxf 0.39 (Style, reuse, compact 1080 band, RX 9060), mochizuki 82560c4, new menu,
  NR in the FPS overlay, hang protection, DLSS logs. `opti-runtime` 0.5.1 copied from 0.4.8.
- **mochizuki 0.4.9-amd-nr** (`pin-mochizuki.ps1`, 68 files, 38-line prewarm for 65 shaders). Model
  stays 1.
- **New component `lmxxf-gfx1200`**: the 34 files of `lmxxf-modules-gfx1200/`. The published apps reject that
  name, so `pin-optiscaler.ps1` removes those entries from the `optiscaler` component, builds
  `lmxxf-gfx1200-<version>.zip` (fixed order and date) and pins it as its own component, on demand; only app v0.7.8
  requests it (`ComponentsFor`, `Pins`, `PinnedForOptiScaler`). Published at `lmxxf-gfx1200/0.4.9-amd-nr/` on HF.
- **App**: D3D10 and OpenGL 32-bit routes, `GameQuirks`, favorites and library order, lmxxf message mentioning the
  RX 9060 when the release ships the kernels.

## Verified

- `gate.ps1 -Ui`: 0 failures, 329 tests. `OptiScalerVersionTests` and `UserRuntimeTests` now expect 0.4.9; the
  former checks that the RX 9060 kernels are in 0.4.9's pins and outside the `optiscaler` component.
- Real install over the internet (harness in the scratchpad, temporary `AMDNR_HOME`): the new payload downloaded 0.4.9 from
  GitHub and the rest from HF, installed with mochizuki in a test folder (35 + 34 modules, all verified) and the
  uninstall left only `OptiScaler.ini`.
- `ApiDbBuilder` was refused again by PCGamingWiki when redoing the 168 misses; `api-db.json` and
  `api-db.misses.txt` stayed as they were. D3D10 games are still detected by the import table and the per-game rules.

## Publishing (done in this order)

1. Add-on `v0.7.8` (GitHub, six files) and opti `v0.4.9-amd-nr` (zip).
2. `hf upload` of `mochizuki-0.4.9-amd-nr.zip` and `lmxxf-gfx1200-0.4.9-amd-nr.zip`.
3. Installer release v0.7.8 (exe, `SHA256SUMS.txt`, 7z with the json files and the LEIA-ME/README), `check-release.ps1`.
4. `publish-payload.ps1 -From <folder with the six v0.7.8 files>`; revert the `config.json` rewrite;
   mirror on AMD-NR-Extras.
