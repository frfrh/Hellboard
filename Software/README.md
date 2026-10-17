# Hellboard Companion

Windows companion app for the [Hellboard](../README.md): it watches the Helldivers 2 loadout screen, recognizes the selected stratagems and transfers them to the Hellboard.

> Status: released (0.6.0) – all phases of the [implementation plan](doc/spec/implementation-plan.md) are done except P7b (switch to the official MicroPython).

## Requirements

- Windows 11 (Windows 10 2004+ on a best-effort basis)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.400 or newer, see `global.json`)
- IDE: JetBrains Rider (recommended) or Visual Studio 2026; the build itself only needs the `dotnet` CLI

## Build and run

```powershell
.\build.ps1                    # build, test, Linux portability guard
.\build.ps1 -SkipLinuxGuard    # faster local build
dotnet run --project src/HellboardCompanion.Desktop.Windows
```

The app starts in the system tray. Left-click the tray icon or choose *Open* to show the window; *Exit* ends the app.
Logs are written to `%LOCALAPPDATA%\HellboardCompanion\logs`.

## Release package

```powershell
.\publish.ps1                  # build, test, publish, zip and installer
.\publish.ps1 -SkipTests
.\publish.ps1 -SkipInstaller   # without Inno Setup
```

Creates `publish/HellboardCompanion-<version>-win-x64.zip` with one self-contained `HellboardCompanion.exe` (no .NET installation needed), the license, [third-party notices](THIRD-PARTY-NOTICES.md) and the [beta readme](BETA-README.md), and the installer `publish/HellboardCompanion-<version>-setup.exe` ([`installer/HellboardCompanion.iss`](installer/HellboardCompanion.iss), needs [Inno Setup 7](https://jrsoftware.org/isdl.php)). The installer offers "install for me only" (`%LOCALAPPDATA%\Programs`, no admin rights) or "for all users" (Program Files), creates a Start menu entry, installs over older versions and removes the autostart entry on uninstall; user data stays.

## Project structure

| Project | Target | Purpose |
|-|-|-|
| `src/HellboardCompanion.Core` | `net10.0` | Platform-neutral logic and platform interfaces |
| `src/HellboardCompanion.App` | `net10.0` | Platform-neutral Avalonia UI (tray app, windows) |
| `src/HellboardCompanion.Platform.Windows` | `net10.0-windows` | Windows implementations of the platform interfaces |
| `src/HellboardCompanion.Desktop.Windows` | `net10.0-windows` | Windows host, builds `HellboardCompanion.exe` |
| `tests/HellboardCompanion.Core.Tests` | `net10.0` | Unit tests, incl. portability guard |

Core and App must not use Windows-only APIs; this is checked by `PortabilityTests` and by the `linux-x64` build in `build.ps1`.

## Test data

Screenshots of the game are copyrighted game content. Put them into `tests/testData/local/` – this folder is ignored by git and is never committed.

## Knowledge base

Reference pages for development and for extending the stratagem catalog (input codes, names, designations). They are looked up **manually** by developers; the software never queries them.

| Source | Content | Notes |
|-|-|-|
| [Helldivers Wiki (wiki.gg) – Stratagems](https://helldivers.wiki.gg/wiki/Stratagems) | 116 stratagems with input code, incl. rare mission objectives (Portable Comms Relay, Tactical Video Camera, E-711 Extraction Drill, Aquifer Drill) | Most complete source (checked 2026-10-16). Lists SSSD Delivery twice (↓↓↓↑↑ and ↓↓↓↓↓↑↑). Reinforcement Pods share the code of Upload Data. |
| [DiversHub – Stratagems](https://divershub.app/de/database/stratagems) | 111 stratagems with input code, cooldown and full designation (German UI) | Checked 2026-10-16. Differs from wiki.gg for Cremator (DiversHub ↓←↑←↑↑ = code of GL-28, wiki.gg ↓↓→↓↑↑) and TD-110 Maelstrom (DiversHub starts with ↑, wiki.gg with ← like all other vehicles). |
| [Helldivers Wiki (Fandom) – Stratagem Codes](https://helldivers.fandom.com/wiki/Stratagem_Codes) | 81 stratagems with input code, cooldown and designation | Missing newer Warbonds. SSSD Delivery differs from DiversHub. |
| [Icon repository nvigneux/Helldivers-2-Stratagems-icons-svg](https://github.com/nvigneux/Helldivers-2-Stratagems-icons-svg) | Stratagem SVG icons, the ids and English names of the catalog | Downloaded at runtime by the catalog updater (implementation plan §6.11). |
| [`doc/spec/stratagem-names-de.md`](doc/spec/stratagem-names-de.md) | English ↔ German names read from the game | Own notes. |

The input codes used by the project are maintained in one place only: [`doc/stratagems.json`](../doc/stratagems.json) in the repository root (format and decisions: [`doc/stratagems.md`](../doc/stratagems.md)). New or corrected codes go there first (compare the sources above; when they disagree, check the code in the game and note decisions there); the software and the firmware take their codes from that list. The companion embeds the file at build time and builds its stratagem catalog from it (`StratagemCatalog`); `StratagemCatalogTests` check that it can be read. Users can add or correct stratagems without a new release in `%APPDATA%\HellboardCompanion\stratagems.json` (same format, entries replace or extend the built-in list; see `doc/stratagems.md`).

## License

Hellboard Companion is licensed under the [GNU General Public License v3.0 or later](LICENSE) (`GPL-3.0-or-later`). Third-party components keep their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
