# Handoff v0.6.6: a folder with danielblnc's setup is no longer "ReShade installed"

2026-09-27. Read after [HANDOFF-v0.6.5-2026-09-27.md](HANDOFF-v0.6.5-2026-09-27.md). Only the app changes; the
payload, HF and AMD-NR-Extras stay as in v0.6.5.

## The problem

danielblnc's runtime writes `dlssnr_on_amd_weights.bin` next to `version.dll`. That name is one of the
`InstalledMarkers`, so a folder with only his setup (no manifest, no add-on) showed up as an installed
ReShade route. Choosing OptiScaler asked for a route switch, and that switch's uninstall deleted the
author's weights by name. OptiScaler then installed and used `version.dll`, so it seemed to work.

Even without the switch, the OptiScaler uninstall discarded the backup of those weights, because the name is ours.

## Code

- `Work.IsAuthorsWeights`: weights next to a `version.dll` do not count in `GameScanner.IsInstalled`.
- `Work.Uninstall`: with the author's `version.dll` in the folder or in a manifest's backup, the weights are not
  "ours" (the backup is restored) and the by-name sweep does not delete them.
- `AuthorRuntimeName` became `internal`.

## Tests

- `LibraryStateTests.TheAuthorsOwnSetupIsNotAnInstallOfOurs`: a folder with the author's `version.dll` and weights is not
  an install, installs OptiScaler, uninstalls, the author's weights come back. 264 in total; `gate.ps1 -Ui` with no
  failures.
- Tested by the user in God of War with a local build before the release.
