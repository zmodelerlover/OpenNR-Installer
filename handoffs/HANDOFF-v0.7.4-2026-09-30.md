# Handoff v0.7.4: FiveM, shadPS4 and Kyty, and the support card

2026-09-30. Read after [HANDOFF-v0.7.3-2026-09-30.md](HANDOFF-v0.7.3-2026-09-30.md). Only the app changes; the payload, HF
and AMD-NR-Extras stay as in v0.7.3.

## FiveM

`Preset.FiveM` (last in the enum, so the old `games.json` reads the same), `Work.FiveM.cs`. FiveM loads ReShade and add-ons
from `FiveM.app\plugins`, but runs GTA V as `data\cache\subprocess\FiveM_b####_GTAProcess.exe`, and the add-on looks for the
runtime and the weights next to the running executable (`ExeDirectory()` in `neural.cpp`). One transaction and one manifest,
rooted at `FiveM.app`, write to both places: `plugins/amd-nr.addon64`, `plugins/<proxy>`, `plugins/ReShade.ini`,
the effect in `plugins/reshade-shaders/Shaders/`, and `data/cache/subprocess/` with the runtime and the weights. The names are in
`Engine.Allowed`; `Manifest.Decode` accepts the `FiveM` preset (without that, uninstall fell back to "by name" and the user's
ReShade backup did not come back).

What is in `plugins` belongs to the user. A ReShade already there stays when the add-on loads in it (`FitOf`): the build with
full add-on support, which is not signed (the normal one is signed with CN=ReShade and disables add-ons in online games), at
6.8.0 or newer. The add-on is compiled against ReShade API 20; 6.7.3 is API 18 and 6.8.0 is the first with 20 (read from
each tag's `reshade.hpp`). Otherwise ReShade is replaced by 6.8.0 under the same name, with a backup, and uninstall gives it back.
A `dxgi.dll` that is not ReShade is never replaced. `ReShade.ini` only gains the add-on panel and loses the add-on from
`DisabledAddons`. `Engine.IsConfig` compares by file name, so that `plugins/ReShade.ini` is configuration too.

`FiveM` goes into the `Emulators.Known` table because it has the same shape (a host with its own route, found by its executable).
`FiveM_Diag.exe` is only a marker (`EmulatorInfo.Markers`): the executable is `FiveM.exe`, one folder up. With Diag as the
executable, Play opened CfxDiag. Scan finds `%LOCALAPPDATA%\FiveM\FiveM.app` (`GameScanner.FiveM`,
`GamePlatform.FiveM`). Nested install markers in `Work.FiveMMarkers`, separate from `InstalledMarkers`.

Tested on the user's FiveM (ReShade 6.7.3 with full add-on support, ENB as `d3d11.dll`, ShaderToggler, MartysMods):
ReShade was replaced by 6.8.0 with a backup identical to the original, "Registered add-on AMD Neural Rendering using ReShade API
version 20", D3D11 depth 1920x1080, danielblnc's runtime 0.5.0, "engine ready", two passes. Before that, install and
uninstall ran on a copy of the real `plugins`: everything came back byte for byte.

## shadPS4 and Kyty

Kyty (`kyty_emulator.exe`, `fc_script.exe`) went into the table. shadPS4 gained `Launchers`: the Qt launcher folder is
recognized, and pre-flight refuses to install next to it, naming the build folders (`Emulators.BuildsUnder`). Both
show up by name in Add > Emulator. Neither imports `vulkan-1.dll` (read from the PE of the 2026-09-30 builds); the add-on
hooks the dynamic loader since v0.5.3, so the sentence "the game must import vkCreateDevice statically" was removed from the
Vulkan note in all 21 languages. `NoteVulkanLayer` warns when someone else's layer does not fit (old or signed).

## Support

Grey coffee cup above the version in the rail; it opens `SupportSheet`, in the "What's new" frame (same names, same styles), with the
text and the cards for Ko-fi (`ko-fi.com/proceduralnilo`) and Vakinha. Texts in all 21 languages.

## Publishing

Installer release v0.7.4 (exe, `SHA256SUMS.txt`, 7z with the json files and the LEIA-ME/README), `check-release.ps1`. No
`publish-payload.ps1` and no mirror: the payload did not change.

## Open

- shadPS4 and Kyty were not tested inside the emulator.
- A `FiveM` folder added by hand (instead of `FiveM.app`) installs, but the card's installed badge does not show up.
