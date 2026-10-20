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

On the first start the app needs an internet connection: it reads the stratagem list of the
[Helldivers wiki](https://helldivers.wiki.gg/wiki/Stratagems) (new stratagems and their input codes) and
downloads the stratagem icons it uses for recognition from there. Both are checked again at most once a day. The first start also takes a few seconds longer because the bundled
native libraries are unpacked.

The app opens its window and keeps running in the system tray; left-click the tray icon to open the window
again. A few seconds after the start the app looks on GitHub for a newer version; if there is one, it asks
whether to open the download page in your browser (with only the tray icon shown, Windows shows a
notification first). You can skip a version or turn the check off in *Settings → General*.
In *Settings → General* you can also let it start with only the tray icon, and let the close button (X)
exit the app instead of keeping it in the tray.

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

## If the recognition does not work

Click a stratagem tile in the *Overview* and pick the stratagem (search, categories, recently used). It is
sent to the Hellboard at once, just like a recognized loadout; the tile shows *set by hand*. This also
corrects a single wrongly recognized slot. A stratagem from another slot swaps the two slots.

A choice by hand stays until the game shows a different loadout: if the same wrong recognition comes
again, your correction is kept. *Back to recognized* drops the choices by hand and sends the loadout the
recognition locked in last.

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
