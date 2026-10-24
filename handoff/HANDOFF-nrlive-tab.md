# NRLive in the OpenNR Installer (branch `nrlive-tab`)

Branch `nrlive-tab` in `E:\Projetos\opti_rr_dlss5\wt-installer-nrlive`, from installer master 862a51d (v0.8.6). Not pushed.
NRLive itself (`E:\Projetos\opti_rr_dlss5\NRLive`, main fa859d3, 2.2.0) was only read.

## What it does

- **NRLive page**: a new rail button under Games (icon `IconScale`). It replaces NRLive's WPF launcher for OpenNR users:
  - **Package card**: NRLive is downloaded on demand into `%AppData%\AmdNrInstaller\nrlive\` (AMDNR_HOME respected), checked
    against its pin like every component, and unpacked there. States: not published (placeholder or no entry, with a link to
    NRLive's releases), to download (size shown), downloading (progress), ready (folder button), update available.
  - **Scaling session**: what to capture (the window in front when the countdown ends = `-picker`; a window open now = `-pid`,
    from an EnumWindows list with a refresh button; a game from the library = `-pname ^Exe\.exe$`), the countdown (3 to 30 s),
    the Scale button that turns into "Starting · n" and then Stop, a status line, and "Run as administrator" (on by default,
    as in NRLive's launcher).
  - **Settings**: three cards (Upscaling and frames, Neural rendering, Overlay), each a list of rows built from
    `NrLiveConfig.Settings`, the advanced ones behind "More settings". Written to NRLive's `scaleconfig.ini` the moment they
    change, edited in place (every other byte kept), so the page and NRLive's in-game overlay never disagree. The page reads
    the file again every time it is shown.
  - **Capture**: capture source (WGC / DXGI) and motion vectors (FastMV / AMD optical flow).
  - **Hotkeys**: scale-and-stop (registered with Windows by this app, works from inside the game while the app is open),
    force stop, overlay, passthrough keys. A box takes the combination pressed in it; conflicts and unreadable keys hold Scale
    back and say why.
  - **Credit**: "NRLive is made by Caliel ... GPL-3.0", link to github.com/Caliel666/NRLive, in the title row. Also a row in the credits panel
    (first-run wizard and Settings > About).
- **Anti-cheat warning on the game sheet**: when the game folder (or the executable's folder, when outside it) has an
  anti-cheat, a warning card under the result banner: what was found, why an install risks the account, "Use NRLive" (opens the
  page with that game picked) and "Ignore for this game" (saved as `GameEntry.AntiCheatIgnored` in games.json). Installs are not
  blocked.

## Design notes

- **Payload**: component `nrlive` under `releases.nrlive` (never under `components`, so the installers already published never
  see it). `PayloadManifest.NrLive()` returns the newest release that is not a placeholder; `nrlive` is in `OnDemand`, so
  "Download all" and the first-run wizard never fetch it. The shipped `payload.json` has `releases.nrlive[2.2.0]` with
  `PlaceholderSha` and size 1, so the page says "not published yet". The release also lists `mochizuki-model` and
  `mochizuki-model-small` (the pins OptiScaler 0.5.7 already uses); `NrLivePackage` copies every non-zip file of the release
  into `opennr\dlssnr-amd\` under the NRLive folder (see "Aligned with NRLive's real interface"), because NRLive's
  own package ships without the NVIDIA model.
- **Important**: `tools/publish-payload.ps1` and `tools/check-release.ps1` refuse a payload with a placeholder pin. Before the
  next payload publish or installer release, run `tools/pin-nrlive.ps1 -Zip <NRLiveUI.zip> -Version <v>` (new; checks the
  zip's root has NRLive.exe and version.txt = -Version and no NVIDIA model, pins it with the HF address and NRLive's GitHub
  asset as mirror, keeps the model entries, prints the `hf upload` command), or delete the `releases.nrlive` entry.
- **Folder layout**: one folder for every version, like NRLive's own updater: an update writes over the package and keeps
  `scaleconfig.ini`, `ui_config.json` and `profiles\`. `opennr-package.txt` (version and zip SHA-256) is written last, so a
  package cut off halfway reads as not installed. A zip wrapped in one top folder is unwrapped; an entry that climbs out of the
  folder fails the install.
- **Profile**: `nrlive\profiles\Default.json` in NRLive's launcher's own snake_case shape (`target_mode`, `delay`,
  `scale_hotkey`, ...), so NRLive's launcher opened from that folder finds the same setup.
- **Pending keys**: a row marked `Pending` is shown only when the NRLive.exe in the folder contains both its section and key as
  null-terminated UTF-16 strings (the literals `GetPrivateProfileIntW` is called with). So once NRLive reads a new key, its row
  appears with no change here; until then nothing that does nothing is on screen.
- **Stop**: `Process.Kill`; when NRLive runs elevated that is denied, so its own stop key is sent with SendInput, and if even that
  fails a toast says which key to press. Closing the installer stops a running session (NRLive's launcher does the same).
- **Anti-cheat detection** (`AmdNr.Core.AntiCheat.Find`): names only, breadth first, 4 levels deep, at most 20,000 entries,
  links not followed. Known: Easy Anti-Cheat, BattlEye, Riot Vanguard (by VALORANT / League executables, since vgk lives in
  Program Files), nProtect GameGuard, XIGNCODE3, Ricochet, ACE, mhyprot, FACEIT, EA Javelin, PunkBuster, NetEase, EQU8. Cached
  per folder for the session.
- **Steam data: skipped.** The store API (`appdetails`) has no anti-cheat field (checked with Apex Legends, 1172470). The store
  page HTML has an `anticheat_section` box ("Uses Kernel Level Anti-Cheat ... Easy Anti-Cheat"), but it is developer-filled,
  kernel-only, 200 KB of HTML per game and not an API. Local `appinfo.vdf` only carries Steam Deck results such as
  `UnsupportedAntiCheatConfiguration`. None of it is more reliable than the folder.

## What it expects from NRLive

- `NRLive.exe` at the package root, `scaleconfig.ini` read from the exe's folder at start (`overlay.cpp`, `loadCfg`),
  `NRLive.log` there too.
- Command line exactly as NRLive's launcher builds it (`target.cpp`, `parseTargetArgs`):
  `-picker <delay>` | `-pid <pid> -delay <s>` | `-pname <regex> -delay <s>` (plus `-window`/`-front`, kept in the profile),
  `[-nooverlay] --mv fast|amdof --capture wgc|dxgi --key <stop> --overlaykey <key> --bindbypass <list>`.
- Keys wired now (NRLive main 2.2.0): `[FSR] sharpness`, `[FrameGeneration] enabled`, `[FrameLimiter] enabled fps method`,
  `[DLSSNR] enabled model style scale intensity passes structure color_strength local_tone max_ratio skin_structure
  history_strength automatic_skin_mask stabilizer`, `[FPS] enabled position fps frametime frame_timing resolution background
  text_outline font_size background_alpha`. Ranges and defaults are NRLive's own.
- Release asset `NRLiveUI.zip` (CI `windows-build.yml`), flat, with `version.txt`.

## Aligned with NRLive's real interface (2026-10-24)

Source: NRLive branch `nrlive-overlay-on-runtime` (`E:\Projetos\opti_rr_dlss5\wt-nrlive-stack`, = `nrlive-runtime` +
`nrlive-overlay`, see its `handoff\HANDOFF-stack.md`). NRLive main 2.2.0 still has none of this, so every new row is
`Pending` and appears only once the NRLive.exe in the folder reads its key.

- **Upscaler**: not an ini key. Kept in the profile (`profiles\Default.json`, `"upscaler": "fsr4" | "fsr31" | "none"`,
  the same field NRLive's launcher writes) and passed as `--upscaler <id>` only when NRLive.exe contains the wide
  literal `--upscaler` (an NRLive that does not know it exits on it). Row "Upscaler" at the top of the first card.
- **`[DLSSNR] runtime`** = `auto | opennr | bridge` (written as the word; `NrLiveSetting.Values`).
- **`[OpenNR]`** rows (OptiScaler key names, ranges/defaults from NRLive's opennr.cpp + MochizukiRenoKeys.inc):
  MochizukiOriginalModel, MochizukiRenoStyle (0-2), MochizukiRenoIntensity (0-2), MochizukiRenoPasses (1-4, as the
  launcher), advanced: MochizukiRenoLocalTone, MochizukiRenoLocalStructure, MochizukiRenoSkinStructure (-1..1),
  MochizukiRenoAutoMask, MochizukiRenoSkinIndependent, MochizukiRenoColorStrength, MochizukiTemporal,
  MochizukiHistoryStrength, MochizukiStyleGrade, MochizukiAco. Decimals are shown as percent and written as
  `0.85` (`NrLiveSetting.Scale = 100`); booleans read true/false/on/off/1/0 and are written 1/0 (NRLive reads both).
  Int4 is left out: the installer has no int4 weights for NRLive.
- **Which NR rows show** (`NrLiveSetting.Only`, `NrLiveConfig.Tags`): the [OpenNR] set when the runtime is `opennr`,
  or `auto` with `opennr\MochizukiNrRuntime.dll` (or the DLL beside NRLive.exe) present; the old `[DLSSNR]` set
  (model, style, scale, ...) otherwise, and always for an NRLive without the runtime key. Changing the runtime lays
  the card out again. `[RenoDX] enabled` is gone (it never existed; the RenoDX stage is the [OpenNR] Reno* keys).
- **HUD** (`[FPS]`, from the overlay handoff): preset (0 custom, 1 minimal, 2 compact, 3 standard, 4 full),
  position with 6 entries, pipeline, gpu_load, gpu_temp; advanced lows, vram, cpu_load, gpu_times, scale (50-250 %),
  background_alpha (default 80). NRLive only applies a preset from its own UI, so choosing one here writes the
  item toggles and `layout` the way its launcher's dialog does; changing an item sets `preset=0`. With an older
  NRLive (no `preset` key) the old rows stay: 4 positions, font_size, background_alpha default 50.
- **Detection** (`NrLiveConfig.Supported`): section wide + key wide, or for [OpenNR] the key narrow (NRLive
  widens those names at run time), each with its terminator; plus `cli.upscaler`.
- **Models**: the release's `mochizuki-model` / `mochizuki-model-small` files are now hard-linked from the verified
  cache copy into `nrlive\opennr\dlssnr-amd\` (copied where links fail; `PayloadCache.TryHardLink` made internal),
  not into `dlssnr-amd\` beside the exe: the 0.0.6 bridge reads that one and its 310.9.16 files are not the same
  models. NRLive's own fallback (linking `<exe>\dlssnr-amd\*.bin` into `opennr\dlssnr-amd`) skips names already
  there. The page's "no model" callout looks in `opennr\dlssnr-amd` when OpenNR is the runtime and says to download
  NRLive again (`Str.NrLiveModelMissingOpenNr`), else in `dlssnr-amd` with the old text. The `nrlive` component is
  still the placeholder.
- **Strings**: 41 new/changed in all 21 languages (removed `Upscaler.mode*`, `RenoDX.enabled`, `FPS.gpu`; runtime
  menu now Automatic / OpenNR (mochizuki) / NRLive's 0.0.6 bridge; `Str.NrLiveUpscaler*`, the [OpenNR] labels,
  the HUD labels, `FPS.position.4/5`, `Str.NrLiveModelMissingOpenNr`). The 20 translations were written by two
  sub-agents from each language's existing NrLive file; mochizuki's first Style is "Standard" (the bridge's is
  "Neutral").
- **Tests**: Supported/Tags/Shown for 2.2.0 and the stacked build, decimals/words/booleans/preset writing, models
  linked into `opennr\dlssnr-amd`, `--upscaler` in the command line only when asked. uishot flow: the upscaler row
  appears with `--upscaler`; new step `flow-nrlive-opennr` (stand-in exe with the new literals + a runtime DLL: 6 NR
  rows, the OpenNR model callout, 7 overlay rows). `tools\gate.ps1 -Ui`: build, test, line-limit, publish, ui,
  flows all OK, 0 failed gates.
- **Not tested**: the page against the real stacked NRLive.exe (it is not packaged as a release zip yet).

## Layout (2026-10-24, three columns)

From `handoff/mockup-nrlive-three-columns.png` (a concept the user liked). The page fits the default window
(1240x800) without a scrollbar once NRLive is ready; the ScrollViewer (`PageScroll`) stays for smaller windows.

- **Title row**: "NRLive" + an i with the old subtitle as tooltip, the GPL credit line (trims on narrow windows, the
  github link never does), and, when NRLive is installed and current, a slim "NRLive x is ready" + Open folder
  (`PackageSlim`). The full package card shows only for not published / download / downloading / update. The old
  footer callout is gone (the credit moved up).
- **Session + hotkeys card**: labels before the menus (not over them); the target menu spans the item menu's room
  when there is nothing to pick; status is one line (small tile, title, text) with countdown and Scale at its end;
  hotkey boxes carry their names inside (`TextBox.InnerLeftContent`), 4 across, 2 under 720 px.
- **Capture strip**: icon tile, title + i (the old settings hint `Str.NrLiveSettingsHint` as its tooltip), subtitle
  `Str.NrLiveCaptureHint`, and the two menus each in a bordered group (`nrGroup`) on the right; under the
  three-column width they move under the title.
- **Three cards** (`SettingsGrid`): Upscaling and frames | Neural rendering | Overlay, equal height (one Auto row),
  "More settings" docked at each card's foot. Header: accent bar (`nrBar`, red; `.ok` green for Overlay), title, and
  on the right the GPU badge (`ShowMachine`: "RDNA 4" or the adapter name, from the machine read at start), the
  [DLSSNR] enabled switch, or the profile's overlay switch. `Arrange()` in NrLivePage.axaml.cs: 3 across at >= 1000 px
  of page width, 2 across with Overlay under both at >= 620, else 1.
- **Rows are still table-driven** (`NrLivePage.Rows.cs`, `Lay()`), from new init properties on `NrLiveSetting`:
  `Layout` (`Row`, `Header` = the card's header switch, `Half` = menus two to a line with captions over them,
  `Tile` = HUD item tiles, 2 across, the whole tile toggles, tinted when on), `Panel` (consecutive settings with the
  same name share a nested `nrPanel`; a toggle first heads it and greys the rest while off: Frame limit, NR
  intensity/passes or the bridge's scale/intensity), `Hint` (one muted line under a switch, `<LabelKey>.Hint`, full
  text in its tooltip; only frame generation has one), `Sample` (what a HUD item draws, for the preview strip under
  the tiles; the pipeline item is built from upscaler + NR + FG, everything empty when [FPS] enabled is off).
  Sliders: label + value badge (`nrValue`) on one line, the slider full width under them. Choices: combo at the end
  (180 px). The [FPS] rows were reordered (preset, position, then enabled + items) so the halves and tiles run together.
- Each built control has `Tag = setting.Id` (and the upscaler `cli.upscaler`); the flow finds rows by it.
- **Styles**: `Styles/Details.axaml` (`nrCard`, `nrHead`, `nrBar`, `nrTitle`, `nrBadge`, `nrValue`, `nrPanel`,
  `nrGroup`, `nrTile`, `nrPreview`, `nrMore`, `keyName`); Base.axaml is at its 500 lines. `IconCapture` in Icons.axaml.
- **Strings**: 4 new in all 21 languages: `Str.NrLiveCaptureHint`, `Str.NrLiveHudItems`, `Str.NrLiveHudPreview`,
  `Str.NrLiveSet.FrameGeneration.enabled.Hint`; the strings test checks a `Hint` setting has its key.
- **uishot**: `flow-nrlive-default` (1240x800, OpenNR stand-in with a model file, checks the page extent fits the
  viewport and three cards across) and `flow-nrlive-narrow` (980x620, two across, Overlay under them). Known: at
  1240 the overlay tiles trim long labels ("FSR, NR and F...", tooltip has the full name); at 1920 they fit.

## Settings per game (2026-10-24)

Option A as approved: a Global profile (what `scaleconfig.ini` / `profiles\Default.json` hold) plus one per library game.

- **Storage** (`AmdNr.Core\NrLive\NrLiveProfiles.cs`): `nrlive\opennr-profiles\<id>.json` (`global.json` for Global),
  id = folder name + 12 hex of the SHA-256 of the game folder's full path, lower case (`NrLiveGame.IdFor`). Each holds
  `name`, `folder`, `launch` (upscaler, capture_mode, motion, no_overlay, stop_key, overlay_key, bind_bypass) and
  `settings` ("Section.key" -> the text in the ini, for `NrLiveConfig.ProfileKeys` = the table's keys + the HUD items and
  `FPS.layout` a preset writes; a key not in the file is not in the copy, and is taken out of the ini when the copy is
  applied, so NRLive's default applies). Everything else in the ini (`[General] screenshot_path`...) is never touched.
  `holder.txt` says whose copy NRLive's files hold now; switching first saves the files into the holder's copy
  (`Use` -> `Capture`), so nothing NRLive's overlay saved is lost, even after a crash.
- **Kept one for all**: target, countdown, scale hotkey (registered by this app before any game is known), Run as
  administrator, the NRLive launcher's `exe_path`.
- **Page** (`NrLivePage.Profiles.cs`): "Settings for" menu + "Use global settings" (only for a game with its own) + i,
  on the right of the session card's header. Entries: Global, games with their own copy, the target's game and the one
  shown; a game without its own reads "<name> (global settings)". The context follows the target: library game -> that
  game; open window -> `QueryFullProcessImageNameW` of its pid matched against the library (`NrLiveProfiles.Match`:
  exact executable, else the innermost game folder holding it); window in front -> Global until the countdown ends.
  The menu can show another game or Global; Scale goes back to the target's. Menu and reset are locked in a session.
- **First change** (`Own()` before every write of a copied field: settings rows, upscaler, overlay switch, capture,
  motion, stop/overlay keys, passthrough keys): a game shown on the global settings gets `Fork` (its copy made from the
  current files, holder = game). Every write is followed by `Capture` into the holder's copy. `Store` now reads
  scaleconfig.ini again before writing, so a page edit does not overwrite what the overlay saved meanwhile.
- **Window in front**: with Run as administrator off (or this app already elevated) NRLive is not started at Scale:
  the countdown runs here, then `Foreground()` (pid + exe) -> `UseForeground` -> NRLive started with
  `-pid <pid> -delay 0` (`-front -delay 0` when nothing was in front), so the game's launch fields apply too. With Run
  as administrator on (the default) NRLive starts at Scale as before, so the UAC prompt stays in the app and never comes
  up over the game; at the countdown end the game's ini values are written and NRLive follows them live (overlay.cpp
  watches the file's time, opennr.cpp's `NrConfigWatcher` every 500 ms). **Gap**: its upscaler, capture, motion,
  overlay and keys are on the command line and stay Global's for that session; the status line says so
  (`Str.NrLiveProfileLaunchKept`). Picking the game as the target avoids it.
- **Session end** (`Reset`: stopped, exited, or the app closing): `Capture` saves what the overlay changed into the
  copy the session ran with, then the rows are read again.
- **Status line**: "...  ·  Settings: <Global | game>".
- **Strings**: 8 new in all 21 languages (`Str.NrLiveProfileFor`, `Global`, `Shared`, `Reset`, `ResetHint`, `Hint`,
  `InUse`, `LaunchKept`), translated here; worth a native read.
- **Layout**: the nrHead header on the session card had made the page 26 px too tall at 1240x800. That header is lower
  now (no MinHeight/margin, 6 px rule gap, the menu 28 px high), the session card's padding is 16,10, the page spacing 8
  and its margins 12/10. It fits again with the menu and the reset button shown.
- **Tests**: `NrLiveProfilesTests` (copy made on the first change and the global one kept, exe matching with nested
  folders and case, foreground at the countdown end with an injected reader, save-back of what the overlay wrote and
  keys taken out, reset held and not held). uishot `Flows.NrLiveProfiles.cs` (inside the NRLive flow, on the Use NRLive
  game): global until a change, copy made, Global and back, fits 1240x800 (`flow-nrlive-profiles`), countdown with
  admin off and `NrLivePage.Foreground` stubbed applies the game's settings and names it on the status line (then the
  stand-in exe fails to start: `flow-nrlive-profile-foreground`), reset to global. `tools\gate.ps1 -Ui`: all OK.
- **Not tested**: a real NRLive following the ini switch at the countdown end; the `-pid <pid> -delay 0` launch.

## Files

New:
- `src/AmdNr.Core/Library/AntiCheat.cs`, `src/AmdNr.Core/NrLive/NrLivePackage.cs`, `NrLiveConfig.cs`, `NrLiveProfile.cs`,
  `NrLiveProfiles.cs`, `src/AmdNr.Core/Payload/PayloadManifest.NrLive.cs`
- `src/AmdNr.App/NrLive/NrLivePage.axaml(.cs)`, `NrLivePage.Session.cs`, `NrLivePage.Settings.cs`, `NrLivePage.Rows.cs`,
  `NrLivePage.Profiles.cs`, `NrLiveNative.cs` (window list, `GlobalHotkey` on its own thread, SendInput, the window in
  front and a process's executable)
- `tests/AmdNr.Core.Tests/NrLiveProfilesTests.cs`, `tools/uishot/Flows.NrLiveProfiles.cs`
- `src/AmdNr.App/Sheet/AntiCheatCard.axaml(.cs)`, `GameSheet.AntiCheat.cs`
- `src/AmdNr.App/Languages/NrLive/NrLive.<code>.axaml` x21 (merged by each `Strings.<code>.axaml`, which are near the line limit)
- `tests/AmdNr.Core.Tests/AntiCheatTests.cs`, `NrLiveTests.cs`, `NrLiveStringsTests.cs`
- `tools/uishot/Flows.NrLive.cs`, `tools/pin-nrlive.ps1`

Changed: `PayloadManifest.cs` (partial, `nrlive` on demand), `payload/payload.json` (placeholder release), `MainWindow.axaml(.cs)`
(rail button, `Page.NrLive`, `ShowNrLive`, relabel, stop on close), `GameSheet.axaml(.cs)` (warning card), `GameStore.cs`
(`AntiCheatIgnored`), `CreditsPanel.axaml(.cs)` (NRLive row), `Icons.axaml` (`IconScale`, `IconStop`, `IconShield`),
`Strings.*.axaml` (merge line), `tools/uishot/Program.cs` (page in the renders, the flow, hotkeys off).

## Strings added (all 21 languages, `Languages/NrLive/`)

Page: `Str.NrLiveSubtitle`, `NotDownloaded`, `DownloadHint`, `Download`, `Ready`, `Update`, `UpdateHint`, `Unavailable`,
`UnavailableHint`, `OpenRelease`, `DownloadFailed`, `SessionSection`, `Target`, `TargetPicker`, `TargetWindow`, `TargetGame`,
`NothingToPick`, `Refresh`, `Countdown`, `Scale`, `Starting`, `Stop`, `TitleReady`, `TitleStarting`, `TitleRunning`,
`StatusReady`, `StatusStarting`, `StatusRunning`, `StatusStopped`, `StatusExited`, `StartFailed`, `StopFailed`, `RunAsAdmin`,
`RunAsAdminHint`, `ProblemConflict`, `ProblemHotkey`, `ProblemBypass`, `ProblemTarget`, `HotkeyTaken`, `SettingsHint`,
`PictureSection`, `NrSection`, `OverlaySection`, `More`, `ModelMissing`, `Overlay`, `CaptureSection`, `CaptureSource`,
`CaptureWgc`, `CaptureDxgi`, `Motion`, `MotionFast`, `MotionAmdof`, `HotkeysSection`, `HotkeyHint`, `HotkeyScale`,
`HotkeyStop`, `HotkeyOverlay`, `HotkeyBypass`, `HotkeyBypassHint`, `Credit` (all `Str.NrLive*`); `Str.CreditNrLiveTitle`,
`Str.CreditNrLiveBody`; sheet: `Str.AntiCheatTitle`, `Str.AntiCheatBody`, `Str.AntiCheatUse`, `Str.AntiCheatIgnore`;
settings: `Str.NrLiveSet.<Section>.<key>` for every row of `NrLiveConfig.Settings`, and `.<n>` for each menu entry.

## Tests and gate

- Core: anti-cheat cases (each product, nothing on a clean folder, order, depth limit, executable outside the folder, missing
  folder); placeholder not offered, pinned release offered, shipped payload lists NRLive without offering it; unpack, kept
  settings, re-cut detection, wrapped zip, climbing entry refused; ini defaults/clamp/byte preservation/UTF-16; pending key
  detection; profile compatibility with NRLive's Default.json, command line, problems, hotkey parse; strings parity.
- uishot flow (`Flows.NrLive.cs`, in `uishot <out> flows`): not published, placeholder not offered, pinned offered, download
  unpacks and unlocks, model warning, a setting written to scaleconfig.ini, pending row appears when the exe reads it, hotkey
  conflict holds Scale back, anti-cheat warning on a sheet, Use NRLive picks the game, Ignore saved and remembered.
- `tools\gate.ps1 -Ui` (2026-10-24): build, test (440), line-limit, publish, ui, flows all OK, 0 failed gates.
  Screenshots: `C:\Users\matheus\AppData\Local\Temp\amdnr-gate-8f76cdc74a6d4c6e8fc7c7de2f31cfc3\shots` (temp folder; rerun the
  gate to regenerate). The page: `live-<size>.png` / `live-<size>-end.png` (also `-pt`); the flow: `flow-nrlive-unavailable`,
  `flow-nrlive-offered`, `flow-nrlive-ready`, `flow-nrlive-conflict`, `flow-anticheat-warning`, `flow-nrlive-game-picked`.
- `tools\gate.ps1 -Ui` after the three-column layout (2026-10-24): all gates OK. Screenshots in
  `C:\Users\matheus\AppData\Local\Temp\amdnr-gate-6dd0863a3f0843eba12664274a14a38d\shots` (`flow-nrlive-default.png`,
  `flow-nrlive-narrow.png`, `live-*`).
- Translations of the 20 other languages were written by sub-agents from each language's existing Strings file; worth a native
  read where possible (notes: "Scale" button wording, informal/formal "you" follows each Strings file, Pirate says "Belay" for Stop).
- Not tested against a real NRLive.exe (no published build here): launching, the countdown, elevated stop via the stop key, and
  the global hotkey are untested end to end.
