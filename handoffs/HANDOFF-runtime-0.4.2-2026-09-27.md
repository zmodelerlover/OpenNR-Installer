# Handoff: danielblnc's runtime 0.4.2 in OptiScaler 0.4.3

2026-09-27. Read after [HANDOFF-v0.6.4-2026-09-27.md](HANDOFF-v0.6.4-2026-09-27.md). Only the payload changed:
no new installer build, v0.6.4 reads `payload.json` from HF and already picks up the change.

## What changed in the payload

| Component | Before | Now | Where in the dataset |
|---|---|---|---|
| `opti-runtime` of release `0.4.3-amd-nr` | 0.4.1 (`823063eb…`, 9,916,928 bytes) | 0.4.2 (`8aa2dcc5…`, 12,981,760 bytes) | `opti-runtime/dlssnr_amd_runtime-0.4.2.dll` |

- danielblnc published 0.4.2 on 2026-09-27 ([Alpha 0.4.2](https://github.com/danielblnc/DLSS-NR-on-AMD/releases/tag/v0.4.2)).
  The public `dlssnr_on_amd_setup.exe` (`08aecc04…`) is byte for byte the supporter build we already had, and the
  `version.dll` extracted from it via the PE headers (offset `0x260a27`, as in `install-amd-presr.ps1`) is
  `8aa2dcc5…`, the `kAmd042` of opti 0.4.3's `AmdLayout.h`. 0.4.3 remains supporter-only and out of the payload.
- **Only 0.4.3-amd-nr changes.** 0.4.2-amd-nr stays on 0.4.1 and 0.4.1-amd-nr on 0.4.0: no
  OptiScaler before 0.4.3 accepts 0.4.2 by SHA.
- **The ReShade route does not change:** `runtime` stays the patched 0.4.1 (`c8808716…`) and the add-on v0.6.9, which
  does not accept 0.4.2 yet. The weights are the same as 0.4.1's.
- Anyone who already installed 0.4.3 sees the folder as outdated (the pass files no longer match the pin) and updates.

## Code

- `OptiScalerVersionTests`: 0.4.3 ships with `opti-runtime` 0.4.2 (`8aa2dcc5…`); 0.4.2 and 0.4.1 as before.
- Comment on `Work.OptiScaler.KnownRuntimePrefixes`: 0.4.2 is no longer a supporter build.

## Publishing

1. `gate.ps1 -Ui` with no failures.
2. `publish-payload.ps1 -Repo zmodelerlover/amd-nr -From <folder with dlssnr_amd_runtime-0.4.2.dll>`: one new
   file on HF, `payload.json` live, everything downloaded back and verified.
3. End to end with the engine, empty cache, everything downloaded from the HF addresses: OptiScaler 0.4.3 with the
   previous payload (the three pass files `823063eb…`), the new payload marks the folder as outdated, reinstall with
   the three pass files `8aa2dcc5…`, after which it no longer flags it, and uninstall leaves the folder as it was.
4. `payload.json` copied to AMD-NR-Extras (the second address the app reads from).

## Tested beforehand

- 0.4.2 inside OptiScaler 0.4.3 in game, by the user, with the early access build (the same file).
