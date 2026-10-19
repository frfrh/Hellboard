# Hellboard Companion

Windows companion app for the [Hellboard](../README.md): it watches the Helldivers 2 loadout screen, recognizes the selected stratagems and transfers them to the Hellboard.

> Status: released (0.7.0) – all phases of the [implementation plan](doc/spec/implementation-plan.md) are done except P7b (switch to the official MicroPython).

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

The app opens its window and keeps running in the system tray. Left-click the tray icon or choose *Open* to show the window; *Exit* ends the app. *Settings → General* sets whether it starts with only the tray icon and whether the close button (X) exits the app instead of keeping it in the tray.
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

Reference pages for development and for extending the stratagem catalog (input codes, names, designations). **helldivers.wiki.gg is the authoritative source** (decided 2026-10-19): [`tools/stratagem-sync`](tools/stratagem-sync/README.md) reads its stratagem table through the MediaWiki API and compares it with the catalog; the Companion uses the same client to pick up new stratagems, missing codes and the stratagem icons at runtime and proposes different codes in its Stratagems tab ([plan](doc/spec/wiki-source-plan.md)). The other pages are looked up **manually** only.

| Source | Content | Notes |
|-|-|-|
| [Helldivers Wiki (wiki.gg) – Stratagems](https://helldivers.wiki.gg/wiki/Stratagems) | 118 stratagems with input code, designation, type and icon (Cargo table `Stratagems`), incl. rare mission objectives (Portable Comms Relay, Tactical Video Camera, E-711 Extraction Drill, Aquifer Drill) | **Authoritative** (2026-10-19). Most complete and most correct source; Cremator and TD-110 Maelstrom verified in game. Lists SSSD Delivery twice (↓↓↓↑↑ and ↓↓↓↓↓↑↑). Reinforcement Pods share the code of Upload Data. Content under CC BY-NC-SA 4.0. |
| [DiversHub – Stratagems](https://divershub.app/de/database/stratagems) | 111 stratagems with input code, cooldown and full designation (German UI) | Checked 2026-10-16. Wrong for Cremator (DiversHub ↓←↑←↑↑ = code of GL-28; ↓↓→↓↑↑ of wiki.gg verified in game 2026-10-18) and TD-110 Maelstrom (DiversHub starts with ↑; wiki.gg with ← verified in game). |
| [Helldivers Wiki (Fandom) – Stratagem Codes](https://helldivers.fandom.com/wiki/Stratagem_Codes) | 81 stratagems with input code, cooldown and designation | Missing newer Warbonds. SSSD Delivery differs from DiversHub. |
| [Icon repository nvigneux/Helldivers-2-Stratagems-icons-svg](https://github.com/nvigneux/Helldivers-2-Stratagems-icons-svg) | Stratagem SVG icons | **Former** icon source (until 2026-10-19); the ids of the catalog are slugs of its names. Replaced by the wiki icons ([plan](doc/spec/wiki-source-plan.md) §4). |
| [`doc/spec/stratagem-names-de.md`](doc/spec/stratagem-names-de.md) | English ↔ German names read from the game | Own notes. |

The input codes used by the project are maintained in one place only: [`doc/stratagems.json`](../doc/stratagems.json) in the repository root (format, naming rule and decisions: [`doc/stratagems.md`](../doc/stratagems.md)). New or corrected codes go there first (run `stratagem-sync` against the wiki; when sources disagree, check the code in the game and note decisions there); the software and the firmware take their codes from that list. The companion embeds the file at build time and builds its stratagem catalog from it (`StratagemCatalog`); `StratagemCatalogTests` check that it can be read. Between the built-in list and the user's list sits the wiki layer: the last stratagem table read from the wiki (`%LOCALAPPDATA%\HellboardCompanion\wiki\stratagems.json`, checked at most once a day, `WikiStratagemStore`). Users can add or correct stratagems without a new release in `%APPDATA%\HellboardCompanion\stratagems.json` (same format, entries replace or extend the built-in list and win over the wiki; see `doc/stratagems.md`).

## Online access

At runtime the Companion queries only two services: the stratagem table and icons of **helldivers.wiki.gg** (at most once a day, [plan](doc/spec/wiki-source-plan.md)) and the **GitHub releases** of `frfrh/Hellboard` at start to find a newer Companion (`CompanionUpdateChecker`; can be turned off in the settings). Companion releases are tagged `vX.Y.Z_companion`; other parts of the project use other tags.

## Acknowledgements

A big thank you to the editors of the **[Helldivers Wiki](https://helldivers.wiki.gg/)** on wiki.gg. Their carefully maintained stratagem pages – input codes, designations and the traced stratagem icons – are the foundation of the Companion's stratagem catalog and proved more reliable and better maintained than any other source we compared. Wiki content is licensed under [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/).

## License

Hellboard Companion is licensed under the [GNU General Public License v3.0 or later](LICENSE) (`GPL-3.0-or-later`). Third-party components keep their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
