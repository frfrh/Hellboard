# Hellboard Companion – Implementation Plan

| | |
|-|-|
| Status | Draft, revision 2 |
| Date | 2026-10-15 |
| Location | `Software/` (all code, assets and docs of this program live here) |

## 1. Goal

A Windows desktop program that

1. finds the monitor on which **Helldivers 2** is running (multi-monitor setups),
2. watches the game's **loadout screen** (mission preparation before deployment),
3. identifies the **stratagems the local player has selected**,
4. **locks in** the loadout once the player has confirmed it, and
5. **transfers** the loadout to the **Hellboard**, so that its stratagem buttons are assigned automatically.

The program is a read-only observer of the screen. It does not read or modify game memory, inject DLLs, hook the game process or send any input to the game.

## 2. Scope

### In scope

- Game window / monitor detection, including window moves between monitors and resolution changes.
- Screen capture of the game monitor with low overhead.
- Recognition of the loadout screen, the local player's stratagem slots and the confirmation state.
- Classification of each slot against a library of stratagem icon templates.
- Loadout lock-in logic that also works when the time between confirmation and deployment is very short.
- PC side of the Hellboard transfer: protocol definition, transport abstraction, dry-run and simulator.
- **Extension of the Hellboard firmware** (`Firmware/`) for receiving and using the loadout – see §2.1.
- **Updating the Hellboard's Python files from the PC software** (§6.10).
- Loadout history log, tray UI with status and debug view, template capture tool.
- Tests with recorded screenshots.

### Out of scope (for now)

- Changes in `Hardware/`, `PCB/` or root files (only after asking).
- Recognition outside the loadout screen (in-mission HUD, etc.).
- Sending input to the game.

### 2.1 Hellboard firmware (binding rule)

**The Hellboard firmware (`Firmware/`) is extended as part of this project.** It is not a later add-on and not a fixed constraint:

1. **Not limited by the current firmware.** The PC software is designed for the best overall solution. The current firmware (hardcoded `main.py`, `Macros` table in `macro.py`, HID-only) is the starting point, not a boundary. Its structure, data model, protocol and behavior may be changed or rewritten where that leads to a better solution.
2. **Decisions that require firmware changes are discussed with the user first.** Every such decision is recorded in the firmware decision log (§6.9) with options and a recommendation, and is only implemented after it has been agreed. No code in `Firmware/` is changed without this agreement.
3. **PC software and firmware are developed together.** The protocol is specified jointly (`Software/doc/spec/hellboard-protocol.md`); the firmware work runs alongside the PC-side Hellboard link (phase P5/P7), not after the PC software is finished.
4. Until the firmware side exists, the PC side is tested with dry-run and simulator (§6.6).

## 3. Technology Decision

| Concern | Choice | Reason |
|-|-|-|
| Language / runtime | **C# / .NET 10** (LTS, supported until Nov 2028) | First-class Win32 + WinRT interop, runs on Linux as well, single-file self-contained publish, good performance. |
| UI | **Avalonia UI 12** (incl. its built-in `TrayIcon`) | WPF-like XAML, MVVM (`CommunityToolkit.Mvvm`), compiled bindings; cross-platform, so the UI does not block a later Linux port (§3.1). |
| Screen capture | **DXGI Desktop Duplication** via `Vortice.Direct3D11` / `Vortice.DXGI` (MIT); Windows.Graphics.Capture only if a need arises (e.g. capturing an occluded window) | Works for borderless/fullscreen games without a capture border on all Windows versions, no WinRT interop; the device is created on the adapter that owns the monitor. Verified 2026-10-15 with Helldivers 2 (borderless, AMD RX 6800 XT): ~65 frames/s available, readback ~7 ms. |
| Image processing | **OpenCvSharp4** (runtime package per platform: `OpenCvSharp4.runtime.win`, later `…runtime.linux-x64` / Ubuntu) | Template matching, color conversion. |
| Hellboard transport | `System.IO.Ports` (USB CDC serial) | MicroPython on the RP2040 already exposes a USB CDC serial port next to HID. |
| Config | JSON (`System.Text.Json`) + JSON schema | Human editable, validated on load. |
| SVG rendering | **Svg.Skia** (SkiaSharp, cross-platform) | Renders the stratagem SVG icons into matching templates (§6.11). |
| Logging | `Microsoft.Extensions.Logging` + Serilog file sink | Rolling logs in the per-user data directory (Windows `%LOCALAPPDATA%\HellboardCompanion\logs`, Linux `~/.local/share/HellboardCompanion/logs`). |
| Tests | **xUnit v3** on Microsoft.Testing.Platform (`global.json` → `"test": {"runner": "Microsoft.Testing.Platform"}`, required by the .NET 10 SDK) + **AwesomeAssertions** (Apache-2.0 fork of FluentAssertions) | Offline tests on recorded screenshots. FluentAssertions ≥ 8 is not used because of its commercial license. |

**Alternative considered:** Python (mss + OpenCV + pywin32, PyInstaller). Consistent with the MicroPython firmware, but weaker WGC support and more fragile Win32 interop. Rejected for the main program; Python may still be used for offline tooling.

### 3.1 Platform: Windows target, portable design

**Decision:** Windows is the only target. A later **Linux port** (game via Steam/Proton) is **not a goal, but must be made as easy as possible**. Rule: everything that is not inherently platform-specific is written platform-neutral; platform-specific code is isolated behind interfaces in a separate project.

Portability of the parts:

| Part | Windows (implemented) | Linux (possible later port) |
|-|-|-|
| Core logic (layout, recognition, tracker, protocol, config, history) | ✔ | ✔ unchanged – .NET 10 and OpenCvSharp run on Linux |
| UI (Avalonia) incl. tray icon | ✔ | ✔ unchanged (tray depends on the desktop environment) |
| Hellboard serial (`System.IO.Ports`) | ✔ `COMx` | ✔ `/dev/ttyACM0` (user needs `dialout` group) |
| Serial port discovery by USB VID/PID | SetupAPI / WMI | `/sys/class/tty/*/device/../idVendor` |
| Game window / monitor detection (§6.1) | Win32 | X11: `_NET_WM_PID` + XRandR. Wayland: not possible for other apps' windows → user selects the monitor once |
| Screen capture (§6.2) | DXGI Desktop Duplication | X11: XShm / XComposite. Wayland: `xdg-desktop-portal` ScreenCast + PipeWire (one-time permission dialog) |
| Autostart, global hotkeys, notifications, DPI | Registry `Run` key, `RegisterHotKey`, toast, PerMonitorV2 manifest | `~/.config/autostart/*.desktop`, portal/X11 hotkeys, `libnotify` |

Design rules that follow from this:

1. **Project split**: `HellboardCompanion.Core` and `HellboardCompanion.App` target plain **`net10.0`** and must not reference Windows-only APIs (no `System.Windows.*`, no `Microsoft.Win32.Registry`, no P/Invoke). All Win32/WinRT code lives in **`HellboardCompanion.Platform.Windows`** (`net10.0-windows10.0.19041.0`). The executable is a thin **host project per platform** (`HellboardCompanion.Desktop.Windows` → `HellboardCompanion.exe`), which composes Core, App and the platform project; a Linux port would add `HellboardCompanion.Desktop.Linux`.
2. **Platform interfaces** in `Core/Platform/`: `IPlatformPaths`, `IPlatformInfo` (P0), later `IGameLocator`, `IFrameSource`, `ISerialPortDiscovery`, `IAutostart`, `IGlobalHotkeys`, `INotifier` – each interface is added in the phase that implements it, so its shape follows the real implementation. The host composes them via dependency injection (`Microsoft.Extensions.DependencyInjection`); the platform project registers its implementations with one `AddWindowsPlatform()` call.
3. **Platform-neutral data types**: frames are passed as OpenCV `Mat` / own `Frame` record, rectangles and monitor info as own records (`Rect`, `MonitorInfo`) – no `HWND`/`HMONITOR` outside the platform project (the `GameLocation` record carries opaque `nint` handles only for the platform code).
4. **Paths** via `Environment.GetFolderPath` / `IPlatformPaths`, never hard-coded `%APPDATA%`; file names are case-sensitive-safe (Linux file systems).
5. **Build guard**: `build.ps1` additionally builds `Core`, `App` and `Core.Tests` with `--runtime linux-x64` (separate artifacts path, no Linux runtime test needed), and `PortabilityTests` fail if Core or App target a platform-specific framework or reference Windows assemblies. The recognition and tracker tests run platform-independent on recorded images.
6. A Linux port would then consist of a new `HellboardCompanion.Platform.Linux` project (window/monitor detection, capture, discovery, autostart, hotkeys) – estimated as a separate phase, not planned.

### 3.2 Supported Windows versions

| Version | Support |
|-|-|
| **Windows 11** | **Full support** – all features are developed and tested on Windows 11. |
| Windows 10 (2004 / build 19041 or newer) | Best effort: supported only as far as it does not block or complicate the implementation. No Windows-10-specific workarounds are implemented. (The capture-border limitation of Windows.Graphics.Capture no longer applies, since Desktop Duplication is used.) |

The target framework `net10.0-windows10.0.19041.0` keeps Windows 10 technically possible; Windows-11-only APIs are used freely where they simplify the implementation (with a graceful fallback or a clear message on Windows 10).

### 3.3 Open source and licensing

Hellboard Companion is open source. Consequences checked on 2026-10-15:

| Item | License | Assessment |
|-|-|-|
| This repository (`LICENSE` in the root) | GPL-2.0 | The root license is not changed (root files are not touched without asking). |
| OpenCvSharp4 / OpenCV, Serilog, Svg.Skia's Skia dependency chain | Apache-2.0 / BSD / MIT | **Apache-2.0 is not compatible with GPL-2.0-only**, but compatible with **GPL-3.0**. → Hellboard Companion is licensed under **GPL-3.0-or-later** (`Software/LICENSE`, decided 2026-10-15). |
| Avalonia, SkiaSharp, Svg.Skia, CommunityToolkit.Mvvm, Microsoft.Extensions.*, System.IO.Ports, Vortice | MIT | Compatible. |
| xUnit, AwesomeAssertions | Apache-2.0 | Test-only, not distributed; compatible anyway with GPL-3.0. |
| FluentAssertions ≥ 8 | Commercial (Xceed) | **Not used.** |
| Firmware: `keyboard.py` + UF2 (`erdemarslan/hid-keyboard-micropython`) | GPL-2.0 | Bundled by the firmware updater as separate files ("mere aggregation"), not linked into the PC software → no conflict. |
| Firmware: `ssd1306.py` (`stlehmann/micropython-ssd1306`) | MIT | Compatible. |
| Stratagem SVG icons (`nvigneux/Helldivers-2-Stratagems-icons-svg`) | none declared | Downloaded at runtime, used read-only, never committed or bundled (§6.11). |
| Game content: screenshots, captured in-game icons | © Arrowhead / Sony | **Never committed** – screenshots in `tests/testData/local/` and captured templates stay local (excluded via `Software/.gitignore`). Committed test data must be generated or free of game assets. |
| Seed catalog (stratagem names, input sequences) | facts / own data | Can be committed. |
| Name "Hellboard Companion", no game logos | – | The game is only named descriptively ("for Helldivers 2"); no official logos or artwork in the UI or repository. |

License notation: every source file starts with `// SPDX-License-Identifier: GPL-3.0-or-later` (Python: `# SPDX-…`); the README states the license. A third-party notices file (`THIRD-PARTY-NOTICES.md`) listing all NuGet dependencies with their licenses is generated for releases (P6).

## 4. Directory Layout

Entries marked *(P0)* exist; the others are added in the phase that needs them.

```
Software/
├── doc/
│   └── spec/
│       ├── implementation-plan.md      ← this document (P0)
│       ├── hellboard-protocol.md       ← PC ↔ Hellboard protocol v1 (basis for the firmware work)
│       └── stratagem-names-de.md       ← English ↔ German stratagem names
├── src/
│   ├── HellboardCompanion.Core/        ← (P0) net10.0, platform-neutral, no UI dependencies
│   │   ├── Platform/                   ← (P0) platform interfaces (IPlatformPaths, IPlatformInfo, later IGameLocator, IFrameSource, …)
│   │   ├── Capture/                    ← frame ring buffer, frame model
│   │   ├── Layout/                     ← loadout screen layout (ROIs), resolution scaling
│   │   ├── Recognition/                ← screen detector, slot classifier, ready-status detector
│   │   ├── Loadout/                    ← loadout state machine, lock-in, history
│   │   ├── Catalog/                    ← stratagem catalog, updater (§6.11)
│   │   ├── Hellboard/                  ← protocol, serial transport, simulator link, firmware updater
│   │   ├── Configuration/
│   │   └── Pipeline/                   ← orchestration
│   ├── HellboardCompanion.Platform.Windows/ ← (P0) net10.0-windows: platform info; later Win32 game/monitor
│   │                                      detection, WGC + DXGI capture, serial discovery, autostart, hotkeys, notifications
│   ├── HellboardCompanion.App/         ← (P0) net10.0, Avalonia tray app, status/debug UI, template tool
│   ├── HellboardCompanion.Desktop.Windows/ ← (P0) Windows host → HellboardCompanion.exe, DI composition, logging,
│   │                                      DPI-aware manifest
├── tools/
│   └── board-spike/                    ← checks MicroPython capabilities on the real board (raw REPL, no flash writes)
├── tests/
│   ├── HellboardCompanion.Core.Tests/  ← (P0) unit tests incl. portability guard
│   └── testData/
│       ├── local/                      ← original screenshots provided by the user – local only, never committed
│       └── <resolution>/               ← generated scenes + expected results (free of game assets)
├── assets/
│   └── catalog.seed.json               ← (P6) seed catalog: names, groups, input sequences
├── config/
│   └── default.config.json
├── HellboardCompanion.slnx             ← (P0) solution (XML format)
├── Directory.Build.props               ← (P0) common settings (nullable, warnings as errors, product info)
├── Directory.Packages.props            ← (P0) central NuGet package versions
├── global.json                         ← (P0) SDK version and test runner
├── .editorconfig                       ← (P0) code style, SPDX file header
├── build.ps1                           ← (P0) build + test + linux-x64 guard
├── LICENSE                             ← (P0) GPL-3.0 (§3.3)
├── .gitignore                          ← (P0) bin/, obj/, artifacts/, .idea/, tests/testData/local/
└── README.md                           ← (P0)
```

Captured in-game templates and rendered SVG templates are stored in the user data directory, not in the repository (§3.3, §6.11).

## 5. Architecture Overview

```
┌─────────────┐ HMONITOR ┌─────────────┐ frames ┌──────────────────────┐ observations ┌──────────────────┐ locked loadout ┌───────────────┐
│ GameLocator │ ───────▶ │ FrameSource │ ─────▶ │ LoadoutRecognizer    │ ───────────▶ │ LoadoutTracker   │ ─────────────▶ │ HellboardLink │
└─────────────┘          │ + RingBuffer│        │  screen / slots /    │              │ (state machine,  │                │ (serial/sim/  │
                         └─────────────┘        │  ready status        │              │  lock-in)        │                │  dry-run)     │
                                                └──────────────────────┘              └──────────────────┘                └───────────────┘
                                                                                               │
                                                                                               └──▶ LoadoutHistory (log), UI
```

All components are interfaces in `Core`, so tests can replace capture with PNG sequences and the Hellboard with a fake.

## 6. Components

### 6.1 GameLocator – find the Helldivers 2 window and its monitor

1. **Find the process**: `helldivers2.exe` (name configurable), polled every 2 s while not running.
2. **Find the main window**: `EnumWindows` → `GetWindowThreadProcessId` == game PID, visible, not a tool window, largest client area.
3. **Determine the monitor**:
   - `MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)` → `HMONITOR` (window spanning two monitors → largest intersection).
   - `GetMonitorInfo` → monitor rectangle + device name (`\\.\DISPLAY2`).
   - `GetClientRect` + `ClientToScreen` → client area in screen coordinates.
   - Application manifest: **Per-Monitor DPI aware v2**, so all coordinates are physical pixels.
4. **Window mode**: client rect vs. monitor rect → `FullscreenOrBorderless` / `Windowed`; `IsIconic` for minimized.
5. **Track changes**: `SetWinEventHook` (`EVENT_OBJECT_LOCATIONCHANGE`, `EVENT_OBJECT_DESTROY`, `EVENT_SYSTEM_MINIMIZESTART/END` filtered on the game PID, `EVENT_SYSTEM_FOREGROUND` system-wide) on a dedicated thread with its own message loop, plus a **1 s timer** that covers process start/exit and monitor layout / resolution changes (monitors are re-enumerated on every evaluation, so no `WM_DISPLAYCHANGE` window is needed). Each change publishes a new `GameSnapshot`; unchanged snapshots are not re-published.

```csharp
record GameLocation(
    nint WindowHandle, nint MonitorHandle, string MonitorDeviceName,
    Rect MonitorBounds, Rect ClientBounds, WindowMode Mode,
    bool IsForeground, bool IsMinimized);
```

States: `GameNotRunning → GameStarting → GameRunning(location) → GameNotRunning`. Manual monitor override (`\\.\DISPLAYn`) in the config as fallback.

Edge cases: different resolutions / scaling per monitor, monitors with negative coordinates, game restart with new HWND, game on a non-primary monitor.

### 6.2 FrameSource – capture

- **`IFrameSource.Capture(monitor, region, timeout)`** (Core) – pull-based, the caller decides the rate; returns a platform-neutral `Frame` (BGRA, tightly packed, screen bounds, timestamp).
- **`DesktopDuplicationFrameSource`** (Platform.Windows): keeps one duplication session per monitor open, copies each new desktop image into a staging texture and reads back only the requested region. If no new image arrives within the timeout (static screen), the last image in the staging texture is used. The session is recreated when the monitor changes or the access is lost (display mode change, secure desktop, UAC).
- **`GameFrameGrabber`** (Core) combines `IGameLocator` and `IFrameSource`: captures the game's client area, or nothing while the game is not running or minimized.
- **Black-frame detection** (`Frame.IsBlack`, sparse sample) → warning in the UI with the advice to use borderless window mode (exclusive fullscreen may deliver black images).
- Measured (P2): full 2560×1440 client area at 2 frames/s: 14 ms per capture on average, 0.25 % CPU summed over all cores (24 logical cores).
- Later optimizations, only if needed: copy only the ROIs (`CopySubresourceRegion`), reuse pixel buffers, adaptive rate (P4).
- Frame-sequence recording for replay tests moves to the manual trigger (§6.13, `saveSnapshotOnTrigger`).

### 6.3 Loadout screen layout

Screen structure (from the loadout screen screenshot, tab "Ausrüstung", German UI):

- The **local player's card is always the leftmost card**. Other players' cards (or "empty slot" placeholders) are shown to the right of it and are **ignored** – no ROI covers them.
- Inside the card, from top to bottom:
  1. Player name / level header, character preview.
  2. Label "Missions-Taktikausrüstungen" and the **mission stratagem row**: 3 fixed mission stratagems (Reinforce, SOS Beacon, Resupply – confirmed visually; beige frame with corner accents) followed by **up to 4 optional mission stratagems** offered by the game.
  3. **Loadout stratagem row**: the **4 stratagems** chosen by the player, followed by the booster slot (hexagon). **Boosters are ignored.**
  4. **Ready button** across the full card width (§6.4).

Measured positions in the **reference resolution 1920×1080** – measured on native 2560×1440 captures (pixel values × 0.75) and verified pixel-exact on a 1920×1200 capture (P3, 2026-10-15). Source of truth: `assets/layouts/loadout-v1.json` (embedded into Core); the values below are for orientation.

| ROI | x | y | w | h | 2560×1440 (native) | Purpose |
|-|-|-|-|-|-|-|
| `readyButton` | 54.75 | 927 | 420 | 42 | 73, 1236, 560×56 | Ready button (`Prepare` / `Ready`, §6.4); primary screen anchor. |
| `playerSlots[0..3]` | 60 / 144.75 / 229.5 / 315 | 842.25 | 70 | 70 | x 80 / 193 / 306 / 420, y 1123, 93–94 px | Inner area of the 4 player stratagem slots. |
| `fixedMissionSlots[0..2]` | 60 / 120.75 / 181.5 | 777.75 | 45 | 45 | x 80 / 161 / 242, y 1037, 60 px | Reinforce, SOS Beacon, Resupply (beige frame incl.); supporting evidence only (§6.4). |
| `optionalMissionSlots[0..3]` | 242.25 / 303 / 363.75 / 424.5 | 777.75 | 45 | 45 | x 323 / 404 / 485 / 566, y 1037, 60 px | Optional mission stratagems (same frame style as the fixed ones when present). |

Visual observations (unmasked screenshots, status `Prepare`):

- The card position varies by a few pixels between screenshots. At native resolution, two window captures (2552/2553 px wide) are shifted by ≈ 5–8 px against full-screen captures (2560 px) – the shift comes from the capture crop, which the real program avoids by capturing exactly the client area. This must be absorbed by the offset correction below.
- **Player stratagem icons** are drawn in their **category color** on a dark square: red (orbital/eagle), blue (support weapons/backpacks), green (sentries/emplacements). Example observed (confirmed): Eagle 500KG Bomb (red), Grenade Launcher (blue), Supply Pack (blue), **Mortar Sentry** (green, not the Gas Mortar). The category color is a strong, cheap pre-filter for classification.
- **Empty player slots** are dark grey squares with a thin light border; **empty mission slots** are dark, nearly transparent squares with a faint border.
- The **focused slot** (gamepad/keyboard selection) gets **yellow corner brackets** around the square. These brackets must not influence the classification.
- The ready button can have a **yellow outline** (focus highlight) even in state `Prepare`.
- The booster hexagon turns bright yellow when a booster is selected; it lies outside all ROIs and is ignored.

Rules for scaling and tolerance:

- Positions are relative to the **game's client area**, anchored **top-left**, and scaled by `s = min(clientWidth / 1920, clientHeight / 1080)` (verified for 16:9 and 16:10, §6.3.1). Supported resolutions and aspect ratios: §6.3.1.
- **Tolerance for slight deviations**: every slot is matched inside a search window enlarged by **±2 % of the client height** (≈ ±20 px at 1080p) and with a small scale range (**±5 %**, 3 steps). The offset found by the screen detection (§6.4) is used to correct all other ROIs of the same frame, so a global shift of the card is absorbed once.
- Text elements ("Ausrüstung", "Missions-Taktikausrüstungen", "Bereitmachen") are **not** used as anchors because they depend on the game language.
- The layout is data (`config/layouts/loadout-v1.json`), so a game update only requires re-measuring, not code changes.


#### 6.3.1 Resolution support

Requirements:

| Range | Support level |
|-|-|
| **1280×720 (HD) … 3840×2160 (4K)**, 16:9 | **Must** work reliably. |
| Below 720p, above 4K | Best effort. Below ~720p a warning is shown when the player slots become smaller than ~48 px. |
| 16:10 | **Must** – layout verified with a 1920×1200 capture. |
| Wider aspect ratios (21:9, 32:9), arbitrary window sizes | Best effort; horizontal anchoring unverified → auto-calibration (step 7). |

Observations:

- The game's **render resolution** (3D scene) does not change the UI: `with_stratagem_small.png` was taken with a lower render resolution but the same output resolution 2560×1440 – the UI layout and the button position are identical. Only the **output (window/client) resolution** matters; only the 3D background differs, which the ROIs avoid.
- **16:10 (1920×1200, windowed)**: the card is laid out **exactly as at 1920×1080** – same position (ready button at x 55, y 927, 420×42 px, ±1 px) and same size. The UI is therefore scaled by the limiting dimension (`min(W/1920, H/1080)` = 1.0, not `H/1080` = 1.11) and the card is anchored **top-left**; the additional 120 px of height remain below the card. Bottom elements (key hint bar) are anchored to the bottom edge, but they are not used by the recognition.
- Window-mode screenshots include the window border (1922×1201 instead of 1920×1200). The program captures exactly the client area (§6.1), so this does not affect it.
- Slot sizes across the required range (scaled from the native 2560×1440 measurements): player slot ≈ 53 px at 720p, 80 px at 1080p, 107 px at 1440p, 160 px at 4K; ready button 280×28 px at 720p up to 840×84 px at 4K.

Strategy – everything is computed relative to the client height, nothing is tied to a pixel size:

1. **Layout**: ROIs in reference coordinates (1920×1080), anchored top-left and scaled by `s = min(clientWidth / 1920, clientHeight / 1080)` (0.667 at 720p … 2.0 at 4K; 1.0 at 1920×1200). For aspect ratios wider than 16:9 (21:9, 32:9) the horizontal placement is not yet verified (left-anchored vs. centered 16:9 area) → handled by auto-calibration (step 7) until real captures exist.
2. **Slot classification at a canonical size**: each slot crop (inner area) is resized to a **canonical size** (player slots 64×64, mission slots 48×48) before matching – `INTER_AREA` when shrinking, `INTER_CUBIC` when enlarging. Templates are stored at the same canonical size. This makes classification independent of the resolution and equally fast at every resolution.
3. **Templates are created from the highest available resolution** (currently 2560×1440, later 4K if available) and downscaled to the canonical size. They are never created from low-resolution captures, to avoid upscaling artifacts.
4. **Structural cues** (ready button, slot grid edge template) are scaled to the client resolution at runtime and cached per resolution. Edge detection thresholds and the Canny aperture scale with `s`.
5. **Ready-button state** uses a yellow pixel *share*, which is scale-independent by definition.
6. **Resolution changes** at runtime (window resize, settings change) invalidate the caches via the `GameLocation` event (§6.1).
7. **Auto-calibration fallback**: if the ready button is not found at the predicted position, the screen detector searches for it in the whole left half of the client area with the scale candidates `min(W/1920, H/1080)`, `H/1080` and `W/1920`. A successful, confirmed hit (cues A + B, §6.4) stores the found origin and scale **per client size** in the user config, so the search runs only once per resolution. This keeps unknown aspect ratios and future layout changes working without a program update.

Verification without real captures at other resolutions:

- **Synthetic test data**: the native 2560×1440 screenshots are rescaled to 1280×720, 1600×900, 1920×1080 and 3840×2160 (`INTER_AREA` / `INTER_CUBIC`) and run through the full recognition. This covers the scaling math, but not resolution-specific rendering differences (font hinting, line widths, anti-aliasing of UI elements).
- **Real captures** at 1080p and 4K (and 720p if possible) are added later via the debug view's snapshot function (§6.7) and become part of the regression set as soon as they exist.
- **Self-check in the app**: the status page shows the detected client resolution, scale factor and per-cue match scores, so a failing resolution is immediately visible.

### 6.4 LoadoutRecognizer

Per frame, three steps with early exit:

1. **Screen detection** *(implemented: `LoadoutScreenDetector`)* – must not depend on any specific stratagem, mission or text, because the game developers may change mission stratagems at any time. It combines **structural cues** of the local player's card that are present regardless of content, language or ready state:

   | Cue | Check (as implemented) | Role |
   |-|-|-|
   | **A – Ready button** | In a search window of ±2 % of the client height around the expected position: rows/columns that are mostly yellow give the button rectangle (Prepare: yellow outline, Ready: yellow fill). Score = min(size match, pattern), pattern = diagonal stripes (block-wise, direction-independent, so label and key symbol do not matter) or yellow fill ≥ 60 %. | required (≥ 0.6) |
   | **B – Slot raster** | Uniform neutral gray (≈ 47, 47, 47) along the inner edge of all 4 player slots – present for empty and filled slots. *The gaps between the slots are transparent (animated background), so they are not used.* | required (≥ 0.4) |
   | **D – Fixed mission icons** | Beige frames of Reinforce / SOS Beacon / Resupply. | supporting |
   | *C – Card frame* | *Not implemented – A and B separate clearly; added only if needed.* | – |

   - The loadout screen counts as visible when **A and B** match, or when one of them is strong (≥ 0.85) and D matches.
   - **Offset correction**: derived from the **center** of the found button (the Ready fill is ~3 px smaller per side than the Prepare outline at 1440p). All ROIs are shifted by it; verified with artificially shifted captures (±20 px).
   - **Measured** on all local scenes at 720p, 900p, 1080p, native and 2160p incl. 16:10: loadout screen button 0.86–1.00 / raster 0.61–0.99; other screens (settings menu, 12 ship management screens, black frame) button 0.00 / raster ≤ 0.05.
   - Cheap path: when no loadout screen is visible, only cue A is evaluated (a single small ROI), 2 fps.

2. **Slot classification** *(implemented: `StratagemSlotClassifier`)*: each slot (offset-corrected) is cropped with a small margin, scaled to a **canonical size of 64 px** (+ 3 px shift tolerance per side) and compared with `TM_CCOEFF_NORMED` against templates **rendered from the SVG icons** (Svg.Skia) – no captured game images are needed:

   | Slot type | Icon size (of the slot) | Background (RGB) | Excluded border | Measured (all scenes, 720p–4K) |
   |-|-|-|-|-|
   | Player slots | 87.5 % | 47, 47, 47 | – | correct in 30/30 cases, score 0.78–0.97, margin to 2nd ≥ 0.08 |
   | Mission slots (fixed + optional) | 86.7 % | 60, 59, 45 | 15 % (beige frame and corner ornaments) | correct in 30/30 cases, score 0.71–0.96, margin ≥ 0.21 |

   - All templates (currently all 109 icons of the repository) are compared for every slot; accepted when score ≥ 0.70 and margin ≥ 0.05, otherwise `Unknown` (best guess kept for diagnostics).
   - **Empty** slots are detected by a low standard deviation of the slot center (uniform background), before matching.
   - Icons are taken from a local copy of the icon repository (`StratagemIconDownloader`, minimal version; the full catalog updater follows in P6). Ids are slugs of the file names, e.g. `eagle_500kg_bomb`.
   - Captured in-game templates (template tool, §6.7) are optional now; they are only needed if a future icon differs too much from its SVG.
3. **Status detection** *(implemented: `ReadyStatusDetector`; measured on all local scenes at 720p, 900p, 1080p, native and 2160p incl. 16:10: Prepare 6–8 %, Ready 71–73 % yellow share)*: `readyButton` is the ready button. It has two visual states; its **label is ignored** (it depends on the game language and its length varies):

   | State | Appearance | Yellow pixel share* | Mean RGB |
   |-|-|-|-|
   | `Prepare` (loadout still editable) | Dark background, yellow diagonal stripes, yellow label | ~6–17 % (measured on two captures) | (83, 85, 34) |
   | `Ready` (loadout confirmed) | Bright yellow background, lighter stripes, black label | ~77 % | (200, 186, 13) |

   \* yellow = R > 180, G > 160, B < 90; measured on the full button area at native resolution (≈ 560×56 px at 2560×1440).

   - The share is measured on the button area **inset by ~15 % vertically and ~5 % horizontally**, so the yellow focus outline (§6.3) does not count.
   - Classification by the **yellow pixel share** over that inner area: `< 35 %` → `Prepare`, `> 60 %` → `Ready`, in between → `Unknown` (e.g. transition animation, overlay). The label covers too little area to move the share across these thresholds, so no text recognition or text masking is required.
   - A state change is accepted after **2 consecutive frames** with the same result.
   - A sanity check (button area has the expected stripe pattern / aspect) prevents a random yellow area from being classified as `Ready` when the anchor match was marginal.

Output: `LoadoutObservation(timestamp, screenVisible, offset, stratagems[4], missionStratagems[0..4], status)` where every slot is `{id, score, margin}`.

**Template library** (`assets/templates/stratagems/catalog.json`):

```json
{
  "referenceResolution": [1920, 1080],
  "stratagems": [
    { "id": "eagle_500kg_bomb", "name": "Eagle 500KG Bomb", "file": "eagle_500kg_bomb.png", "category": "red", "firmwareMacro": "Eagle500KGBomb" },
    { "id": "grenade_launcher", "name": "Grenade Launcher", "file": "grenade_launcher.png", "category": "blue", "firmwareMacro": "GrenadeLauncher" }
  ]
}
```

`firmwareMacro` maps a stratagem to the attribute name in the firmware's `Macros` class (only needed if decision F2 keeps a firmware-side table). The catalog itself is maintained automatically from the icon repository (§6.11). Templates come from two sources: **rendered SVG icons** (automatic, available for every stratagem in the repository) and **captured in-game templates** (template tool, §6.7). Captured templates take precedence; rendered ones cover stratagems that have not been captured yet.

### 6.5 LoadoutTracker – lock-in under time pressure

> **Implementation status (P4):** `LoadoutRecognizer` (one `LoadoutObservation` per frame), `LoadoutTracker` (state machine below, 11 unit tests incl. replays of real captures), `LoadoutPipeline` (background loop: 500 ms interval without loadout screen, 100 ms with it) and `LoadoutHistory` (`loadouts.jsonl`) are implemented. Measured recognition time per frame: no loadout screen 1–10 ms (≤ 25 ms at 4K); new loadout 75–140 ms; unchanged loadout 2–11 ms (≤ 38 ms at 4K) thanks to a per-slot result cache. Because every captured frame is analyzed completely and fast enough, the **ring buffer** described below is not needed and was not implemented; the stable candidate from the last frames covers the "screen closes immediately" case (tested by replay).

Problem: if the local player is the last one to confirm, the game may leave the loadout screen almost immediately. Recognition therefore must **not depend on frames after the confirmation**.

Strategy:

1. While the loadout screen is visible, the recognizer runs continuously at high rate and the tracker keeps a **stable candidate**: a slot value becomes stable after it was recognized identically in `stableFrames` (default 3) consecutive frames. The player usually spends seconds on this screen, so a stable candidate exists long before confirmation.
2. **Lock-in triggers**:
   - Status changes `Prepare → Ready` → lock in the current stable candidate (plus that frame's own result if it agrees) and transfer it to the Hellboard.
   - The loadout screen disappears while a stable candidate exists and no lock-in happened yet (e.g. the `Ready` frame was too short to be seen) → re-evaluate the **ring buffer** frames of the last ~2 s, find the most recent frame with the screen visible and a full, confident result, then lock in. Transferring on a screen exit without `Ready` (e.g. player pressed Esc) is harmless, because it is still the loadout the player selected; it is marked with `source = ScreenExit`.
3. **Plausibility checks**: all 4 player slots known (or explicitly empty), no duplicate stratagems. Otherwise lock in partially, mark the unknown slots and report a warning in the UI / log.
4. **Ready → Prepare (multiplayer)**: the player can withdraw the ready state and keep editing. The tracker then returns to `Selecting`, discards the stable candidate and builds a new one from the following frames. The next `Prepare → Ready` produces a new lock-in. It is transferred to the Hellboard **only if it differs** from the last transferred loadout; it is always written to the history. This can repeat any number of times.
5. Each lock-in produces a `LockedLoadout(timestamp, stratagems, missionStratagems, confidence, source = Ready | ScreenExit | Manual)` and is written to the **loadout history** (`%LOCALAPPDATA%\HellboardCompanion\loadouts.jsonl`).

```
                 screen visible                status Prepare→Ready
      ┌──────┐ ────────────────▶ ┌───────────┐ ────────────────────▶ ┌────────────────┐
      │ Idle │                   │ Selecting │                       │ Ready (locked, │
      └──────┘ ◀──────────────── └───────────┘ ◀──────────────────── │  transferred)  │
         ▲      screen gone:            status Ready→Prepare          └────────────────┘
         │      lock-in from ring buffer                                      │
         └──────────────────────────── screen gone ───────────────────────────┘
```

Timing target: lock-in ≤ 100 ms after the first `Ready` frame, or ≤ 300 ms after screen exit.

### 6.6 HellboardLink – transfer to the Hellboard

The firmware side is developed in this project (§2.1, phase P7). The PC side is built against an interface, so dry-run, simulator and the real board are interchangeable.

- **Interface**:

```csharp
interface IHellboardLink {
    Task<bool> ConnectAsync(CancellationToken ct);
    Task<TransferResult> SendLoadoutAsync(LockedLoadout loadout, CancellationToken ct);
    event EventHandler<HellboardStatus> StatusChanged;
}
```

- **Implementations**:
  - `DryRunHellboardLink` (default until the extended firmware exists): logs what would be sent.
  - `SerialHellboardLink`: USB CDC serial; finds the board by USB VID/PID (RP2040 MicroPython: `2E8A:0005`) or a configured COM port; reconnects automatically.
  - `SimulatedHellboard` (in-process, used by the tests and the `Simulator` connection mode): implements the protocol like the firmware should. *A separate simulator console app is not needed.*
- **Protocol**: specified in **[hellboard-protocol.md](hellboard-protocol.md)** (version 1, 2026-10-16): newline-delimited JSON over USB CDC, requests with `id`/`ack`, commands `ping`, `setLoadout` (complete stratagem definitions: name, color, arrow sequence – F2), `getState`, `setConfig` (F7), `saveDefault` (flash write only on "Set as default" – F3), `enterUpdateMode` (F8), event `hello`. Before the firmware work starts, the capabilities of MicroPython v1.17 on the real board are verified with the board spike (`tools/board-spike/`).

- **Button mapping** (target behavior of the extended firmware; based on the current `Firmware/main.py`, may change with the firmware decisions):

| Screen | Hellboard |
|-|-|
| Player stratagems 1–4 (red row) | `button_2_1 … button_2_4`, in slot order. |
| Multi-function list (see below) | List of the **multi-function button** `button_1_3` of the upper row (`row1List`, cycled with `button_1_4` / `CYCLE`). |
| Fixed mission stratagems (Reinforce, SOS Beacon, Resupply) | Not transferred automatically; Reinforce and Resupply stay on `button_1_1` / `button_1_2`. SOS Beacon can be added via the fixed list. |

- **Multi-function list mode** (user setting, §6.7). The list sent as `missionStratagems` is built by the `MultiFunctionListBuilder`:

| Mode | UI label | Resulting list |
|-|-|-|
| `FixedList` | "Fixed list" | Only the stratagems the user selected in the software (ordered), e.g. SOS Beacon, Eagle Rearm, SEAF Artillery. Detected optional mission stratagems are ignored. |
| `AutoDetect` | "Automatic detection" | Only the optional mission stratagems detected on the loadout screen (green row), in slot order. Empty if the mission offers none. |
| `FixedListAndAutoDetect` | "Fixed list + automatic detection" | Fixed list first, then the detected stratagems; duplicates are removed (first occurrence wins). |

  - Default: `FixedListAndAutoDetect` with SOS Beacon in the fixed list (matches the current hardcoded `row1List` behavior).
  - In `FixedList` mode the slot recognition of the green row is still run and shown in the debug view, but not transferred.
  - When the mode or the fixed list is changed in the UI, the list is rebuilt from the last locked loadout and re-sent immediately (if a Hellboard is connected), so the user does not have to re-enter the loadout screen.
  - If the resulting list is empty, an empty list is sent; the firmware behavior for that case is decision F5 (§6.9).
- Retries with timeout (e.g. 3 × 500 ms); the last locked loadout is re-sent when the board reconnects.
- Stratagems unknown to the firmware are reported in the UI (only relevant if decision F2 keeps a firmware-side stratagem table).

### 6.7 UI (Avalonia tray app)

> **Implementation status (P6, steps 2–3):** the main window has four tabs. **Overview** (for demonstrations): the chain game → recognition → Hellboard with status lights, the loadout as large stratagem tiles with icons (live while selecting, otherwise the last locked one – after a restart the last one from the history), the multi-function list, warnings for stratagems without input sequence, "Recognize loadout now" and "Set as default". **Settings**: recognition mode, hotkey, test mode, saving trigger frames; Hellboard connection (USB / simulator / dry run) and port; multi-function mode and fixed-list editor with icons (add, reorder, remove); start with Windows (`HKCU\…\Run`); game process name – every change is saved and applied immediately. **History**: locked loadouts with icons and recognition problems; view "Important" (default: Ready, manual and problems) or "Everything" (also lock-ins when the screen closed), saved in `config.json` (`ui.historyFilter`). Problems: unrecognized stratagem (screenshot of the slot, PNG in `%LOCALAPPDATA%\HellboardCompanion\problems`), input sequence unknown (catalog icon, once per session), loadout screen not detected on a manual trigger (reduced screenshot), recognition errors (text, at most every 10 min per error type). **Diagnostics**: the former status window (game, recognition and Hellboard details, capture preview, snapshots, manual recognition, image file analysis). Icons for the UI are rendered from the SVGs (`StratagemImages`). Not implemented: template tool (not needed – templates come from the SVGs), warning about game key bindings.

- **Tray icon** with status color: grey = game not running, yellow = game running, green = loadout screen visible, blue = loadout locked and transferred, red = error. Menu: Pause/Resume, Open window, Re-send last loadout, Exit.
- **Status page**: process, monitor (`\\.\DISPLAYn`, resolution, scaling, position), window mode, capture backend, fps, Hellboard connection, last locked loadout with icons and confidence.
- **Debug view**: live preview of the ROIs with slot results (best/second-best template, scores) and ready status. "Snapshot" saves full frame + ROIs in the `tests/testData` format. "Record" stores a frame sequence for replay tests.
- **Template tool**: on a snapshot, pick a slot → choose the stratagem from the catalog → saves the normalized template PNG. This makes it easy to build the library and to update it after game patches.
- **History page**: list of locked loadouts.
- **Multi-function button page**:
  - Mode selection (radio buttons): "Fixed list", "Automatic detection", "Fixed list + automatic detection".
  - Fixed list editor: pick stratagems from the catalog (with icons), reorder by drag & drop or up/down buttons, remove. Stratagems without a firmware macro are marked.
  - Preview of the resulting list for the last locked loadout.
- **Settings**: start with Windows, process name, monitor override, Hellboard mode (dry-run / serial / simulator), COM port, thresholds, recognition mode and trigger hotkey (§6.13).

### 6.8 Configuration

> **Implementation status (P6, step 1):** `CompanionSettings` with the sections `general`, `game`, `recognition`, `hellboard`, `multiFunction` in `%APPDATA%\HellboardCompanion\config.json` (`SettingsStore`: defaults for missing values, atomic write, an unreadable file is kept as `config.json.invalid`). The sections are the option objects the services use, so most changes apply immediately; the Hellboard link reconnects on a changed connection mode/port and resends a changed multi-function list, the hotkey is re-registered. Settings are changed through the settings page (step 2) instead of hot-reloading the file; JSON schema validation is not needed for that and was dropped.

- `%APPDATA%\HellboardCompanion\config.json`, created from `config/default.config.json` on first start; validated against a JSON schema; hot reload via `FileSystemWatcher`.
- Sections: `game`, `capture`, `layout`, `recognition` (thresholds, `stableFrames`), `hellboard`, `multiFunction`, `ui`.

```json
"recognition": {
  "mode": "AutomaticWithManualTrigger",      // Automatic | ManualOnly | AutomaticWithManualTrigger
  "triggerHotkey": "Ctrl+Shift+F9",
  "manualTransfer": true,                    // false = test mode: show result only
  "saveSnapshotOnTrigger": false
},
"multiFunction": {
  "mode": "FixedListAndAutoDetect",          // FixedList | AutoDetect | FixedListAndAutoDetect
  "fixedList": ["sos_beacon", "eagle_rearm"] // catalog ids, in button order
}
```

### 6.9 Firmware decision log

Decisions that require changes to the Hellboard firmware. Each one is discussed with the user (§2.1); status `open` → `proposed` → `agreed` (with the chosen option) → `implemented`.

Starting point (reviewed 2026-10-16): the firmware runs on a third-party MicroPython **v1.17 build from 2021** with a custom `usb_hid` module (`erdemarslan/hid-keyboard-micropython`, GPL-2.0). `main.py` hard-codes the button assignment, `macro.py` contains ~80 stratagem definitions (name, color, arrow sequence), `helldiver.py` sends them as HID keyboard input. The USB CDC serial port (REPL) is available next to HID – MicroPico uses it for uploads.

**Hardware constraint (user, 2026-10-16):** the M93C86 EEPROM in the schematic is **not populated** (it could not be made to work). All firmware changes must assume that **no EEPROM is present**; only the memory of the RP2040-Zero itself is available (264 KB RAM, 2 MB on-board flash with the MicroPython filesystem).

| ID | Decision | Decision / proposal | Alternatives | Status |
|-|-|-|-|-|
| F1 | Transport PC ↔ Hellboard | **USB CDC serial** (the existing REPL port). `main.py` reads newline-delimited JSON from `sys.stdin` non-blocking (`select.poll`) inside the main loop and answers via `print`. No firmware build changes needed. | Vendor-specific HID reports (custom MicroPython build, no advantage). | **agreed 2026-10-16** |
| F2 | Stratagem data model | **The PC sends complete definitions**: full display name (never shortened; only the firmware display module fits it to the OLED line – prepared for a later scrolling text, see hellboard-protocol.md §4.2), color category and arrow sequence. The firmware needs no stratagem table; new stratagems only need an update of the PC catalog. A small built-in default loadout stays in the firmware for use without PC. | PC sends names that the firmware resolves in its own `Macros` table (firmware update for every new stratagem). | **agreed 2026-10-16** |
| F3 | Persistence | **Received loadouts and settings are kept in RAM only.** They are written to the RP2040 flash (JSON file, LittleFS) **only on an explicit user action**: button **"Set as default"** in the Companion, no confirmation dialog. At power-up the board starts with the saved default (or the built-in default loadout). This keeps flash writes to a minimum. | Write every received loadout to flash. | **agreed 2026-10-16 (modified by the user)** |
| F4 | Feedback on the board | Display as today (names) plus a **2 s message "Loadout received"** and a small **"PC" connection indicator**; LEDs: short flash of all buttons in their category colors on receipt; error state = red flash. | Display only; LEDs only. | **agreed 2026-10-16** |
| F5 | Multi-function button | `button_1_3` gets the **list sent by the PC** (`missionStratagems`, built by the selected mode §6.6); `button_1_4` cycles through it; index resets to 0 on a new list; **empty list** → button disabled (LED off, display "–"). No length limit. | Keep firmware-side defaults when the list is empty. | **agreed 2026-10-16** |
| F6 | Firmware structure | **Refactor into modules**: `state` (loadout, persistence), `protocol` (serial), `hid` (macro output), `display`, `leds`, `buttons`, thin `main.py`; non-blocking main loop; `FIRMWARE_VERSION` and `PROTOCOL_VERSION` reported by `ping`. | Minimal changes in the current `main.py`. | **agreed 2026-10-16** |
| F7 | Configuration beyond the loadout | **Yes, via a `setConfig` command** (modifier key, toggle mode, key timing, arrow keys vs. WASD, LED brightness), applied in RAM and persisted only via "Set as default" (same rule as F3), edited in the PC settings page. Implemented after the loadout transfer works. | Keep editing `helldiver.py`. | **agreed 2026-10-16** |
| F8 | Update mode entry | **Protocol command `enterUpdateMode`** (releases all keys, stops LEDs/display, returns to the REPL) **plus Ctrl-C fallback**; the keyboard interrupt stays enabled. | Ctrl-C only. | **agreed 2026-10-16** |
| F9 | Update of MicroPython itself (UF2) | **Supported**: `machine.bootloader()` → drive `RPI-RP2` → PC copies the UF2; only after explicit confirmation by the user. | Not supported (manual as today). | **agreed 2026-10-16** |
| F10 | MicroPython base | **Switch to the official, current MicroPython** (MIT) plus `usb-device-keyboard` from micropython-lib (HID keyboard and CDC serial at the same time). **Implemented in isolation as its own phase P7b** after P7 is done and stable: all F1–F9 work is done on the existing v1.17 build first. The F6 module `hid` is the only place that uses the HID API, so the switch touches one module plus the UF2. See P7b for the test plan and rollback. | Keep the third-party v1.17 build. | **agreed 2026-10-16 – isolated phase P7b** |

New decisions are appended to this table as they come up.

### 6.10 Firmware updater (Python files on the Hellboard)

The PC software can update the MicroPython application files on the Hellboard, so the user no longer needs VS Code / MicroPico for updates.

**Feasibility:** yes. The board runs MicroPython (`Firmware/libs/3rd/firmware_with_HID_support.v.1.17.uf2`), which exposes the **REPL over USB CDC serial** next to the HID keyboard – this is exactly what MicroPico uses today to upload files. The same mechanism (as in `mpremote`) can be implemented in C# on top of `System.IO.Ports`.

Procedure:

1. **Detect board and version**: find the serial port by USB VID/PID (`2E8A:0005`), query the firmware version via the protocol (`ping` → `fw`), compare with the firmware bundle shipped with the PC software. Offer an update if it differs (never automatically during a mission).
2. **Stop the application**: protocol command `enterUpdateMode` (clean stop: releases all keys, stops LEDs/display updates, returns to the REPL). Fallback: send `Ctrl-C` (KeyboardInterrupt). The firmware must therefore **not** disable the keyboard interrupt (`micropython.kbd_intr(-1)`) – a design constraint for decision F1.
3. **Raw REPL** (`Ctrl-A`, raw-paste mode where available): files are written in chunks (base64-encoded, e.g. 1 KB per chunk) to a temporary name (`main.py.new`).
4. **Verify**: SHA-256 of every written file is computed on the board (`hashlib.sha256`) and compared with the PC-side hash.
5. **Activate atomically**: only after all files are verified, rename the old files to `*.bak` and the new files to their final names (`os.rename`), write `version.json`.
6. **Restart**: soft reset (`Ctrl-D` / `machine.soft_reset()`), wait for the board to reconnect, check the new version via `ping`.
7. **Rollback**: if the board does not report the expected version within a timeout, restore the `*.bak` files via the raw REPL and report the error.

Further points:

- **Update bundle**: the Python files from `Firmware/` (`main.py`, `libs/*.py`, `libs/3rd/*.py`) plus a manifest (`version`, file list, SHA-256) are packed into the PC software at build time. The `Firmware/` folder is only read by the build, never modified by it.
- **Optional**: precompile to `.mpy` with `mpy-cross` (smaller, faster boot) – later.
- **MicroPython itself (UF2)**: can also be updated: `machine.bootloader()` puts the RP2040 into BOOTSEL mode, it appears as drive `RPI-RP2`, the PC software copies the UF2 file onto it. Only when necessary (decision F9), with explicit user confirmation.
- **Recovery** if everything fails: hold the `Boot` button while connecting and re-flash the UF2 + files (as in the current README). The UI links to these instructions.
- **Safety**: the update runs only on explicit user action, shows the file list and versions before starting, and blocks the loadout transfer while it is running. The Hellboard does not send key presses while in update mode.
- **UI**: "Hellboard" page with connection state, firmware version on the board vs. bundled version, button "Update Hellboard", progress and log.

### 6.11 Stratagem catalog and automatic update

Source: the icon repository **`nvigneux/Helldivers-2-Stratagems-icons-svg`** on GitHub (already referenced in the root README), branch `master`.

Repository structure (checked 2026-10-15, 111 files):

- One SVG per stratagem, **file name = display name** (e.g. `Hangar/Eagle 500KG Bomb.svg`).
- Top-level folders = ship module or warbond (`General Stratagems`, `Hangar`, `Bridge`, `Orbital Cannons`, `Engineering Bay`, `Robotics Workshop`, `Patriotic Administration Center`, plus one folder per warbond).
- The SVGs carry the **category color** (e.g. red `#de7b6c` for Eagle 500KG Bomb) on a transparent background, viewBox 126×126.
- The repository contains **no input sequences** and no machine-readable metadata – input sequences are maintained by this project (§6.12).
- The repository declares **no license** → icons are downloaded at runtime into the user's data directory and are **not** committed to this repository or bundled in the release (see open questions).

Catalog model (`catalog.json` in the user data directory, seeded from `assets/catalog.seed.json` shipped with the program):

```json
{
  "id": "eagle_500kg_bomb",                 // slug of the display name, stable
  "name": "Eagle 500KG Bomb",
  "group": "Hangar",                        // repository folder
  "category": "red",                        // derived from the SVG fill color
  "source": { "path": "Hangar/Eagle 500KG Bomb.svg", "blobSha": "…" },
  "inputSequence": ["Up", "Right", "Down", "Down", "Down"],  // null = unknown → editor (§6.12)
  "templates": { "rendered": "eagle_500kg_bomb.svg.png", "captured": null }
}
```

- The **seed** contains all stratagems known at build time incl. input sequences, initially taken from `Firmware/libs/macro.py` (read only). Name differences between the firmware and the repository (e.g. `SEAF Artilery` vs. `SEAF Artillery`, `Recoiless Rifle`) are resolved by an alias table in the seed.

Update procedure (`CatalogUpdater`):

1. On program start (and via a button), **at most once per day**: query the repository tree via the GitHub REST API (`GET /repos/{owner}/{repo}/git/trees/master?recursive=1`, unauthenticated, conditional request with `ETag` to save rate limit).
2. Compare every `*.svg` entry by **blob SHA** with the local catalog:
   - **new file** → new catalog entry (id from file name, group from folder, category from SVG color, `inputSequence = null`);
   - **changed SHA** → download the SVG again and re-render the template;
   - **moved/renamed** (same blob SHA, different path) → update the path, keep id and input sequence;
   - **removed** → entry is kept but marked `retired` (stratagems can still appear in old screenshots/history).
3. Download changed SVGs from `raw.githubusercontent.com`, **render** them with Svg.Skia onto the dark slot background at the canonical size (player 64×64, mission 48×48, §6.3.1).
4. Write the catalog atomically (temp file + rename); the recognizer reloads it without restart.
5. Offline or API error → keep the current catalog, log a warning, retry next start.
6. The UI shows a summary ("3 new stratagems, 2 icons updated"); new stratagems without input sequence are highlighted and offered in the editor (§6.12, last phase).

Matching rendered SVG templates against the real game:

- The in-game icons are close to the SVGs (same shapes and category colors), but rendering differs (anti-aliasing, slight glow, slot background). Rendered templates therefore get a slightly lower acceptance threshold and require a larger score margin; captured templates always win.
- P4 measures how well rendered templates alone classify the test screenshots. If insufficient, a preprocessing step (edge/shape-based matching on the category-color mask) is added.

### 6.12 Input sequence editor (last phase)

For stratagems that are new in the catalog (no `inputSequence`), the software offers an editor to define the key sequence. **Implemented in the last phase (P8).** Until then, stratagems without sequence are recognized and shown, but marked as "not transferable" and not sent to the Hellboard.

- Entry by clicking arrow buttons or pressing the arrow keys/WASD; shows the sequence as arrows with the stratagem icon.
- Validation: 3–10 inputs, only Up/Down/Left/Right; warning if the sequence equals another stratagem's sequence.
- Edits of existing sequences are allowed (game updates occasionally change sequences); user-defined sequences are stored as overrides and survive catalog updates.
- Optional later: export/import of user sequences (JSON), so they can be shared or contributed to the seed.
- After saving, the stratagem becomes transferable; if it is part of the current loadout, the loadout is re-sent.

### 6.13 Manual trigger (test mode and user fallback)

> **Implementation status (P4):** implemented as described below: `RecognitionOptions` (defaults; loaded from the configuration in P6), `Hotkey` parsing and `IGlobalHotkeys` with `Win32GlobalHotkeys` (`RegisterHotKey` on its own message thread), `ManualTriggerService` (1 s debounce), burst capture with majority vote in `LoadoutPipeline` (5 frames, 40 ms apart, executed on the pipeline thread), forced recognition when the screen is not detected (with warning and cue scores), test mode, optional saving of the burst frames, "Analyze image file…" in the status window, tray menu entry "Recognize loadout now", and a non-activating notification window over the game. Not implemented yet: sound, the warning about common game key bindings (settings page, P6). A second running instance cannot register the same hotkey and reports this in the status window.

A **global hotkey** triggers the recognition on demand. This makes testing possible without starting a mission (no need to press Ready and deploy) and serves as a fallback for users when the automatic detection fails (e.g. unusual resolution, game update).

Recognition modes (setting, §6.8):

| Mode | Behavior |
|-|-|
| `Automatic` | Only the automatic pipeline (§6.4, §6.5). |
| `ManualOnly` | No automatic lock-in; recognition runs only when the hotkey is pressed. Lowest CPU usage. |
| `AutomaticWithManualTrigger` *(default)* | Automatic pipeline plus hotkey at any time. |

Trigger behavior:

1. Hotkey pressed (default `Ctrl+Shift+F9`, configurable; also available as tray menu entry and as a button in the debug view).
2. The frame source captures a short **burst** (e.g. 5 frames within ~200 ms) of the game monitor; the capture is started on demand if it is not running (`ManualOnly`).
3. The recognizer runs on every frame of the burst; per slot the **majority result** is taken. Screen detection is evaluated but **does not block**: if the loadout screen is not detected confidently, the result is still shown, with a warning and the per-cue scores (useful for analyzing failures).
4. Result → `LockedLoadout` with `source = Manual`, written to the history.
5. **Transfer**: with `manualTransfer = true` (user fallback) the loadout is sent to the Hellboard like an automatic lock-in (only if all player slots were recognized). With `false` (**test mode**) the result is only shown and logged, nothing is sent.
6. Feedback: a short notification / overlay text with the recognized stratagems and the ready status, plus an optional sound.
7. Optional `saveSnapshotOnTrigger`: the burst frames are saved in the test-data format (§8), so every manual test directly produces new test data.

Further points:

- The hotkey is registered via `IGlobalHotkeys` (Windows: `RegisterHotKey`; no keyboard hook, no input injection). It must not collide with game bindings; the settings page warns about common game keys.
- Debounce: a second trigger within 1 s is ignored.
- **Offline analysis** (debug view): "Analyze image file…" runs the same recognition on a PNG (e.g. from `tests/testData/local/`), no game needed at all.

## 7. Implementation Phases

| Phase | Content | Acceptance criteria |
|-|-|-|
| **P0 – Setup** ✔ | Solution, projects (Core, Platform.Windows, App, Desktop.Windows host, Core.Tests), first platform interfaces (`IPlatformPaths`, `IPlatformInfo`), DI composition, `.gitignore`, DPI-aware manifest, Serilog file logging, Avalonia tray skeleton, `build.ps1` incl. `linux-x64` build guard and portability tests. The Simulator project is created in P5. | `build.ps1` succeeds: build without warnings, all tests pass, Core/App/Core.Tests also build for `linux-x64`; app starts as tray icon and writes its log. |
| **P1 – Game & monitor detection** ✔ | `IGameLocator` + `GameSnapshot`/`GameLocation` model in Core, monitor selection and window-mode rules (unit tested), `Win32GameLocator` with WinEvent hooks and 1 s polling on a per-monitor-DPI-aware thread, game section in the status window. | With 2+ monitors: correct monitor reported when the game starts, after dragging the window to another monitor and after changing resolution; game exit/restart detected. *Accepted 2026-10-15* by manual test with two monitors (2560×1440 @ 125 % primary, 1920×1080 at negative coordinates): fullscreen/borderless and windowed, monitor change, focus, minimize, resolution change, exit and restart. |
| **P2 – Capture** ✔ | `Frame`, `IFrameSource`, `GameFrameGrabber` in Core; `DesktopDuplicationFrameSource`; live preview (2 frames/s while the status window is open), black-frame warning, "Save snapshot" (PNG to `%LOCALAPPDATA%\HellboardCompanion\snapshots`) and "Open snapshot folder" in the status window. | Snapshots match the game monitor at native resolution; ≤ 3 % CPU at 2 frames/s; works in borderless and windowed mode. *Accepted 2026-10-15:* 0.25 % CPU; preview, snapshot and snapshot folder checked by the user in borderless and windowed mode, incl. minimize and monitor change; no black frames. |
| **P3 – Test data & layout** ✔ | Measure ROIs at native resolution, `assets/layouts/loadout-v1.json` (embedded), `LoadoutLayout` + `LayoutScaler` (`min(W/1920, H/1080)`, top-left anchor) with unit tests. Test data in `tests/testData/local/` (local only): Fori Prime and Partion missions, empty and full loadouts, Prepare/Ready, 1 optional mission stratagem, 1920×1200 window, settings menu as negative example. Synthetic resolutions (720p–4K) are generated **in memory** by the P4 recognition tests from the native captures, so no derived game images are written to disk. The template library moves to P4 (SVG rendering + captured templates, §6.11). | Layout overlays match all local captures (16:9 native and 16:10 window) pixel-exactly; scaler unit tests for 720p–4K and secondary monitors. *Accepted 2026-10-15.* |
| **P4 – Recognition & lock-in** *(recognition, tracker, pipeline, history, manual trigger and file analysis implemented; automatic lock-in **live-tested in the game (solo) on 2026-10-15**; open: live test of the manual trigger; Ready→Prepare→Ready only covered by unit/replay tests – live test needs a multiplayer session)* | Screen detector, slot classifier, ready-status detector, `LoadoutTracker`, history, **manual trigger and offline image analysis (§6.13)** – built early in this phase to simplify testing. | Offline: 100 % correct slot classification and screen detection on native and synthetic test sets **from 720p to 4K** at default thresholds, no false lock-in outside the loadout screen. Replay of "last player confirms" sequences locks in the correct loadout every time; Ready→Prepare→Ready sequences with a changed loadout produce a second transfer, without a change no second transfer. Lock-in latency within targets (§6.5) at 4K. Manual trigger: hotkey on the loadout screen shows the correct loadout within 500 ms; test mode sends nothing. |
| **P5 – Hellboard link (PC side)** ✔ | `HellboardClient` (JSON lines, id/ack, 500 ms timeout, 3 retries, non-protocol output ignored), `SerialHellboardConnection` (System.IO.Ports), `RegistrySerialPortDiscovery` (USB VID 2E8A), in-process `SimulatedHellboard` (replaces the separate simulator console app), `HellboardLink` service (connect, heartbeat, resend after reconnect/`hello`, send only changed loadouts, release the port of boards without protocol and re-check every 30 s, dry-run mode), `LoadoutTransferBuilder` (full names, multi-function modes, not-transferable report), seed catalog `assets/catalog.seed.json` (109 stratagems, 83 with input sequences from `Firmware/libs/macro.py`), Hellboard section with "Set as default" in the status window. | Unit tests against the simulator: transfer, no resend of unchanged loadouts, resend after board restart, flash written only via "Set as default" (F3), board without protocol reported, dry run. *Done 2026-10-16.* Live check with the user's board on the old firmware: detected as "no protocol" after ~2 s, port released while the Companion keeps running (found and fixed: a blocking write of the old firmware left the port open). Note: while a board without protocol is connected, the Companion briefly opens the port every 30 s; tools like MicroPico may fail to connect during these ~3 s. |
| **P6 – Polish & release** | Catalog updater (§6.11) incl. SVG rendering and seed catalog, third-party notices, history page, settings, autostart, single-file self-contained publish, README. | `HellboardCompanion.exe` runs on a clean Windows 11 machine without installed .NET (Windows 10: best effort, §3.2); a new SVG in the repository appears in the catalog after the next start; changed icons are re-rendered; offline start works with the cached catalog. |
| **P7 – Firmware extension & updater** *(runs alongside P5, §2.1)* | On the existing MicroPython v1.17 build (decisions F1–F9 agreed, §6.9; **board spike passed 2026-10-16**, see hellboard-protocol.md §8): finalize `hellboard-protocol.md`, implement the firmware side (protocol handling, button assignment, display/LED feedback, "Set as default" persistence (F3), `hid` module as the only user of the HID API (prepares P7b)), firmware updater (§6.10) with bundle/manifest, verification and rollback, update the firmware setup instructions. | End-to-end with the real Hellboard: locked loadout appears on the buttons and the display within 1 s; re-sent after reconnect; the updater installs a new firmware bundle, verifies it and rolls back on a simulated failure (interrupted transfer, wrong hash); all agreed decisions implemented. Changes in `Firmware/` only after the respective decision was agreed. |
| **P7b – MicroPython switch (F10)** *(isolated, after P7)* | Own branch and own firmware bundle. Build/obtain the official MicroPython UF2 for RP2040, port the `hid` module to `usb-device-keyboard`, keep everything else unchanged. Test plan on the real board: (1) HID and CDC enumerate together on Windows, (2) every stratagem macro works in the game incl. modifier hold/toggle and key timing, (3) protocol, "Set as default", display and LEDs unchanged, (4) unplug/replug and PC sleep/resume, (5) updater: install the new UF2 + files and **roll back to v1.17** (old UF2 kept in the bundle; BOOTSEL recovery documented). | All tests of the checklist pass on the user's board; v1.17 rollback verified; only then the new build becomes the default in the firmware bundle. |
| **P8 – Input sequence editor** *(last phase)* | Editor for input sequences of new stratagems (§6.12), overrides that survive catalog updates, validation, optional export/import. | A stratagem added to the catalog without sequence can be given a sequence in the UI, becomes transferable and is sent to the Hellboard. |

## 8. Testing Strategy

- **Local scene tests** *(P4)*: `tests/testData/scenes.json` (committed) holds the expected results for the screenshots in `tests/testData/local/` (not committed). `LocalScenes` loads a scene, crops the client area and rescales it in memory to 720p, 900p, 1080p and 2160p (`INTER_AREA` / `INTER_CUBIC`); tests are skipped when the image is missing. The ship management and settings screenshots serve as negative examples for the screen detection.

- **Unit tests**: ROI scaling/anchoring, monitor selection (fake layouts incl. negative coordinates and mixed DPI), tracker state machine (stable candidate, Prepare→Ready, Ready→Prepare→Ready with and without changes, screen exit, partial results), protocol serialization.
- **Recognition regression tests**: `tests/testData/<resolution>/<scene>.png` + `<scene>.expected.json` (screen visible, 4 slot ids, status Prepare|Ready). Each scene runs at native resolution and at the synthetic resolutions 720p, 900p, 1080p and 4K (§6.3.1).
- **Replay tests**: recorded frame sequences fed through a fake `IFrameSource`, especially the case "local player confirms last and the screen closes within a few frames".
- **Manual checklist**: game on primary/secondary monitor, different scaling, borderless/windowed/fullscreen, alt-tab, solo and 4-player lobby, Hellboard unplugged/replugged.

## 9. Risks and Mitigations

| Risk | Mitigation |
|-|-|
| Very short time between confirmation and screen exit. | Continuous recognition with stable candidate before confirmation, event-driven capture at up to 30 fps, ring buffer re-evaluation on screen exit (§6.5). |
| Slight layout deviations (resolution rounding, aspect ratio, UI scale). | Search windows ±2 % of client height, ±5 % scale, global offset correction from the anchor icons (§6.3). |
| Developers change the fixed mission stratagems or the card content. | Screen detection uses only structural cues (ready button, slot grid, card frame); mission icons are optional evidence (§6.4). |
| Resolutions without real test captures (720p, 1080p, 4K, non-16:9) render the UI slightly differently. | Canonical-size classification, scale-relative thresholds, synthetic test sets, per-cue scores in the status page, real captures added to the regression set when available. |
| Game language changes texts. | Only icons and colors are used for recognition, no text. |
| Similar-looking stratagem icons (e.g. Mortar Sentry vs. Gas Mortar Sentry, variants of the same weapon family). | Color matching, score margin check, `Unknown` instead of a wrong guess; dedicated test cases for each known look-alike pair. |
| Game updates change layout or icons. | Layout and templates are data; template tool for quick re-capture. |
| Exclusive fullscreen yields black frames. | Desktop Duplication fallback; recommend borderless; detect and warn. |
| Anti-cheat (nProtect GameGuard). | Only official OS capture APIs; no process access beyond name/PID/window; no input to the game. |
| Interrupted or faulty firmware update leaves the board unusable. | Write to temporary files, SHA-256 verification, atomic rename, `*.bak` rollback, version check after restart; manual BOOTSEL recovery documented. |
| Firmware not ready when the PC side is. | Dry-run and simulator; protocol specified jointly early (P5/P7). |

## 10. Open Questions

Resolved: Linux is not a goal, but the design keeps a port as easy as possible (Avalonia, platform-neutral Core, §3.1). The local player's card is always the leftmost card, other players are to the right and are ignored; player stratagems go to `button_2_1 … button_2_4`, optional mission stratagems go to the multi-function button of the upper row; the content of the multi-function list is user-selectable (fixed list / automatic detection / both, §6.6).

1. **Further status states** *(to be checked later by the user)*: Are there other states of the ready button besides `Prepare` and `Ready` (e.g. disabled, countdown when all players are ready)? Until then, anything else is classified as `Unknown` and does not change the tracker state.
2. **Test data**: Available in `Software/tests/testData/local/`: native 2560×1440 (Prepare, Ready, loadout selected, masked layout, lower render resolution) and 1920×1200 windowed (16:10, empty loadout). Further output resolutions cannot be provided at the moment → synthetic test sets (§6.3.1); real captures at 720p/1080p/4K and 21:9 are added later. Still missing: optional mission stratagems present (1–4), squad lobby with other players.
- **Stratagem coverage (decided 2026-10-15):** P4 is implemented and tested with the stratagems available to the user now (89 ship/warbond stratagems with German names in [stratagem-names-de.md](stratagem-names-de.md); not all are unlocked, so captured in-game icons exist only for some). Missing stratagems (mission stratagems, locked warbond items) are added later from additional screenshots – this doubles as the real-world test of the "add new stratagem" workflow (§6.11, §6.12).
3. **Icon repository license**: `nvigneux/Helldivers-2-Stratagems-icons-svg` declares no license. **Agreed:** the icons are used read-only as templates and downloaded at runtime; they are not committed or bundled. Asking the author is only needed if this ever changes.
4. ~~License~~ – decided: GPL-3.0-or-later (§3.3).
5. **Firmware decisions F1–F9** (§6.9) – to be discussed before P5/P7.
