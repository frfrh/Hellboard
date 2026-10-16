# Stratagems

[`stratagems.json`](stratagems.json) is the **single source of truth for the stratagem input codes of the Hellboard project.** The firmware (`Firmware/`) and the companion software (`Software/`) take their stratagem data from this file only. A stratagem or code is added or corrected there and nowhere else.

> Status: the companion software embeds `stratagems.json` at build time and builds its stratagem catalog (names, LED colors, codes) from it; its unit tests fail if the file is invalid. The firmware gets the file unchanged on the board (`Firmware/manifest.json`) and reads it line by line; its `config.json` refers to stratagems by `id` (decision F11).

## User list (no new release needed)

Users can add stratagems or correct codes themselves in `%APPDATA%\HellboardCompanion\stratagems.json` (Linux: `~/.config/HellboardCompanion/stratagems.json`). The Companion creates it as an empty template on the first start. Same format as this file; an entry with the id of a built-in stratagem **replaces** it, a new id **adds** a stratagem, user categories add to the built-in ones. The built-in list still updates with every release, so corrections reach users who have their own list. An invalid user list is ignored with a warning in the log; changes apply after a restart. Corrections that are useful for everyone should come back into this file.

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
| `id` | Stable key, lowercase `a-z0-9_`. Equals the icon id of the companion software (file name slug in the icon repository `nvigneux/Helldivers-2-Stratagems-icons-svg`). Never change an existing id. |
| `name` | Short English name (icon repository name). Sent to the Hellboard in full; shortened only on the display. |
| `designation` | Full in-game designation, for reference. |
| `category` | `Orbital`, `Eagle`, `Support weapon`, `Backpack`, `Vehicle`, `Defensive` or `Mission`. |
| `code` | Input code as letters `U` `D` `L` `R` (↑ ↓ ← →), 1–10 arrows; `null` if not known yet – the software then shows the stratagem but cannot send it to the Hellboard. |
| `notes` | Unverified or disputed codes, missing icons and other remarks; `null` if the code agrees in every source that lists it. |

## Rules

- Every code must be unique among the stratagems a player can have at the same time. Known overlap: Reinforcement Pods and Upload Data (same code in all sources).
- A code taken from only one source or disputed between sources gets a note until it is checked in the game.
- Sources for looking up codes are listed in the knowledge base of [`Software/README.md`](../Software/README.md#knowledge-base). They are only read manually; no part of the project queries them automatically.

## Decisions

- **SSSD Delivery:** `DDDUU` (Fandom wiki) until further notice; the 7-arrow variant `DDDDDUU` (wiki.gg, DiversHub) is considered too long. *(2026-10-16)*
- **Cremator:** code from wiki.gg, unverified (DiversHub differs). *(2026-10-16)*
- **TD-110 Maelstrom:** `LDRDLDULR` verified in game (wiki.gg correct, DiversHub wrong). *(2026-10-16)*

Checked against helldivers.wiki.gg, DiversHub and the Fandom wiki on 2026-10-16: 115 stratagems.
