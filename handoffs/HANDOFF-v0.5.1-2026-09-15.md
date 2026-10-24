# Handoff — v0.5.1: the five defects, the new installer, and what is left before launch

2026-09-15. Two `v0.5.1` branches, one in each repository, **nothing pushed**.

| | |
|---|---|
| Add-on | `D:\dlss5-hotkey` (worktree), branch `v0.5.1`, base `origin/master` (`425ed4d`) — 3 commits, 8 files, +503/−64 |
| Installer | `D:\AMD-NR-ReShade-Installer`, branch `v0.5.1`, base `master` (`46097a9`) — 10 commits, 39 files, +4250/−171 |
| Beta for the tester | `Desktop\AMD-NR-ReShade-Installer-beta-20260915.zip`, 41.4 MB, sha256 `9c6b9b9e4c83ab1e…` |

---

## 0. In one line

The hotkey rebind did not work because of **three** causes combined, not one; Oblivion did not run because of
a fourth, which has nothing to do with the hotkey; and the installer gained a version menu read from GitHub,
pre-download, credits and a layout fix that was hiding information on three screens.
**None of this has been tested in game yet.**

---

# PART I — WHAT WAS DONE

## 1. Add-on (`D:\dlss5-hotkey`, branch `v0.5.1`)

```
497cbc3 Fix the toggle hotkey rebind, and the ini a byte-order mark was hiding
c134a9e Carry an X8R8G8B8 back buffer as its A8 twin, which has a typed UAV
b6e0e4d Record the hotkey, ini and back-buffer format findings in the handoff
```

### 1.1 Rebind cause no. 3 — ReShade zeroes `GetAsyncKeyState`

The missing one, and the one that made the log end at `rebind armed` and nothing more, forever.
`reshade/source/input_windows.cpp`:

```cpp
extern "C" auto WINAPI HookGetAsyncKeyState(int vKey) -> SHORT
{
    if ((vKey & 0xF8) != 0)                       // every key from 8 up
        if (reshade::input::is_blocking_any_keyboard_input())
            return 0;
    ...
}
```

ReShade hooks `GetAsyncKeyState`, `GetKeyState` and `GetKeyboardState` and answers **0 for every
key** while the overlay is blocking the keyboard — which is exactly the time the panel is
open. The scan was reading an empty keyboard: it found no key, did not give up (`overlayAt` was
re-stamped on every frame of the open panel) and did not disarm.

It also explains why it once picked up `VK_HOME`: that was on the first frame after opening the
overlay, before the block took effect. The fix for cause no. 2 (wait for release) closed that window
and the defect became 100% reproducible.

**Fix:** the capture reads `effect_runtime::is_key_down()`, ReShade's own key state,
which comes from window messages and does not go through the hook — it is what its hotkey widget uses. The
runtime pointer is stamped in `OnOverlay32` (the only place where ReShade hands one over) and zeroed
in `OnDestroy`.

### 1.2 Cause no. 4 — the captured key never reached the synchronized copy

It wrote to `g.toggleKey` and that was it. The panel reads `controls.shadow`, so **the button label did not
change**; `SyncControls` only pushes when `shadow.settings_revision` goes up, so the host never
knew; Save belongs to the host, so nothing went to the ini; and the next `GetState`/`SetState` called
`OperationalSettings()`, which copies the shadow over `g` — undoing the new key.

From the user's point of view: "the bind does not change", even the times the key was read.

**Fix:** writes to `controls.shadow.toggleKey/toggleMods`, bumps `settings_revision` and calls
`OperationalSettings()`.

### 1.3 `ToggleKey=35` with the ini saying 120 — UTF-8 BOM

Half-Life 2's `bin\dlss5-neural.ini` starts with **EF BB BF**. `GetPrivateProfileInt` reads the file
as bytes, the first line becomes `<BOM>[dlss5]`, which matches no section: **the whole file
becomes invisible and every value falls back to the default** (35 = `VK_END`). No warning, no log, and the
file looks right in any editor. What puts it there is PowerShell 5.1's `Set-Content -Encoding utf8`
— it was a manual edit during the previous investigation. The installer writes without a BOM.

**Fix:** `src/neural/ini_text.h`, called before reading the ini on both sides. It strips the BOM in
place, via a temporary + `MoveFileEx`, and records it in the log. An installation that already has a BOM fixes itself
when the game is opened.

### 1.4 Cause no. 5 — `B8G8R8X8_UNORM` has no typed UAV (Oblivion)

From the report `amd-nr-report-20260915-014136.zip`:

```
this GPU/driver has no typed UAV store for the back buffer format (DXGI 88, read as 88),
so there is no way to write the corrected image back. Stopping instead of drawing garbage.
```

Measured on this machine (same GPU as the report, RX 9070 XT, driver 32.0.31041), with a D3D12
`CheckFeatureSupport` probe:

| Format | texture2d | uav | typed_load | typed_store |
|---|---|---|---|---|
| `B8G8R8A8_UNORM` (87) | 1 | 1 | 1 | 1 |
| `B8G8R8X8_UNORM` (88) | 1 | **0** | **0** | **0** |
| `R8G8B8A8_UNORM` (28) | 1 | 1 | 1 | 1 |

**Fix:** `D3D9CpuFormat()` now maps `D3DFMT_X8R8G8B8` to `B8G8R8A8_UNORM` — the same
four bytes per pixel with a channel the game does not read. The `X8B8G8R8` pair was already carried as
`R8G8B8A8_UNORM` exactly this way; the BGRA pair was the exception. Only the CPU route changes: on the
shared route the format comes from the D3D9 texture and both sides of `CopyResource` stay the same.
No alpha risk: `compose` writes `float4(v, c.a)` and the destination is an X8 surface.

**Half-Life 2 was going to hit this too** as soon as the bind worked: same X8R8G8B8 back buffer.

### 1.5 What changed in the add-on

```
src/neural/hotkey_capture.h     new    capture state machine, shared by both ends
src/neural/ini_text.h           new    removal of the ini BOM
src/x86bridge/capture_test.cpp  new    capture test, runs in the x86 and x64 builds
src/x86bridge/frontend32.cpp           capture in OnPresent with is_key_down, shadow, format
src/x86bridge/overlay32.inc            the button only arms; the runtime stamps
src/neural/neural.cpp                  same capture in the 64-bit add-on, and the BOM
build-x86bridge.ps1                    builds and runs capture_test on both architectures
```

The 64-bit add-on had the **same** causes no. 2 and no. 3, exposed (there is no bridge there). Now both
ends use the same header.

**Build:** `.\build-x86bridge.ps1` passes — x86 and x64, protocol, IPC and capture tests, PE,
imports and the integrated addon64. The new pair is already installed in
`D:\SteamLibrary\steamapps\common\Half-Life 2\bin\` (the old ones became `.bak`).

---

## 2. Installer (`D:\AMD-NR-ReShade-Installer`, branch `v0.5.1`)

```
7f19788 Add the first-run setup, emulator routes, install logs and problem reports   <- previous session
5e14701 Tell a remaster from the game it remade when reading the API database
667bb15 Offer a menu of add-on versions read from the GitHub releases
789d1fe Add a button that downloads every payload into the cache up front
04af8cc Never let a record for another build replace what the executable says
82792ab Show what is in the cache, what it would cost, and how far a download has got
02b8fcc Credit the network, name the repositories, and say that nobody should be paying for this
c0d7555 Give the credits their own panel: the logos are the links, and each one says what it is
67a360e Let the setup window resize, keep both windows inside the screen, and lead with the Discord
c20f311 Fix the page gutter that made wrapping content overflow and cut the scroll short
```

The first commit is what was already in the working tree from the previous session; I split it by hunk so as
not to mix things up, but it goes into the same branch and the same review.

### 2.1 Version menu, read from GitHub

`src/AmdNr.Core/Releases.cs` (new). One call to the releases API of `zmodelerlover/dlss5-neural-amd`
per launch, cached in `%AppData%\AmdNrInstaller\cache\releases.json` (offline uses the last
list). The download path still does not touch the API: the files come from asset addresses.

A version is only offered when it publishes **what pins it**: `SHA256SUMS.txt` plus the files the
route installs. Size comes from the asset record in the API, hash from `SHA256SUMS.txt` — so a version
discovered today is pinned just as tightly as one written into `payload.json` months ago.

Checked live against GitHub: it lists v0.5.0, and the hash it takes from `SHA256SUMS.txt`
(`203f0278b64c…`) matches byte for byte what `payload.json` was already pinning.

**v0.5.0 published the 32-bit pair only inside the zip**, so for the x86 route it comes from the manifest. From
v0.5.1 onward, uploading the four loose, the menu grows on its own (see §5.4).

### 2.2 Oblivion — two defects, not one

The tester's report said:

```
apis          DX12
source        PCGamingWiki
why           PCGamingWiki (The Elder Scrolls IV: Oblivion Remastered): DX12.
platform      Gog
```

`NormaliseTitle` stripped "Remastered", so "Oblivion" and "Oblivion Remastered" became the same
key and the title index kept whichever appeared first — D3D12, 64-bit. A GOG copy has no
Steam appid, so it fell through to the title.

I scanned the `api-db.json` we distribute for titles that collide with **disagreeing**
records:

```
before: 9 ambiguous titles
  Age of Empires III | : Definitive Edition          Mafia | Mafia: Definitive Edition
  BioShock | BioShock Remastered                     Mafia II | Mafia II: Definitive Edition
  BioShock 2 | BioShock 2 Remastered                 Mass Effect | Mass Effect Legendary Edition
  Oblivion | Oblivion Remastered                     Sleeping Dogs | : Definitive Edition
  Skyrim | Skyrim Special Edition

after:  6 (the three "Remastered" left the collision)
```

It was not one game, it was nine — Oblivion was just the one someone installed. Three layers now:

1. a remaster is **not** an edition (it left the qualifiers regex);
2. a title that two disagreeing records share **answers nothing** — it falls through to the exe reading, which
   was measured on disk (the 6 that remain fall here);
3. even with a record in hand, if it describes an architecture the exe is not (x64-only against a
   32-bit exe), it **does not replace** the local reading, and the "Detected" line says why.

### 2.3 Pre-download and the files card

"Download everything now", in the This machine tab: checks the hash of everything off the UI thread; if everything is already
there, **it says it already was** instead of looking like it did nothing; if something is missing, it downloads only what is missing.
A progress bar **for the whole set** (bytes downloaded / total), not one per file going from 0 to 100
six times. A summary line ("5 of 5 components ready — 161 MB in the cache") and the size of each
component on its own line.

The cache check moved off the UI thread: entering that tab froze the window for ~1s while it
hashed the 141 MB of weights.

### 2.4 Credits, links and the notice

`src/AmdNr.App/CreditsPanel.axaml` (new control, used on the **first screen** and in
**Settings** — a single definition):

1. **Discord** at the very top, its logo in blue #5865F2;
2. a notice strip: *"This is free. If you paid for this, you are being scammed"* + *"It is open source
   and will stay that way: read it, modify it, build your own version… the only thing we ask is
   that nobody turns it into a paid version"*;
3. **Credits**: danielblnc's DLSS-NR-on-AMD (with the thank-you), then the add-on (which is where this
   installer also lives);
4. **Support · optional, and buys nothing**: Ko-fi and Vakinha side by side.

Each whole row is the link, so the logo is what gets clicked. The six URLs come from `config.json`
(`Addon`, `App`, `DiscordUrl`, `RuntimeUrl`, `KofiUrl`, `VakinhaUrl`) — a rotated invite or a fork
do not require a new build.

> The open source text is written as **a request from the project**, not as a clause: MIT
> technically allows selling. If you want it to be a real prohibition, that means changing the license of both
> repos — and daniel's runtime has its own terms.

### 2.5 The layout fix (the thing that was hiding information)

I put together a headless renderer that opens the window without opening a window and dumps the visual tree with the
measurements. It showed:

```
StackPanel#PageLanguage   w=764      <- the page
  Border.card             w=764      <- the language card, correct
  CreditsPanel            w=819      <- 55px wider than its parent
```

**`Padding` on a `ScrollViewer` is subtracted in arrange but not in measure.** Content that wraps
was measured against the full width: it came out too wide *and*, having wrapped into fewer lines
than it needs, **too short** — the page reported 688px when it needed 773. Hence both
symptoms together: text overflowing to the right and **the scroll stopping before the end**.

Fix: the margin goes on the content, not padding on the ScrollViewer — on the three pages that scroll.
Also: scrollbar always visible (`AllowAutoHide=False`), the wizard window resizable
and 820×900, and `WindowFit.ToScreen` on both windows, which measures the work area **of the screen the window
opened on**, respects its DPI, shrinks only if it does not fit, and recenters.

Measured afterwards: 698px of content against 740 of work area — **fits entirely without scrolling** at 820×900; at
700×640 it scrolls, and now it scrolls all the way to the end.

### 2.6 Tool that stayed

`tools\uishot` — the headless renderer, now in the repository:

```powershell
dotnet run --project tools\uishot -- <output folder>
```

It generates PNGs of the screens and a `.tree.txt` with the position and size of every control. It was the tree, not the
screenshot, that found the bug in §2.5.

### 2.7 Tests

139 in the installer (`dotnet test tests\AmdNr.Core.Tests\AmdNr.Core.Tests.csproj`), including 5 new
ones for release/version and 3 for the Oblivion case. In the add-on, protocol + IPC + capture on both architectures.

---

# PART II — WHAT IS LEFT

## 3. Before launch (blocks v0.5.1)

| # | What | Why |
|---|---|---|
| 1 | **Test the bind in Half-Life 2** | It is the reason for the whole session and none of it has been seen running. Script in §4.1 |
| 2 | **Test Oblivion with the new format** | The fix in §1.4 is measured, not observed. The tester from the report is the one who closes this |
| 3 | **Check Save** | `settings saved to dlss5-neural.ini` has never been seen in any session. It may be a sixth defect |
| 4 | **`/code-review ultra` on both branches** | Your command, requested. `master` as base in the installer, `origin/master` in the add-on (§4.2) |
| 5 | **Publish the v0.5.1 release with the 4 loose assets** | Without that the version menu does not grow on its own (§5.4) |
| 6 | **Decide on merging the repositories** | The credits already say the installer lives in `dlss5-neural-amd`. If that is the case, `config.App` (update check) and the README need to follow |
| 7 | **Talk to danielblnc** | Pending from the previous session: it is his runtime, distributed through our Hugging Face, and his project's README still says these files "will never be in a repo" |

## 4. Recipes

### 4.1 The bind test (HL2, 10 minutes)

The new pair is already installed. Open the game, open the overlay (Home), click the hotkey button,
**release everything**, press F9. Expected in `bin\dlss5-neural-x86.log`:

```
x86bridge: removed a UTF-8 byte-order mark from dlss5-neural.ini; ...
x86bridge native x86 ... ToggleKey=120 ToggleMods=1      <- 120, not 35
x86bridge: rebind armed (overlay stamp NN ms old)
x86bridge: toggle bound to key NNN mods N
```

Then close the overlay and press the new combination: `x86bridge enabled=1`, and now **the image has
to change** (this was where the format from §1.4 stopped everything). To go back to normal: `%LOCALAPPDATA%\Temp\
hl2-bin-backup` has the official v0.5.0 pair, and each file has a `.bak` next to it.

**Do not test with F10**: on Windows it generates `WM_SYSKEYDOWN`. Use F9 (120) or Insert (45).

### 4.2 The review

`/ultrareview` looks at the repository of the folder where the session was opened, and does not find a base on its own in these
two. Open a session inside each one:

```
cd /d D:\AMD-NR-ReShade-Installer && claude     ->  /code-review ultra master
cd /d D:\dlss5-hotkey && claude                 ->  /code-review ultra origin/master
```

In the add-on it has to be `origin/master`: the **local** `master` is at `0a1fd9a`, 48 commits behind, and
is checked out in the other worktree (`D:\dlss5`). Wrong base = review of old code.

### 4.3 Builds

```powershell
# add-on: x86 + x64, protocol, IPC, capture, PE, imports, addon64
D:\dlss5-hotkey\build-x86bridge.ps1

# installer
dotnet test tests\AmdNr.Core.Tests\AmdNr.Core.Tests.csproj
dotnet publish src\AmdNr.App\AmdNr.App.csproj -c Release -r win-x64 -o dist
Copy-Item payload\api-db.json dist\api-db.json -Force     # publish does not include this one

# view the UI without opening a window
dotnet run --project tools\uishot -- <folder>
```

The exe stays **locked while the app is open** — publish fails with
`UnauthorizedAccessException` and it is nothing else.

### 4.4 Running the app from scratch, without your settings

```powershell
$env:AMDNR_HOME = "$env:TEMP\amdnr-limpo"
& "D:\AMD-NR-ReShade-Installer\dist\AMD-NR-ReShade-Installer.exe"
```

Empty folder = first-run wizard, no games, empty cache. `%AppData%\AmdNrInstaller`
is not touched.

## 5. Roadmap

### 5.1 Now (v0.5.1)

The seven items in §3. The beta zip is already ready to circulate in the meantime.

### 5.2 Right after

- **Wait for release and `is_key_down` in the 64-bit add-on — check in game.** The code is there, but
  only the 32-bit route was exercised in this session.
- **D3D9 shared route.** §1.4 fixes the CPU route. If a D3D9Ex game lands on the
  shared route with an X8 back buffer, the host will stop for the same reason and the remedy is different: force the
  CPU route when the shared format has no typed UAV. Not measured, not implemented.
- **ReShade on the Vulkan route** is still manual: there it is a global layer registered in
  `HKCU\SOFTWARE\Khronos\Vulkan\ImplicitLayers` + `ReShadeApps.ini`, not a DLL in the folder.
- **The other 12 languages.** English and pt-BR are complete; the mechanism is already in place.
- **api-db coverage**: run the generator with `--steam-top 3000`. The 6 ambiguous titles that remain
  answer by appid (Steam) and by exe (the rest) — a GOG copy of Mafia today gets no answer from the
  database, and that is on purpose.

### 5.3 Ideas, not commitments

- Optional `StartOn=1` in the interface — today the add-on starts off on purpose.
- The version menu keeps the choice **per app session**, not per game. If that bothers anyone, store it
  in `games.json`.
- A second "offline" zip with the payloads already in the cache, for testers with bad internet
  (`tools\seed-cache.ps1` already does the hard part).

### 5.4 The v0.5.1 release recipe

The version menu only offers a release that publishes **everything loose, next to the zip**:

```powershell
gh release upload v0.5.1 `
  build\dlss5-neural.addon64 `
  build-x86bridge\dlss5-neural.addon32 `
  build-x86bridge\dlss5-neural-host64.exe `
  release\payload.sha256 `
  SHA256SUMS.txt
```

Without `SHA256SUMS.txt` the version does not appear — nothing pins it, and the app does not install what it cannot
check. It is documented in `payload/README.md`, section "The version menu".

---

## 6. Traps paid for in this session

- **Never read keys with `GetAsyncKeyState` inside a ReShade add-on** when the overlay may
  be open. Use `effect_runtime::is_key_down`. This applies to any add-on.
- **A BOM in an ini erases the whole file** as far as the Windows profile API is concerned. PowerShell 5.1's `Set-Content -Encoding
  utf8` and `Out-File -Encoding utf8` add a BOM.
- **`B8G8R8X8_UNORM` is not a UAV format.** If something needs to write to the back buffer, the X8 pair has
  to become A8.
- **`Padding` on a `ScrollViewer` does not count in measure.** Content that wraps comes out too wide and
  too short, and scrolling stops before the end. Put the margin on the content.
- **Avalonia's name generator creates a field per `x:Name`**: a method with the same name as a
  control is `error CS0102`. (`VersionNote` became `VersionNoteText`.)
- **A self-contained project can only be referenced by another self-contained one** — that is what blocked
  `tools\uishot` until `win-x64` was pinned in it.
- **An XML comment cannot contain `--`.** XAML rejects the whole file.
- **`Start-Process -Environment` does not exist in PowerShell 5.1.** Setting `$env:X` in the same call
  works: the child inherits it.
- **`vswhere` did not find Visual Studio Insiders** on this machine; `build-x86bridge.ps1` has
  its own fallback, a new script needs the same.
- **`\x` in a non-raw Python string** becomes a different character (again).
- **Both repositories' files are CRLF.** A patch with an LF pattern does not match and fails silently.

---

## 7. Where everything is

```
D:\dlss5-hotkey                      add-on, branch v0.5.1 (worktree; NOT D:\dlss5)
D:\AMD-NR-ReShade-Installer          installer, branch v0.5.1
  dist\                              published build (outside git)
  tools\uishot\                      headless UI renderer
  payload\README.md                  manifest, publishing and the release contract
Desktop\AMD-NR-ReShade-Installer-beta-20260915.zip    what to send to the tester
Desktop\HANDOFF-x86-hotkey-2026-09-15.md              the fine detail of the five causes
%AppData%\AmdNrInstaller\            the app's cache, logs, games.json, settings.json
%LOCALAPPDATA%\Temp\hl2-bin-backup\  the official v0.5.0 pair, to roll back HL2
```
