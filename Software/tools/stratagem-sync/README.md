# stratagem-sync

Compares the stratagem catalog [`doc/stratagems.json`](../../../doc/stratagems.json) with the stratagem table of the [Helldivers wiki](https://helldivers.wiki.gg/wiki/Stratagems), the authoritative source of codes, designations and names (decided 2026-10-19, [plan](../../doc/spec/wiki-source-plan.md)). Development tool for maintaining the built-in catalog before a release; it reads the wiki with the same Core client as the Companion (`WikiClient`).

## What it does

1. Reads the Cargo table `Stratagems` of helldivers.wiki.gg through the MediaWiki API (one request).
2. Matches every wiki row to a catalog entry by its page name (= `designation`).
3. Prints the differences:
   - different codes (with the catalog note, e.g. a decision or a check in the game),
   - several codes on the wiki for one stratagem,
   - designations that are not the wiki page name,
   - names that do not follow the naming rule (`designation` without model prefix, [`doc/stratagems.md`](../../../doc/stratagems.md#names)),
   - different categories,
   - stratagems on the wiki that are missing in the catalog, each with a suggested catalog line,
   - catalog entries the wiki does not list.

The catalog is never changed: check each difference and enter corrections by hand (decisions go into `doc/stratagems.md`).

## Usage

From the repository root:

```powershell
dotnet run Software/tools/stratagem-sync/stratagem-sync.cs
dotnet run Software/tools/stratagem-sync/stratagem-sync.cs -- --catalog path/to/stratagems.json
```

Exit code: `0` no differences, `1` differences found, `2` the wiki could not be read (offline, or the wiki changed its table – then `WikiClient` needs an update).
