# Plan: helldivers.wiki.gg as stratagem source

Status: **implemented** (2026-10-19, steps 1–4). Replaces the icon repository `nvigneux/Helldivers-2-Stratagems-icons-svg` and makes helldivers.wiki.gg the single online source for stratagem codes, names and icons. Changes implementation plan §3.3, §6.11 and §6.12 once implemented.

## 1. Decision and background

- **Decided 2026-10-19 (user):** helldivers.wiki.gg is the authoritative source for codes, designations and icons, and the Companion may query it at runtime. nvigneux is a fan project as well (no license declared); the wiki proved more reliable (Cremator, TD-110 Maelstrom verified in game), is maintained faster and has more correct names. Other sites (DiversHub, Fandom wiki) stay manual references.
- **Safeguards (same decision):** codes of existing stratagems are never changed silently, ids stay stable, the wiki is credited (CC BY-NC-SA 4.0), wiki content is loaded at runtime and never bundled, a changed wiki structure must not break the Companion.

### 1.1 Recognition test (2026-10-19)

The current classifier (`StratagemSlotClassifier`) was run on all local screenshots (6 loadout scenes × 5 heights, 9 saved slot images; 339 slots) with both icon sets. Script and results are not committed (scratchpad); the numbers:

| Icon set | Correct slots | Score median | Margin median | Failures |
|-|-|-|-|-|
| nvigneux, `IconRatio` 0.875 (today) | 336/339 | 0.936 | 0.312 | Bastion (wrong SVG), EAT-17 and EAT-411 badge |
| wiki.gg as is, `IconRatio` 0.875 | 321/339 | 0.774 | 0.200 | icons 12.6 % too large, SOS Beacon in mission slot 2 |
| **wiki.gg, `IconRatio` 1.0** | **337/339** | **0.953** | **0.360** | EAT-17 and EAT-411 badge (need reference images, as today) |

The wiki SVGs are traced on the 256×256 frame of the game texture (viewBox 0 0 256 256, content centered), so the icon fills the slot interior 1:1: the measured scale factor wiki/nvigneux is 0.874 (median, p10 0.867, p90 0.963) – the inverse of today's `IconRatio`. No per-icon cropping is needed. Weakest correct match: Directional Shield vs. Shield Generator Pack, margin 0.05 (nvigneux 0.08) – the two differ in the emblem only. Covered: about 20 of the ~105 stratagems (those in the screenshots).

## 2. Data source

### 2.1 Stratagem table (Cargo)

The Stratagems page renders its tables from the Cargo table `Stratagems` (`Module:Cargo Template Utilities`, `stratagemTable`). One request returns all rows:

```
GET https://helldivers.wiki.gg/api.php?action=cargoquery&format=json&limit=500&tables=Stratagems
    &fields=_pageName=page,title,stratagem_code=code,stratagem_type=type,image
```

| Field | Use |
|-|-|
| `page` (`_pageName`) | Full designation, e.g. `B/FLAM-80 Cremator`. **Key for matching** (§3.1). |
| `title` | Display title; for most rows equal to `page` (exceptions: `Hover Pack`, `Hellbomb`, `Link Hellpods to Destroyer`). |
| `code` | Wikitext with one arrow image per input (`[[File:Stratagem Arrow Down.svg|link=]]`) → parsed to `U/D/L/R`. Empty for unreleased stratagems (AMW-20 Apex, LAS-44 Nova on 2026-10-19). |
| `type` | `Orbital`, `Eagle`, `Support Weapon`, `Backpack`, `Vehicle`, `Sentry`, `Emplacement`, `Ship`, `Objective`, `Other` → catalog category (`Sentry`/`Emplacement` → `Defensive`; `Ship`/`Objective`/`Other` → `Mission`). |
| `image` | Icon file, e.g. `Cremator Stratagem Icon Background.svg` (§4.1). |

Rules learned from the data (2026-10-19: 122 rows, 118 with a type):

- Rows **without type** come from mission pages that repeat a stratagem box (`Rapid Acquisition`, `Seize Industrial Complex`, `Spread Democracy`) → ignored.
- A page can have **several rows** (`SSSD Delivery`: `DDDDDUU` and `DDDUU`) → all codes are kept; the catalog code wins if it is one of them.
- Changes in the wiki structure (missing table, renamed fields, no arrow images) → the check fails with a clear message ("wiki.gg could not be read: …"), nothing is changed locally.

### 2.2 Request budget and etiquette

- Automatic check at most **once a day** (as today for GitHub), plus a button. Per check: 1 Cargo request + 3 `imageinfo` requests (50 titles each) + downloads of new or changed SVGs only (first start: ~105 files, 1–2 KB each, 4 in parallel).
- User-Agent `HellboardCompanion/<version> (+https://github.com/frfrh/Hellboard)`; timeouts 30 s; HTTP errors and 429 → keep the local data, retry at the next check.
- Privacy: wiki.gg sees the user's IP address – the same as GitHub today. The BETA-README names the host.

## 3. Catalog

### 3.1 Matching wiki rows to catalog ids

- **Rule:** `designation` in `doc/stratagems.json` equals the **wiki page name** exactly. Today 114 of 115 entries match; `sos_beacon` changes from `SOS Beacon` to `SoS Beacon`. `doc/stratagems.md` documents the rule, `stratagem-sync` reports every mismatch.
- A wiki row whose page matches no designation is a **new stratagem**. Its id is the slug of the icon name (`Meltagun Stratagem Icon Background.svg` → `meltagun`), or of the title if the icon is a placeholder (`… Fallback Icon.svg`) – the same rule as the suggested lines of `stratagem-sync`.
- Ids never change (firmware `config.json`, user lists, reference images and history refer to them). Where today's id comes from an old nvigneux name (`eat_411`, `gl_28`, `cqc_20`), it stays.

### 3.2 Three layers

```
built-in doc/stratagems.json  <  wiki layer (wiki.json, cached)  <  user list (stratagems.json in %APPDATA%)
```

`wiki.json` lives in the data directory (`%LOCALAPPDATA%\HellboardCompanion\wiki\`) and holds the last successful Cargo result (rows as above + time of the check). `StratagemCatalog.Load` merges the three layers; what the wiki layer may do:

| Case | Wiki layer | Shown to the user |
|-|-|-|
| New stratagem with code | **added automatically** (code, name, category from the wiki) | Stratagems tab, source "wiki.gg" |
| New stratagem without code | added, not transferable (as today for icons without code) | "no code yet" |
| Catalog entry without code, wiki has one | **code filled in automatically** | source "wiki.gg" |
| Different code for an existing entry | **not applied** – a *proposal* | Stratagems tab: "wiki.gg lists ↓↓→↓↑↑ – take over?" with the catalog note (e.g. "Verified in game 2026-10-18"). *Take over* writes the code into the user list; *Dismiss* remembers the wiki code, so only a later, different change shows again. |
| Different category or designation | updated automatically (designation is not shown on the board; category only for new entries) | – |
| `name` (board display, sorting) | derived from the wiki page by the naming rule (§3.3) – for existing entries too | – |
| Entry missing on the wiki | kept unchanged | – |

The user list always wins. The firmware gets the merged list (incl. wiki layer) with the next firmware update, as today (`ToBoardJson`, F11) – no firmware change.

### 3.3 Names

Decided 2026-10-19 (user): the model prefix of the wiki page name is only an addition – hardly any player knows a stratagem by it.

- **Full name** = wiki page name (`designation`), e.g. `B/FLAM-80 Cremator`. Shown in the Companion (Stratagems tab, history, pickers in the settings and the Hellboard tab).
- **Short name** = full name without its model prefix: the first match of `^([A-Za-z0-9/\\]{1,10}-[A-Za-z0-9]{1,10}) ` is removed **once** (POSIX: `^([[:alnum:]\/\\]{1,10}-[[:alnum:]]{1,10}) `). `B/FLAM-80 Cremator` → `Cremator`, `AX/LAS-5 Rover` → `Rover`, `GL-28 Belt-Fed Grenade Launcher` → `Belt-Fed Grenade Launcher`; names without a prefix stay (`Orbital Laser`, `Activate E-711 Extraction Drill`). The short name is sent to the Hellboard (`name` in the board list) and is the sort key of the Stratagems tab. **The Overview tab shows the short names on purpose** (loadout and multi-function tiles, hints) – it mirrors what the Hellboard shows, and the short names read better on the tiles (user decision 2026-10-19).
- Result on 2026-10-19: 75 of 117 names lose a prefix, no two short names are equal, 47 are longer than one display line (16 characters) and scroll as today.
- `name` in `doc/stratagems.json` becomes the short name by this rule (generated by `stratagem-sync`, checked by a unit test); a user's own `name` in the user list still wins. Ids stay unchanged.

### 3.4 Release maintenance

`Software/tools/stratagem-sync/stratagem-sync.cs` (exists, uncommitted) compares `doc/stratagems.json` with the wiki and prints the differences plus suggested lines for new stratagems. In step 1 it moves onto the Core client (§5), so the tool and the Companion read the wiki the same way. Before every release: run it, take over what is right, note decisions in `doc/stratagems.md`.

## 4. Icons

### 4.1 Source and storage

- For every row the **plain icon** is used: `image` without ` Background` (`Cremator Stratagem Icon.svg`).
- If only the **background variant** exists (dark tile in the category color with a frame; `Meltagun` on 2026-10-19, drawn with another program), the plain icon is **derived from it by raster processing**: render it, measure the frame width from the left edge, replace frame and tile color with the slot background (anti-aliased edges blended by their distance to the tile color). Checked on all 107 stratagems that have both variants: mean difference to the plain icon at 64 px 0.12/255 (median), 0.58/255 (max) – the derived icon is as good as the plain one. Removing tile and frame from the SVG text is not used: the files are built differently (Affinity, CorelDRAW).
- `… Fallback Icon.svg` (placeholder of unreleased stratagems) → no icon; the UI shows the category placeholder as today.
- `imageinfo` (`iiprop=url|sha1`) gives URL and SHA-1 per file; a new `WikiIconDownloader` replaces `StratagemIconDownloader` with the same behavior: index file with SHA-1 per file and time of the last check, download new and changed files only (temp file + rename), keep files removed on the wiki.
- Storage: `%LOCALAPPDATA%\HellboardCompanion\icons-wiki\<file name>.svg` plus `index.json` (file → SHA-1, ids). The old folder `icons\` (nvigneux) is deleted after the first successful wiki download – it is a cache only; reference images (`references\`) are kept, their ids do not change.
- `StratagemIcon` gets the id from the catalog match (§3.1), not from the file name; `Group` (nvigneux folder) is dropped or becomes the wiki type.
- Derived icons are stored as PNG next to the SVGs (`<file name>.png`, 256×256 on the slot background) and re-derived when the SHA-1 of their source changes; `StratagemIcon` and the classifier accept SVG or PNG.

### 4.2 Shared icons

Some files belong to several stratagems: `Drill` (5 drills), `Cargo Container` (Cargo Container, Portable Comms Relay), `Upload Data` (Upload Data, SSSD Delivery, Reinforcement Pods). Identical templates cannot be told apart (margin 0 → "unknown"). These ids get the icon for the **UI only**; the recognizer gets the template once per file under the id whose designation equals the icon name (`Cargo Container`, `Upload Data`) or none (`Drill`). Today none of them has an icon at all, so this is not a step back.

### 4.3 Recognition

- `SlotStyle.PlayerSlot.IconRatio` 0.875 → **1.0**; `SlotStyle.MissionSlot.IconRatio` 0.867 → **0.991** (same factor; to be confirmed with the mission slots of the scenes).
- `StratagemImages` (UI): the wiki icons have a margin of ~6 % per side; rendered as they are (looks like the game slot). The editor's category guess from the SVG color (`GuessCategory`) is replaced by the wiki type.
- Tests: `LocalIcons` downloads the wiki icons (same rule: at most once a day, skipped offline); all scene and slot tests must pass. `A_reference_image_replaces_a_wrong_svg` loses its premise (the wiki Bastion is right): it is rewritten to replace a correct template with a reference image of another stratagem.

## 5. Code structure (Core)

| New / changed | Responsibility |
|-|-|
| `Catalog/WikiClient` | Cargo query and `imageinfo` requests, parsing (codes, types, icon names); `HttpClient` injected; unit tests with recorded JSON responses (committed: facts only, no icons). |
| `Catalog/WikiStratagemLayer` | Reads/writes `wiki.json`, computes additions and proposals against built-in + user list, dismissed proposals. |
| `Catalog/WikiIconDownloader` | Replaces `StratagemIconDownloader` (§4.1). |
| `StratagemIconStore` | Uses the wiki downloader; `UpdateAsync` updates stratagem table and icons together ("Check wiki.gg now"). |
| `StratagemCatalog.Load` | Merges the wiki layer between built-in and user list; `CatalogEntry` gets `Source` (built-in / wiki / user). |
| App: Settings, Stratagems tab | Status "wiki.gg: last check, N icons, N new stratagems, N proposals"; proposals with *Take over* / *Dismiss*; source text "wiki.gg". |

## 6. Steps

Each step is one or more commits on `feature/hellboard-companion`, with tests; release as **0.8.0** after step 4 (pull request, user merges).

1. **Wiki client, sync tool and names.** `WikiClient` with tests; `stratagem-sync` uses it; `designation` rule (`SoS Beacon`), naming rule (§3.3: 21 names changed, the Companion shows the full name), docs (`doc/stratagems.md`, README knowledge base and acknowledgement). *Done 2026-10-19.*
2. **Wiki layer in the catalog.** `WikiStratagemStore` (local copy `wiki\stratagems.json` with the dismissed proposals, checked at start, every 6 h at most once a day and on *Check now*), `WikiMatching` shared with `stratagem-sync`, merge in `StratagemCatalog` (`WikiIds`, `WikiProposals`, `Note`), proposals in the Stratagems tab (*Take over wiki code* / *Keep current code*, filter, summary), wiki status in the settings, BETA-README and third-party notices. *Done 2026-10-19.*
3. *Done 2026-10-19:* all scene and slot tests pass with the wiki icons (incl. Bastion MK XVI without reference image); `HellboardLinkTests.Changed_multi_function_list_is_sent_again` was made race-free on the way. **Icons from the wiki.** `WikiIconDownloader`, id mapping, shared icons, `IconRatio`, migration of the icon folder, tests on the local scenes (expectation: ≥ 337/339 as in §1.1). Manual check in the game with a few loadouts.
4. *Done 2026-10-19 together with step 3 (the root README is unchanged):* **Clean-up and docs.** Remove `StratagemIconDownloader` and the nvigneux references (README, BETA-README, THIRD-PARTY-NOTICES, implementation plan §3.3/§6.11/§6.12); attribution: "Stratagem data and icons: Helldivers Wiki (helldivers.wiki.gg), CC BY-NC-SA 4.0; icons traced by wiki contributors (e.g. Dogo314)". The root `README.md` (line "icons for the buttons") is only changed with the user's OK.

## 7. Risks

| Risk | Mitigation |
|-|-|
| Wrong or vandalized code on the wiki | Codes of existing entries only by proposal + confirmation; user list wins; in-game verified decisions documented. |
| Changed or vandalized icon | SHA-1 change → re-download and log; a bad icon makes a stratagem "unknown", not wrong (score/margin thresholds); reference images override icons. |
| Wiki structure changes (Cargo fields, arrow image names) | Parser fails closed with a message; cached `wiki.json` and icons stay in use; `stratagem-sync` shows the problem before a release. |
| Wiki unreachable / rate limit | Once a day, cache, retry at the next check. |
| New stratagem with a placeholder icon (`… Fallback Icon.svg`) | UI placeholder; not recognized until the wiki has the icon (as today with nvigneux). Only the background variant → derived icon (§4.1). |
| Close pairs (Directional Shield / Shield Generator Pack) | Margin is at the threshold on the wiki icons; watch the history; reference image as fallback. |

## 8. Decisions (2026-10-19)

1. **Different codes:** proposal + confirmation in the Stratagems tab (§3.2).
2. **Names:** full wiki name in the Companion, short name (model prefix removed by the regex, applied **once**) on the Hellboard, in the Overview tab and as sort key (§3.3). Confirmed by the user's comparison list: e.g. `Anti-Tank Emplacement`, `Anti-Tank Mines`, `Belt-Fed Grenade Launcher` (applying the regex repeatedly would give ambiguous names such as `Mines` and a duplicate `Grenade Launcher`).
3. **nvigneux:** dropped completely, no fallback; stratagems with only a background icon get a derived icon (§4.1).
4. **Online checks:** always on as today, no setting.
