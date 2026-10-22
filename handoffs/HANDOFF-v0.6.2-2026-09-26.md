# Handoff v0.6.2: mochizuki on the ReShade route too

2026-09-26. Read after [HANDOFF-v0.6.0-2026-09-25.md](HANDOFF-v0.6.0-2026-09-25.md), which explains
how mochizuki came into the OptiScaler route. This file covers only what v0.6.2 adds.

## What went in

- **Add-on v0.6.8** (`zmodelerlover/dlss5-neural-amd`, tag `v0.6.8`): the add-on runs the network through
  danielblnc's runtime (default) or through mochizuki, chosen in the panel's "NR runtime" combo
  (`NrBackend` in `amd-nr.ini`, takes effect on the next start). With `NrBackend=mochizuki` and the files
  missing, the add-on runs danielblnc's.
- **On the ReShade routes' sheet** (64-bit and the 32-bit bridge), the same mochizuki box as the
  OptiScaler route, with its own text (`Str.MochizukiCheckAddon`, `Str.MochizukiNoteAddon`). It shows up
  when the chosen add-on is 0.6.8 or newer (`GameSheet.MochizukiAddon`) and the payload has a
  mochizuki build. Same RDNA4 lock and same per-game memory (`GameEntry.Mochizuki`).
- **Checked**, the install puts `MochizukiNrRuntime.dll` and `dlssnr-amd\` next to the add-on (on the
  bridge, next to `amd-nr-host64.exe`, which runs the network). **Unchecked**, the next install removes
  what a previous one put there, in the same transaction, as on the OptiScaler route.
- **The installer does not write `amd-nr.ini`.** It belongs to the user and the add-on rewrites it on save, so
  a registered config entry would become "PRESERVED" on the following install. The report says
  to pick mochizuki in the panel. The add-on's fallback covers the case of an ini that still names
  mochizuki after it was unchecked.

## Where the ReShade route's mochizuki comes from

`PayloadManifest.WithMochizuki()`: the `mochizuki` and `mochizuki-model` components of the newest
OptiScaler release that carries them (today 0.4.1-amd-nr, mochizuki 0.4.0-amd-nr and model 1). **The
payload format did not change**: no new top-level component, so old apps do not download
anything extra on "Download all" and v0.6.1 keeps reading the payload as before. The sheet's `Selected()`
applies this to the ReShade routes, and so does `PinnedFor`, so that "outdated" compares the
mochizuki files in a ReShade folder that has them.

Consequence: a new OptiScaler with a different mochizuki build also swaps the ReShade route's. If
the add-on ever needs a build different from OptiScaler's, the way to go is a component of its own
inside a `releases` (where old apps do not look).

## Code

- Core: `Work.Preflight`/`Install` pass `mochizuki` to the ReShade routes (`PreflightReShade`,
  `InstallReShade`, `InstallX86`); `X86Installer.Install` accepts `extra` and `retire`;
  `InstalledManifest` and `HasMochizuki` look at both manifests (x64 and x86); uninstall calls
  `AfterMochizukiUninstall` on every route.
- App: `ComponentsFor` includes both components on the ReShade routes when requested;
  `OffersMochizuki` decides the section on all three routes.
- Tests: `ReShadeMochizukiTests` (6 cases, 260 in total).

## Publishing

1. Add-on v0.6.8: GitHub release with the usual six assets, downloaded back and verified.
   `amd-nr.addon64` `7552204c…` (622,592 bytes), `amd-nr.addon32` `8bc7dd66…`, `amd-nr-host64.exe`
   `de44f95e…`, `payload.sha256` `a717b80c…`.
2. The four files on HF in versioned paths (`addon/0.6.8/`, `bridge/0.6.8/`), with the GitHub
   release as a mirror.
3. End to end with this version's engine, empty cache, everything downloaded from the
   `payload.json` addresses: 64-bit route with mochizuki, reinstall, unchecked (everything goes), checked again,
   uninstall; 32-bit bridge with mochizuki, unchecked, uninstall. Passed.
4. `gate.ps1 -Ui` with no failures.
5. Installer release v0.6.2, `publish-payload.ps1`, mirror on AMD-NR-Extras.

## What was left out

- The mochizuki status line in the 32-bit bridge panel (it does not go through the bridge protocol).
- A UI flow (`tools/uishot`) for the box on the ReShade route; the OptiScaler route's one remains.
