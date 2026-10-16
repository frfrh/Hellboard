# Hellboard Companion – beta

Thank you for testing! Hellboard Companion watches the Helldivers 2 loadout screen, recognizes your
four stratagems (and the optional mission stratagems) and sends them to the Hellboard.

## Installation

1. Unzip the archive into any folder, e.g. `C:\Tools\HellboardCompanion`.
2. Start `HellboardCompanion.exe`. No installation and no .NET runtime are needed – the EXE is self-contained.
3. Windows SmartScreen may warn that the app is from an unknown publisher (the beta is not code-signed).
   Choose *More info* → *Run anyway*.

On the first start the app needs an internet connection: it downloads the stratagem icons it uses for
recognition from GitHub (`nvigneux/Helldivers-2-Stratagems-icons-svg`). The first start also takes a few
seconds longer because the bundled native libraries are unpacked.

The app runs in the system tray. Left-click the tray icon to open the window.

## Requirements

- Windows 11 (Windows 10 version 2004 or newer should work)
- Helldivers 2 in **fullscreen or borderless** mode; resolutions from 1280×720 to 3840×2160
- Optional: a Hellboard connected via USB. Without one, the app still shows what it recognizes.
  Hellboards with the current firmware are detected but reported as "no protocol" until the new
  firmware is released.

## Usage

- Open the loadout screen in the game. The app recognizes the selection and locks it in when you press
  *Ready* (or when the screen closes).
- Manual trigger: **Ctrl+Shift+F9** recognizes the current screen immediately (changeable in *Settings*).
- *History* shows the locked loadouts and recognition problems, including a picture of the slot that was
  not recognized.

## Reporting problems

Please include:

- the full version from *Diagnostics* → *Application* → *Copy*,
- your game resolution and window mode,
- what you did and what happened, and if possible a screenshot from the *History* tab.

Useful files:

| What | Where |
|---|---|
| Logs | `%LOCALAPPDATA%\HellboardCompanion\logs` |
| Problem images | `%LOCALAPPDATA%\HellboardCompanion\problems` |
| Settings | `%APPDATA%\HellboardCompanion\config.json` |

Screenshots of the game show copyrighted game content – share them only with the developers for bug
analysis.

## Uninstall

Enable autostart only if you want it (*Settings*); disable it again before deleting the folder. Then delete
the app folder and the two folders `%LOCALAPPDATA%\HellboardCompanion` and `%APPDATA%\HellboardCompanion`.

## License

Hellboard Companion is free software under the GNU General Public License v3.0 or later (`LICENSE.txt`).
Third-party components are listed in `THIRD-PARTY-NOTICES.md`.
