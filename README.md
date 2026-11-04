# OpenNR Installer

A Windows app that sets up OpenNR, NVIDIA's DLSS 5 neural rendering (NR) and DLSS Ray Reconstruction on AMD Radeon
cards, in your games. It finds your games, works out which renderer each one uses, downloads and checks every file,
installs it, and takes it all back out again.

Discord: <https://discord.gg/wYhvS3JSHM>

Ko-fi: <https://ko-fi.com/T6T213OVFE>

## Download

Get `OpenNR-Installer.exe` from [Releases](https://github.com/zmodelerlover/OpenNR-Installer/releases). It's one file
and needs nothing else installed. Releases also carry the same exe as `AMD-NR-ReShade-Installer.exe`, the old name,
so older copies can still update themselves, and a `.7z` with the exe and its lists.

## Supported GPUs

| | Radeon cards |
|---|---|
| danielblnc runtime | RX 6000, RX 7000, RX 9000 with HIP 7 (RX 6000 needs AMD's HIP SDK 7.2) |
| lmxxf runtime | RX 9070 and RX 9060 series |
| mochizuki runtime | RX 9000; RX 7000, RDNA3 APUs and RX 6000 on experimental networks |
| OpenRR (Ray Reconstruction) | RX 9000 |

The first-run wizard checks your card and driver, and the **This machine** page keeps showing them.

## How it works

1. **Scan** reads Steam, Epic, GOG, EA, Ubisoft, Battle.net and Xbox, or add a folder or an emulator by hand.
2. Open a game and pick a route:
   - **OptiScaler** (recommended): the [OpenNR OptiScaler fork](https://github.com/MatheusFerreiraS/OpenNR-OptiScaler)
     runs NR inside the game's DLSS, FSR or XeSS, with the game's own depth and motion vectors, or on the finished
     frame when there is no upscaler. D3D12, D3D11, D3D10, Vulkan, OpenGL and D3D9, 64-bit and 32-bit.
   - **ReShade + add-on**: the [OpenNR ReShade add-on](https://github.com/zmodelerlover/OpenNR-ReShade), for
     emulators, D3D8 and whatever the OptiScaler route doesn't take. The installer adds ReShade with full add-on
     support.
3. Pick which NR runtimes go in that game (danielblnc, lmxxf, mochizuki). Unticked ones are not downloaded.
4. Install. In game, press **Insert** for OptiScaler (Neural tab) or **Home** for ReShade (Add-ons tab).

## Features

- **Per-game runtime picker** and a version menu for the OptiScaler fork and the add-on, remembered per game.
- **OpenRR card** (RX 9000, OptiScaler route): our own DLSS Ray Reconstruction runtime for D3D12 and Vulkan games. It
  needs `nvngx_dlssd.dll` 310.9.1; when the game has another version, the installer supplies 310.9.1 and keeps the
  game's own in the backup.
- **WrongGPU** (RX 9000, off unless ticked): phantoslayer21's DLSS 4.5 super resolution (Model M) as OptiScaler's
  upscaler. Its model is converted on your PC.
- **NRLive tab**: Caliel's [NRLive](https://github.com/Caliel666/NRLive) plays a game scaled in a window of its own,
  with FSR, frame generation and NR on top, without touching the game's files. Meant for games with anti-cheat.
- **REFramework** (praydog, MIT) for RE Engine games: an optional card per game, off by default and off where online play makes injection risky.
- **Last session**: whether NR ran the last time you played, read from the logs, and what went wrong if not.
- **NR settings** export and import per game.
- **Report a problem**: packs the logs and the folder listing into one zip to share. Nothing is sent anywhere.
- Every file is pinned by SHA-256 and checked before it goes into a game folder. Everything replaced is backed up,
  and uninstall puts it back.
- 20 languages, plus Pirate English.

Files are downloaded from the [zmodelerlover/amd-nr](https://huggingface.co/datasets/zmodelerlover/amd-nr) dataset on
Hugging Face, plus GitHub releases. **Import files...** takes files you downloaded yourself.

FiveM has its own route. It has its own anti-cheat, so check your server's rules first; you use it at your own risk.

## Building

```powershell
dotnet test
powershell -ExecutionPolicy Bypass -File tools\gate.ps1 -Ui
```

.NET 10 SDK, Windows. `tools\gate.ps1` runs the build, the tests and a line limit per file; `-Ui` also renders every
page headless against a throwaway `AMDNR_HOME`.

## Credits

- [DLSS-NR-on-AMD](https://github.com/danielblnc/DLSS-NR-on-AMD) by danielblnc: the closed-source runtime and its
  weights. Not reimplemented or kept in this repository.
- [lmxxf/dlss5-on-amd-9070xt-porting](https://github.com/lmxxf/dlss5-on-amd-9070xt-porting) (MIT) and
  [mochizuki0323/DLSSNR-AMD](https://gitlab.com/mochizuki0323/DLSSNR-AMD) (MIT): the open-source runtimes, as built
  by the OpenNR OptiScaler fork (GPL-3.0). Their licences go into the game folder with them.
- [NRLive](https://github.com/Caliel666/NRLive) by Caliel (GPL-3.0), downloaded on demand.
- WrongGPU by phantoslayer21, closed source, installed with the author's written permission.
- Person protection: [ONNX Runtime](https://github.com/microsoft/onnxruntime) (MIT), YuNet (MIT) and PP-HumanSeg
  (Apache-2.0) from the [OpenCV Zoo](https://github.com/opencv/opencv_zoo), and
  [Ultralytics YOLO11n-seg](https://github.com/ultralytics/ultralytics) (AGPL-3.0). Licences in `person-model\`.
- The install engine is ported from the Rust installer in
  [dlss5-neural-amd](https://github.com/zmodelerlover/dlss5-neural-amd) (MIT).
- The app's shape follows [Optiscaler-Client](https://github.com/Optiscaler-Client/Optiscaler-Client) (GPL-3.0). None
  of its code is used here.

None of NVIDIA's model weights are in this repository. The NR weights come from NVIDIA's models and are downloaded or
made on your PC.

## License

MIT, see `LICENSE`.
