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

## Visual round (2026-10-24)

The user's list of visual fixes, all in one commit.

- **More on the cards** (`NrLive\CardBody.cs`, new): each card's body is a panel of its rows plus its More expander. It asks
  for the rows and a closed More whether More is open or not, so opening one no longer makes the row of cards taller.
  Open, More takes the room under the rows and its content scrolls inside it (`ScrollViewer.thin`); it is measured again
  in that room, or its scroller would not scroll. When the rows leave less than `MinRoom` (160 px, header included; the
  tallest card has none), More rises over the last rows instead of showing a bare header. Two or one across, a card alone
  on its line (`Grows`) just grows, since the page scrolls there anyway. Three across, the cards' row is `*` and
  `Fill()` (on `PageScroll.LayoutUpdated`) gives the grid the window's remaining height as `MinHeight`, so the cards reach
  the foot of the window and More gets that room too; the page still fits 1240x800.
- **More order**: `ShowSettings` sorts each card's More by kind, sliders, then menus, then switches, table order within a kind.
- **Thin scroll bar** (`Styles/Details.axaml`, `ScrollViewer.thin`): the vertical bar re-templated to a track, two
  transparent page buttons and a 4 px thumb in a 10 px lane, `AllowAutoHide` off; it never widens under the pointer.
  Used on the NRLive page's `PageScroll` and inside each More.
- **The well** (`Styles/Palette.axaml`): new brush `Well` = #110C0D. The app has only the dark theme (`App.axaml`
  RequestedThemeVariant="Dark", one palette), so there is no light counterpart. Fluent's keys overridden there, app-wide:
  `ExpanderHeaderBackgroundPointerOver/Pressed` = Well, `ExpanderChevronBackground*` transparent (the grey square behind
  the chevron), `ComboBoxDropDownBackground` = Well. That covers the `details` expanders and the NRLive `nrMore` ones,
  and every ComboBox drop-down. Measured on the shots: header and chevron #110C0D under the pointer, popup #110C0D.
- **Hotkey boxes**: `TextBox.hotkey` (Details.axaml): Well background, the cards' `Border` brush, corner radius 8;
  `BorderStrong` under the pointer, the accent border on focus as before.
- **Game sheet**: "Last session" (`SessionBox`, now a grid so its box stretches) and "Detected" sit in one grid
  (`InfoRow`), two equal columns; "Installs into" moved inside the Detected box as its third line, so the last session and
  the install location are side by side. `LayInfo()` (GameSheet.Session.cs) stacks them when the row is under 620 px
  (the window's sheet is ~600 px wide, so it stacks there, as before) and gives Detected the whole row with no session.
  Detection's callouts (elsewhere, API hint) span both columns under them; the anti-cheat card moved under this row,
  inside `Options`.
- **uishot**: `Flows.NrLiveMore.cs` (in the NRLive flow, 1240x800, the stand-in also reading NR's advanced keys): every
  More open, cards keep their height, page fits, sliders before switches, NR's More scrolls, the bar is <= 10 px also
  under the pointer; shots `flow-nrlive-more-open`, `flow-nrlive-more-hover`, `flow-nrlive-dropdown` (popup on the
  overlay layer so the window shot carries it). `Flows.Session.cs`: stacked in the window's sheet, side by side and equal
  height docked at 1920x1040 (`flow-session-docked`).

## Visual round 2 (2026-10-24)

The user's second list (screenshots `handoff/user-round2-*.png`), one commit. It replaces parts of the round above.

- **Hover colours** (`Styles/Palette.axaml`): `Well` is #110C0C now, and a new `WellEdge` = #454141. Fluent's dark
  theme paints #99000000 / #CCFFFFFF under the pointer (#0A0908 / #D0CFCF on a card) on `ComboBox`, `TextBox`
  (`TextControl*`, so the NumericUpDown/AutoCompleteBox family too) and the date/time pickers; found by dumping the
  theme's resources from a throwaway probe. Overridden app-wide: `ComboBoxBackgroundPointerOver/Pressed`,
  `TextControlBackgroundPointerOver`, the three pickers' = Well; their `BorderBrushPointerOver` (and the combo's
  Pressed) = WellEdge; `ExpanderHeaderBorderBrushPointerOver/Pressed` = WellEdge, so a More header is the same pair.
  The hotkey boxes' hover border is WellEdge too. Not changed: the search box (its own SurfaceHigh hover), and the
  switch/check/radio strokes that are near white under the pointer (lines, not a background).
- **Cards** (`NrLive/CardBody.cs` rewritten, `Arrange`/`Fill` in NrLivePage.axaml.cs): the cards are top-aligned in an
  `Auto` row; each body knows the bodies on its line (`Peers`: all three, the first two, or itself) and asks for the
  tallest one's `Closed` height (rows + gap + a closed More). So closed, the three are as tall as the tallest one's
  rows, no taller, and each More sits 12 px under its rows (no more foot docking, no overlap, no `MinHeight` to the
  window). An open More makes its own card taller: up to `Room` (three across, set by `Fill` = from the body's top to
  the window's foot less the card's padding), then its content scrolls inside. When the window leaves less than
  `MinMore` (80 px of content) under a card, it takes that much anyway and the page scrolls: at 1240x800 the cards
  already reach nearly to the foot, so there each open More does that. On 2/1 across a card just grows (page scrolls).
- **Scroll bars** (`Styles/Details.axaml`): the thin bar is global now, vertical and horizontal, every ScrollViewer with
  auto-hide off. Fluent's template then gives the bar its own column only while it shows: a 10 px lane with the 4 px
  thumb 2 px from the far edge. `Shared/ScrollGutter.cs` (attached `ScrollGutter.Size`): a scroller that reaches the
  edge of its box keeps that much right margin on its content while there is no bar and gives it back when the bar
  shows, so the content runs up to the bar. On each More (`nrMore` content padding 14,10,0,10, Size 14), the game
  sheet's `ReportScroll` and the notes sheet.
- **HUD tiles' switches**: `Store` re-laid every row after any [FPS] switch (a HUD item makes the preset custom), so the
  switch being clicked was replaced by a new one mid-slide; plain switches never hit that. Now only the runtime and the
  preset re-lay; a HUD item updates the preset menu in place (`ShowPreset`, `_quiet` keeps it from writing back).
- **Game sheet**: the verdict's banner/steps sit straight in the report stack (an empty sub-stack still cost a spacing),
  the report starts 12 px under the header instead of 18, "Last session" and "Detected" are the first line inside their
  own boxes (`SessionBox.axaml`, `DetectedPart` is the `Border` now, the Choose button centred over both rows), 12 px
  between them when stacked.
- **Gate**: `tools/gate.ps1` lists a failed gate's `FAIL` lines before its last 30, which hid the flow's failures.
- **uishot** (`Flows.NrLiveMore.cs` rewritten): closed equal heights = tallest `Closed`, each More right under its rows,
  page fits (`flow-nrlive-closed`); each More opened alone grows only its card, to the foot or by `MinMore`, the bar at
  the box's edge with the rows up to its lane or the padding kept without a bar (`flow-nrlive-more-picture/-nr/-overlay`);
  More header hover pixel = Well (`flow-nrlive-more-hover`); at 1600x1000 the cards leave the room under them free and
  NR's More takes its card alone to the foot, scrolling inside (`flow-nrlive-tall-closed`, `flow-nrlive-tall-nr`); a
  menu under the pointer reads #110C0C inside and #454141 at its edge (`flow-nrlive-combo-hover`); a HUD tile switched
  keeps its switch and the preset menu says Custom (`flow-nrlive-tiles`). `tools\gate.ps1 -Ui`: all gates OK; shots in
  `C:\Users\matheus\AppData\Local\Temp\amdnr-gate-3ef418d08e684ca5b0e07fe05295986f\shots` (also `flow-session-ran`,
  `flow-session-docked` for the sheet top).
- **Not checked**: the switch animation itself (headless shows end states only; the cause is gone, the user's eye is the
  test), and a real window's hover colours outside the shots taken.

## Visual round 3 (2026-10-24)

The user's third list (screenshots `handoff/user-round3-*.png`), one commit.

- **List view's scroll bar** (`Styles/Details.axaml`): the game list is a `ListBox`, whose own scroller takes
  `AllowAutoHide` from the ListBox by a template binding, which outranks the global `ScrollViewer` style; so it stayed
  auto-hide and its bar lay over the rows (the open row's end and status dot). `ListBox, ComboBox, TextBox` now set
  `ScrollViewer.AllowAutoHide` False themselves, so their bars get the 10 px lane like every other scroller and the rows
  end where it starts. The plain ScrollViewers (grid, pages, sheet, More, notes) already had the lane.
- **Route cards** (`Sheet/GameSheet.axaml`): name, (i), Recommended and Installed in one `WrapPanel`; the chips sit on the
  name's line and wrap under it only when the card is too narrow (the window's sheet with both chips wraps Installed).
- **No captions** on the last session and Detected boxes: `Str.SessionSection` and `Str.DetectedSection` removed from all
  21 languages; the session's (i) follows its title ("NR ran in the last session"), Detected's follows the executable's
  line (`SessionBox.axaml`, `DetectedPart`).
- **Runtime tiles** (`Styles/Routes.axaml`): Fluent's CheckBox template is 32 px tall whatever MinHeight says, so the
  tile's CheckBox has a -4 px vertical margin and the tile padding 12,6: 38 px instead of 50, the 20 px box unchanged.
- **uishot**: list view scrolled to its last row, the row's right edge at or before the bar's lane (`flow-list-scrolled`);
  `Flows.Lines.cs` (`SheetLines`): docked, each route's chips on its name's line and each box opening on its own line
  with the (i) after it, with OptiScaler's Recommended shown beside Installed for the picture (`flow-route-chips-docked`,
  `flow-route-chips` in the window's sheet); runtime tiles <= 40 px with a 20 px box (`flow-2-optiscaler-mochizuki`).
  `tools\gate.ps1 -Ui`: all gates OK, shots in
  `C:\Users\matheus\AppData\Local\Temp\amdnr-gate-1414cd0205ca4b529e215b7e8894ac0d\shots`.

## Files

New:
- `src/AmdNr.Core/Library/AntiCheat.cs`, `src/AmdNr.Core/NrLive/NrLivePackage.cs`, `NrLiveConfig.cs`, `NrLiveProfile.cs`,
  `NrLiveProfiles.cs`, `src/AmdNr.Core/Payload/PayloadManifest.NrLive.cs`
- `src/AmdNr.App/NrLive/NrLivePage.axaml(.cs)`, `NrLivePage.Session.cs`, `NrLivePage.Settings.cs`, `NrLivePage.Rows.cs`,
  `NrLivePage.Profiles.cs`, `NrLiveNative.cs` (window list, `GlobalHotkey` on its own thread, SendInput, the window in
  front and a process's executable)
- `tests/AmdNr.Core.Tests/NrLiveProfilesTests.cs`, `tools/uishot/Flows.NrLiveProfiles.cs`, `src/AmdNr.App/NrLive/CardBody.cs`,
  `src/AmdNr.App/Shared/ScrollGutter.cs`, `tools/uishot/Flows.NrLiveMore.cs`
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
