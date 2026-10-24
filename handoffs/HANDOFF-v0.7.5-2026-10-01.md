# Handoff v0.7.5: the shadPS4 launcher, where the install writes, and the FiveM report

2026-10-01. Read after [HANDOFF-v0.7.4-2026-09-30.md](HANDOFF-v0.7.4-2026-09-30.md). Only the app changes; the payload, HF
and AMD-NR-Extras stay as in v0.7.3.

## shadPS4 through the Qt launcher

A player had `shadPS4QtLauncher.exe` loose in Downloads and got "shadPS4.exe is neither in it nor under it". The
launcher does not keep the builds near itself: the data folder is `launcher\` next to it when it exists (portable) and
`%APPDATA%\shadPS4QtLauncher` otherwise (shadps4-qtlauncher's `path_util.cpp`). There, `versions.json` lists the `path` of each
build's executable and `qt_ui.ini` `[version_manager] versionSelected` says which one it opens.

`EmulatorInfo.LauncherData` and `Emulators.BuildsUnder` read that list, the selected one first, before searching
subfolders. `GraphicsDetector.ForEmulator` uses the executable the user chose, then the folder's one, then that build;
the install goes to its folder. The chosen executable now applies to emulators too (before, emulator detection
ignored it). On the sheet, "Choose…" accepts the same emulator's executable outside the card's folder. Test:
`ALaunchersOwnListSaysWhichBuildItStarts`.

## Where the install writes

The sheet shows "Installs to: <folder>" below the executable (both folders for FiveM), and a warning when an
emulator's build is outside the added folder, with how to choose another. 21 languages.

## FiveM report

The report looked at `FiveM.exe`'s folder. Now it gathers the listing and logs of `plugins` and `data\cache\subprocess` and the manifest
of `FiveM.app`. The report that prompted this (NordPC) was from a session before the install: the manual guide's add-on in
`plugins` without `dlssnr_amd_pass1.dll` next to the game process, which is the add-on's "Not installed in this game's folder";
the 01:11 install created the file, and the report came out 8 s later, without the game having been opened again.

## Publishing

Installer release v0.7.5 (exe, `SHA256SUMS.txt`, 7z with the json files and the LEIA-ME/README), `check-release.ps1`. No
`publish-payload.ps1` and no mirror.
