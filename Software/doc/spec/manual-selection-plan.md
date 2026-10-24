# Stratagems by hand – plan

| | |
|-|-|
| Status | **Implemented without section 5** (branch `feature/manual-selection`); the multi-function list by hand is a later step |
| Date | 2026-10-18, decisions 2026-10-20, right-click reset 2026-10-24 |
| Scope | Overview of Hellboard Companion: choosing the four stratagems (and the detected part of the multi-function list) by clicking the tiles, sent to the Hellboard like a recognized loadout |

Goal: the Hellboard stays usable when the recognition does not work – e.g. on a Windows 10 PC where the game window is not found, with an unusual resolution or HUD scale, or for a brand-new stratagem whose icon is not known yet. The player clicks a tile in the overview, picks the stratagem, and the Hellboard gets it. The same also corrects a single wrongly recognized slot. Nothing in the recognition itself changes.

## 0. Starting point

- `LockSource` already has `Manual` – that is the **manual trigger** (hotkey / "Recognize loadout now", implementation plan §6.13), i.e. still a recognition. It is stored as `"manual"` in `loadouts.jsonl`, so it cannot be renamed. Choosing by hand needs its own value.
- `LoadoutTracker` is not thread-safe and is only fed on the pipeline thread; `LoadoutPipeline` already has a request queue (`Enqueue`) that runs work on that thread, also in recognition mode *ManualOnly* and before the icons are loaded.
- Every lock-in goes `Tracker.Locked` → `LoadoutPipeline.OnLocked` (history, problem entries) → `LoadoutLocked` → `HellboardLink.Submit` (wired in `Desktop.Windows/Program.cs`) → `LoadoutTransferBuilder.Build`. A loadout chosen by hand can use exactly this path; the transfer, the "not transferable" warning and the "keep until a board is connected" behaviour come for free.
- The overview shows the live screen while the player selects, otherwise `Tracker.LastLocked`, otherwise the last loadout of a previous session (`OverviewViewModel.RefreshLoadout`). The large tiles use the `LargeTile` template, which is only used there.

## 1. UI

### 1.1 Clickable tiles

- The four large tiles become buttons (a `Button` with the `LargeTile` content, flat style). Hover: slightly lighter background and a small pencil mark in the top-right corner; tooltip "Choose the stratagem by hand".
- An empty tile shows "+" instead of "–" and the caption "choose …" when there is nothing at all yet (no recognition, no previous session) – this is the Windows 10 case, where the user must see at once that the tiles are clickable.
- A tile chosen by hand gets a small hand/pencil mark and the note "set by hand" (same place as today's "no input sequence yet" note, which takes precedence).
- Below the tiles, only while the game is not found or nothing was recognized in this session: a dim hint line "Recognition not available? Click a tile to choose the stratagem by hand." (wording see open questions).
- **"Back to recognized"** button next to the loadout title, only while the shown loadout contains hand choices: drops all hand choices and shows/sends the last automatic lock-in again (if there is none: clears to the previous state).

### 1.2 Picker

A **flyout** attached to the clicked tile (Avalonia `Flyout`, no extra window; closes on click outside or Esc). Content from top to bottom:

1. **Search box**, focused when the flyout opens. Matches the full name (wiki designation, `FullName`), the short name and the id, case-insensitive, substring – like the search in the Stratagems tab. German names are not searched in the first version (decision 9). Enter takes the first match, Esc closes.
2. **Category chips**: All · Orbital · Eagle · Support weapon · Backpack · Vehicle · Defensive (from `StratagemCatalog.Categories`; *Mission* only in the multi-function picker, section 5). One chip active at a time; the chip has the LED colour of the category as a small dot.
3. **Recently used** row (up to 8, from the last locked loadouts in the history) – the usual loadout is then one click away. Hidden while searching.
4. **Grid of icon tiles** (48 px icon + name, wrapping, scrollable, ~4–5 columns at the flyout width of about 480 px), grouped by category with a small heading, alphabetical inside. Stratagems without icon (user stratagems) show a square in the category colour with the first letters.
5. Footer: **"Empty slot"** (sets the slot to empty) and, if the slot was recognized, **"Use recognized: <name>"**.

Rules in the grid:

- Mission stratagems are not offered for the four player slots (the game does not allow them there; `StratagemCatalog.MissionOnlyIds`).
- A stratagem already in another slot is shown dimmed with "slot 2"; choosing it **swaps** the two slots (the game does not allow duplicates, and swapping is what the user usually means).
- Stratagems without input sequence can be chosen (the user may add the code in the stratagem editor afterwards) but are marked "no input sequence"; the existing warning below the overview then names them.

Data: a new `StratagemPickerViewModel` (search text, active category, recently used, filtered items as `StratagemTileViewModel`) shared by the tile picker and the multi-function picker; the filter logic is a pure function so it can be unit-tested without Avalonia (like `MultiFunctionGroups.Split`).

### 1.3 Right-click: back to the recognized stratagem (2026-10-24)

- A **right-click** on a tile set by hand puts that one slot back to what the recognition had there; the other hand choices stay. It also works with the keyboard context key and a long press (Avalonia `ContextRequested`). A tile with nothing to go back to (not set by hand, the slot was "not recognized", or a hand choice of version 0.10 or older) ignores the right-click.
- The tooltip of such a tile says "Right-click: back to the recognized one (<name>)".
- What the recognition had is **stored with the hand choice** in `loadouts.jsonl` (`LockedLoadout.Recognized`, section 2), so the reset also works for the loadout of a previous session after a restart – the in-memory `Tracker.LastRecognized` behind "Back to recognized" is gone then.
- A swap is undone as well (the slot that got the other stratagem gets its recognized one back). Choosing the recognized stratagem in the picker also clears the hand mark.
- When the last hand mark goes away and the result equals the last recognized lock-in of this session, it counts as "Back to recognized" (the pending history entry is dropped, the recognized loadout is locked in again). Otherwise the result is sent as a hand choice without marks, which corrects nothing: the next automatic lock-in counts again (rule 3.2).

## 2. Data model

- New `LockSource.ByHand` (JSON `"byHand"`), described as "set by hand" in the overview and "Set by hand" in the history.
- A hand choice always produces a **complete `LockedLoadout`**: the shown loadout (live snapshot, last lock-in or previous session – the same base the overview shows) with the one slot replaced. `MissionStratagems` are taken over from the base. `IsComplete` is true unless a slot still is "not recognized" (`?`), which is then kept as `null`.
- To be able to show which tiles were set by hand and to support "Back to recognized", the record gets an optional `IReadOnlyList<bool>? ByHand` (per player slot; `null` = none). Older Companion versions ignore the unknown property; lines with `"source": "byHand"` are skipped by older versions when they read the history (damaged-line handling), which is acceptable.
- Since 2026-10-24 also `IReadOnlyList<string?>? Recognized` (JSON `"recognized"`): per player slot what the recognition had before the hand choice – stratagem id, `null` = empty, `"?"` = nothing to go back to. Derived from the shown loadout (a slot not set by hand counts with what is shown, a slot set by hand keeps its stored value), written with the hand choice and left out when no slot is set by hand (section 1.3).

## 3. Tracker, pipeline and the next automatic recognition

### 3.1 Path

- `LoadoutPipeline.SetByHandAsync(LockedLoadout)` enqueues the work on the pipeline thread (like `RecognizeManuallyAsync`) and calls the new `LoadoutTracker.LockByHand(LockedLoadout)`, which sets `LastLocked`, raises `Locked`, and records what the automatic recognition showed at that moment (see 3.2). History, problem entries ("no input sequence") and the transfer then run unchanged.
- Works without the game, without icons and in every recognition mode; `pipeline` is always created, so no extra wiring in `Program.cs` is needed.

### 3.2 What happens when recognition comes in

Recommended rule: **a hand choice stays until the game shows something different.** The tracker keeps the automatic content the hand choice replaced (`_correctedFrom`: the candidate or last automatic lock at the time of the hand choice). An automatic lock-in (Ready or screen closed) whose content equals `_correctedFrom` is **skipped**; any other automatic lock-in replaces the hand choice as usual and clears `_correctedFrom`.

| Situation | Result |
|-|-|
| No game window (Windows 10 case), user sets four tiles | Each choice is sent; nothing overrides it. |
| Recognition locked a wrong slot after Ready, user corrects it | Sent. The loadout screen then closes – no new lock-in (tracker is already locked). |
| User corrects while still on the loadout screen (selecting), then presses Ready | Ready would lock the same wrong candidate → skipped, the correction stays. |
| Next mission, player changes the loadout in the game | Different content → automatic lock-in replaces the hand choice. |
| Next mission, same loadout and the same misrecognition again | Equal to `_correctedFrom` → skipped, the correction stays. |
| Player withdraws Ready and changes something | Different content → replaces the hand choice. |

The overview uses the same rule for what it shows: while the loadout screen is visible and the live snapshot still equals `_correctedFrom`, it shows the hand choice (otherwise a correction made on the loadout screen would seem to have no effect, because the overview shows the live screen while selecting).

Rejected alternatives (decision 2): **(A)** simply "the latest wins" – every automatic lock-in replaces the hand choice (easiest, but a correction on the loadout screen is lost on Ready); **(B)** per-slot corrections that keep applying while the slot shows the same recognized stratagem (most convenient, but "magic" and harder to explain); **(C)** a hand choice pauses the automatic lock-in until "Back to recognized" is clicked (predictable, but easy to forget, then the next mission sends the old loadout).

## 4. Transfer to the Hellboard

- Unchanged: `pipeline.LoadoutLocked` → `HellboardLink.Submit` → `LoadoutTransferBuilder.Build`. Each choice is sent at once (one `setLoadout` of a few hundred bytes; the board only gets something when the content changed).
- No board connected: the loadout is kept and sent on connect, as today. Dry-run and simulator behave as today.
- The multi-function list follows the settings mode exactly as for a recognized loadout (fixed list and/or the detected mission stratagems of the hand-made `LockedLoadout`).

## 5. Multi-function list

- **Fixed list** group: not edited in the overview (it is a setting). Clicking the caption or a small gear opens *Settings → Multi-function button*.
- **Detected** group: editable when the mode includes automatic detection. Each tile gets a small "×" on hover (removes it from `MissionStratagems`), and a "+" tile at the end opens the picker with the *Mission* category preselected (all categories available, because the multi-function list may hold anything). Maximum 16 entries (`LoadoutTransferBuilder.MaxMissionStratagems`), the "+" disappears when full. Order: in the order they were added.
- In mode *Fixed list only* the Detected group does not exist; nothing to edit.
- The result is again a `LockedLoadout` with `LockSource.ByHand` (same path as section 3). **Not part of the first version** (decision 4); the first version only makes the four tiles editable.

## 6. History

- Hand choices are history entries with the source "Set by hand" (filter "important" shows them, like Ready).
- Setting four tiles in a row must not create four entries. Recommended: the pipeline **delays writing** a hand lock-in by 30 s; a further hand choice within that time replaces the pending entry; the pending entry is written at once before any other lock-in and on shutdown. Transfer is never delayed.

## 7. Status chain

- Recognition field while the shown loadout is set by hand: short text "Set by hand", detail "click a tile to change", level *good*. Fits the 28-character rule of `StatusLine`.
- Loadout title: "Set by hand at 14:03:12" / "Locked in at 14:03:12 (Ready), slot 2 set by hand".

## 8. Tests

- `LoadoutTrackerTests`: `LockByHand` raises `Locked` with `ByHand`; every row of the table in 3.2 as a test (skip on equal content, replace on different content, Ready withdrawn, screen exit while selecting).
- `LoadoutHistoryTests`: `"byHand"` and the `ByHand` flags round-trip; old lines without them still read; delayed write (replace within 30 s, flush before an automatic lock-in and on dispose) with an injectable clock.
- `TransferTests`: a hand-made loadout with empty slots, a stratagem without sequence, and mission stratagems in all three multi-function modes.
- Picker filter: search (name, id), categories, mission stratagems excluded for player slots, swap on duplicate, recently used order.
- `OverviewStatusTests`: the new texts, max. 28 characters.
- Overview view model: base selection (live / locked / previous session), "Back to recognized".
- Screenshot tool: overview with hand-set tiles and the open picker at 900 and 1600 px; manual check on the user's machine with the game closed (Windows 10 case simulated by a wrong process name in the settings) and with a connected Hellboard.

## 9. Order and effort

| Step | Effort |
|-|-|
| `LockSource.ByHand`, `ByHand` flags, `LockByHand` + rule 3.2, pipeline method, tests | medium |
| Picker view model (search, categories, recently used, swap) + tests | small to medium |
| Overview: clickable tiles, flyout, marks, hint, "Back to recognized", status texts | medium |
| History: delayed write / labels | small |
| Multi-function "Detected" editing | small to medium |
| Screenshots, manual test with and without game | small |

In total roughly the size of the UI polish round. The multi-function editing can be left for a second step without blocking the rest.

## 10. Decisions (2026-10-20)

1. **Name of the source**: "set by hand" in the overview, "Set by hand" in the history; internally `ByHand`.
2. **Interaction with the recognition**: the recommended rule of section 3.2 – a hand choice stays until the game shows something different.
3. **Picker form**: flyout at the tile; every choice is applied at once.
4. **Multi-function list**: not in the first version; only the four tiles. Section 5 is a later step.
5. **History**: delayed write of one entry (section 6).
6. **Duplicates**: swap the two slots.
7. **Hint line**: visible while the game is not found or nothing was recognized in this session; the README gets a short section "If the recognition does not work".
8. **Teaching the recognition** from a hand correction: a possible follow-up, not part of this plan.
9. **German names in the search**: later, if at all (then as an optional `nameDe` in `stratagems.json`).
10. **Right-click reset** (2026-10-24): the recognized stratagem of every slot is stored with the hand choice; a right-click on a tile set by hand puts the slot back to it (section 1.3).
