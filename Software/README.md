# Hellboard Companion

Windows companion app for the [Hellboard](../README.md): it watches the Helldivers 2 loadout screen, recognizes the selected stratagems and transfers them to the Hellboard.

> Status: beta (phase P6 – polish & release). See the [implementation plan](doc/spec/implementation-plan.md).

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
.\publish.ps1               # build, test, publish, zip
.\publish.ps1 -SkipTests
```

Creates `publish/HellboardCompanion-<version>-win-x64.zip` with one self-contained `HellboardCompanion.exe` (no .NET installation needed), the license, [third-party notices](THIRD-PARTY-NOTICES.md) and the [beta readme](BETA-README.md).

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

## License

Hellboard Companion is licensed under the [GNU General Public License v3.0 or later](LICENSE) (`GPL-3.0-or-later`). Third-party components keep their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
