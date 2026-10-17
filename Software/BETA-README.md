# Hellboard Companion – beta

Thank you for testing! Hellboard Companion watches the Helldivers 2 loadout screen, recognizes your
four stratagems (and the optional mission stratagems) and sends them to the Hellboard.

## Installation

**With the installer** (`HellboardCompanion-<version>-setup.exe`, recommended): run it and choose
*Install for me only* (no administrator rights) or *Install for all users*. It creates a Start menu entry
(optionally a desktop icon), and a newer version simply installs over the old one.

**Without installing** (`HellboardCompanion-<version>-win-x64.zip`): unzip the archive into any folder,
e.g. `C:\Tools\HellboardCompanion`, and start `HellboardCompanion.exe`.

No .NET runtime is needed either way – the EXE is self-contained. Windows SmartScreen may warn that the
app is from an unknown publisher (it is not code-signed). Choose *More info* → *Run anyway*.

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
  not recognized. Choose there which stratagem it is and click *Use as reference*: the picture is stored on
  your PC and replaces the icon of that stratagem in the recognition (useful when the game draws an icon
  differently, e.g. with a small badge, or the icon is wrong). The *Stratagems* tab shows and removes these
  reference images.

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
| Own stratagems / code corrections | Tab *Stratagems* (stored in `%APPDATA%\HellboardCompanion\stratagems.json`) |

Screenshots of the game show copyrighted game content – share them only with the developers for bug
analysis.

## Uninstall

Installed: uninstall it in *Settings → Apps* (this also removes the autostart entry). Unzipped: disable
autostart in *Settings* first, then delete the folder. Your settings, own stratagems and logs stay in
`%APPDATA%\HellboardCompanion` and `%LOCALAPPDATA%\HellboardCompanion`; delete these two folders as well
to remove everything.

## License

Hellboard Companion is free software under the GNU General Public License v3.0 or later (`LICENSE.txt`).
Third-party components are listed in `THIRD-PARTY-NOTICES.md`.
