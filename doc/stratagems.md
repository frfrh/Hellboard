# Stratagems

[`stratagems.json`](stratagems.json) is the **single source of truth for the stratagem input codes of the Hellboard project.** The firmware (`Firmware/`) and the companion software (`Software/`) take their stratagem data from this file only. A stratagem or code is added or corrected there and nowhere else.

> Status: the companion software embeds `stratagems.json` at build time and builds its stratagem catalog (names, LED colors, codes) from it; its unit tests fail if the file is invalid. The firmware gets the file unchanged on the board (`Firmware/manifest.json`) and reads it line by line; its `config.json` refers to stratagems by `id` (decision F11).

## User list (no new release needed)

Users can add stratagems or correct codes themselves on the *Stratagems* tab of the Companion, which writes `%APPDATA%\HellboardCompanion\stratagems.json` (Linux: `~/.config/HellboardCompanion/stratagems.json`). The Companion creates it as an empty template on the first start. Same format as this file; an entry with the id of a built-in stratagem **replaces** it, a new id **adds** a stratagem, user categories add to the built-in ones. The built-in list still updates with every release, so corrections reach users who have their own list. An invalid user list is ignored with a warning in the log; changes made in the Companion apply immediately, changes made by hand after a restart. Corrections that are useful for everyone should come back into this file.

## Format

UTF-8 JSON, **exactly one stratagem object per line**. This is a hard rule, not only for readable diffs: the firmware reads the file line by line to save RAM (decision F11), and a unit test of the companion checks it. Do not reformat the file with a JSON formatter.

```json
{
  "version": 1,
  "categories": {"Orbital": "red", "Eagle": "red", ...},
  "stratagems": [
    {"id": "eagle_500kg_bomb", "name": "Eagle 500KG Bomb", "designation": "Eagle 500kg Bomb", "category": "Eagle", "code": "URDDD", "notes": null},
    ...
  ]
}
```

| Field | Meaning |
|-|-|
| `version` | Format version, currently `1`. Incremented only on incompatible changes. |
| `categories` | Category → LED color (`red`, `blue`, `green`, `yellow`). Every stratagem's `category` must be listed here. |
| `id` | Stable key, lowercase `a-z0-9_`. Existing ids are slugs of the names in the former icon repository `nvigneux/Helldivers-2-Stratagems-icons-svg`; a new stratagem gets the slug of its icon name in the wiki (`stratagem-sync` suggests it). Never change an existing id (the firmware configuration, user lists, reference images and the history refer to it). |
| `name` | Short name: `designation` without its model prefix (see *Names*). Sent to the Hellboard in full (shortened only on its display) and used for sorting in the Companion. |
| `designation` | Full name = **page name in the [Helldivers wiki](https://helldivers.wiki.gg/wiki/Stratagems)**, exactly (e.g. `B/FLAM-80 Cremator`, `SoS Beacon`). Shown in the Companion; used to match the wiki's stratagem table. |
| `category` | `Orbital`, `Eagle`, `Support weapon`, `Backpack`, `Vehicle`, `Defensive` or `Mission`. |
| `code` | Input code as letters `U` `D` `L` `R` (↑ ↓ ← →), 1–10 arrows; `null` if not known yet – the software then shows the stratagem but cannot send it to the Hellboard. |
| `notes` | Unverified or disputed codes, missing icons and other remarks; `null` if the code agrees in every source that lists it. |

## Names

`designation` is the full name from the wiki, `name` is derived from it: the first match of the regular expression

```
^([[:alnum:]/\\]{1,10}-[[:alnum:]]{1,10})
```

(model prefix plus one space) is removed **once**: `B/FLAM-80 Cremator` → `Cremator`, `AX/LAS-5 Rover` → `Rover`, `E/AT-12 Anti-Tank Emplacement` → `Anti-Tank Emplacement`, `GL-28 Belt-Fed Grenade Launcher` → `Belt-Fed Grenade Launcher`. Names without such a prefix stay as they are (`Orbital Laser`, `B/MD C4 Pack`). Hardly any player knows a stratagem by its model, so the short name is what the Hellboard shows and what the Companion sorts by; the Companion shows the full name. `StratagemCatalog.ShortName` implements the rule; a unit test checks that every `name` follows it and that no two names are equal. Users can still choose another name in their own list.

## Rules

- Every code must be unique among the stratagems a player can have at the same time. Known overlap: Reinforcement Pods and Upload Data (same code in all sources).
- **The Helldivers wiki ([helldivers.wiki.gg](https://helldivers.wiki.gg/wiki/Stratagems)) is the authoritative source** for codes, designations and categories (decided 2026-10-19: more reliable and maintained faster than the other sources). Before a release, run [`stratagem-sync`](../Software/tools/stratagem-sync/README.md): it compares this file with the wiki's stratagem table and suggests lines for new stratagems. Take over what is right; differences that stay (decisions below) are noted here.
- A code checked in the game wins over every source; it gets a note with the date.
- Other sources (knowledge base of [`Software/README.md`](../Software/README.md#knowledge-base)) are only looked up manually, e.g. to check a disputed code.

## Decisions

- **SSSD Delivery:** `DDDUU` (Fandom wiki) until further notice; the 7-arrow variant `DDDDDUU` (wiki.gg, DiversHub) is considered too long. *(2026-10-16)*
- **Cremator:** `DDRDUU` verified in game (wiki.gg correct, DiversHub wrong). *(2026-10-18)*
- **TD-110 Maelstrom:** `LDRDLDULR` verified in game (wiki.gg correct, DiversHub wrong). *(2026-10-16)*

Checked against helldivers.wiki.gg, DiversHub and the Fandom wiki on 2026-10-16: 115 stratagems. Names and designations aligned with helldivers.wiki.gg on 2026-10-19 (`stratagem-sync`: only SSSD Delivery differs, as decided; AMW-20 Apex and LAS-44 Nova have no code yet).
