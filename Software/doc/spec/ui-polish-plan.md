# UI polish – plan

| | |
|-|-|
| Status | **Plan, not implemented** (branch `feature/ui-polish`) |
| Date | 2026-10-17 |
| Scope | Main window of Hellboard Companion: background, status chain, multi-function list, "Recognize loadout now" |

Goal: the Companion looks more like it belongs to Helldivers 2 and stays clean at every window width. Nothing in recognition, transfer or settings changes.

## 1. Background

**Requirements:** unobtrusive and dark, but less uniform than the current flat black; in the style of Helldivers 2; no problems when the window is resized.

**How it stays correct at every size:** the background is not a stretched picture. It is drawn by the app at the current window size:

- a dark base with a soft radial gradient (vignette) that scales with the window,
- a fine pattern that is **repeated**, not scaled (tiles stay the same size, the window just shows more or less of them),
- decorations anchored to edges or corners (they move with the edges).

Technically a small `BackgroundLayer` control behind the tab control that draws on every size change (`Render` with a cached pattern tile), or Avalonia brushes (`RadialGradientBrush` + tiled `DrawingBrush`). Only own, procedurally drawn graphics – no game art. Content panels keep their own semi-transparent backgrounds, so text contrast does not change.

**Options** (real overview rendered with each background; left 1200 px wide, right at the minimum width of 900 px):

| | Option | Idea | Wide | Narrow |
|-|-|-|-|-|
| 1 | **Hex grid** | Fine hexagon grid (the hexagon of the app icon and the display logo), soft yellow glow in the top-left corner | ![](ui-polish/1-hex-grid-wide.png) | ![](ui-polish/1-hex-grid-narrow.png) |
| 2 | **Hazard stripes** | Yellow/black warning stripes along the bottom edge, fading out upwards; thin yellow line at the top edge | ![](ui-polish/2-hazard-stripes-wide.png) | ![](ui-polish/2-hazard-stripes-narrow.png) |
| 3 | **Tactical map** | Faint topographic contour lines in blue with map grid crosses, like the planet map / mission briefing | ![](ui-polish/3-tactical-map-wide.png) | ![](ui-polish/3-tactical-map-narrow.png) |
| 4 | **Terminal** | Super Earth terminal: faint grid, scan lines and yellow HUD brackets in the window corners | ![](ui-polish/4-terminal-wide.png) | ![](ui-polish/4-terminal-narrow.png) |

**Recommendation:** option 1 – it is the quietest at any size and repeats the hexagon of the icon and the display logo. Options 2 and 4 can be combined with it (hazard stripes at the bottom edge, or the corner brackets) if more Helldivers character is wanted. The narrow renders also show the problem of section 2: the status texts overlap.

The previews were rendered with the headless screenshot approach of `Software/tools/screenshot`; the final variant gets the same kind of preview for the README screenshot.

## 2. Status chain (game → recognition → Hellboard)

**Today:** a grid with three equally wide columns and two arrows. At narrow widths the texts overlap (see the narrow previews).

**Plan:**

- The three status entries are placed in a **`WrapPanel`**: when the window gets too narrow for all three, the last ones move to the next line; when it gets wider again, they come back. This follows every width change automatically (it is layout, no code).
- **An entry never breaks inside:** each entry is one non-wrapping row (dot + text, `TextWrapping="NoWrap"`). Should a single text ever be wider than the window (not possible at the minimum width of 900 px with today's texts), it is cut with "…" and the full text is shown as tooltip.
- **Arrows:** a separate arrow element would end up at the end or start of a line after wrapping. Instead each entry gets a **chevron shape** (pointed right edge, notched left edge, like the Helldivers UI) – the flow direction stays visible on one line and on several lines.
- Entries have a common minimum width, so on one line they still look like a balanced row.

## 3. Multi-function button list

**Today:** one horizontal row without wrapping; the origin of an entry is not visible.

**Plan:**

- Tiles are placed in a **`WrapPanel`**: entries that do not fit move to the next line, also when the width changes; a tile never breaks inside (icon and name stay on one line).
- **Origin of the entries:** the list is shown in **groups**, each in a thin, rounded frame (1 px, low contrast) with a small caption in the top-left corner of the frame:
  - **Fixed list** – the entries of the settings,
  - **Detected** – the optional mission stratagems recognized on the loadout screen.
  
  The frame costs almost no space (a few pixels plus the caption inside the top border line). Each group is one item of an outer wrap panel; a group that does not fit next to the other one starts on a new line, and its tiles wrap inside the frame.
- The frames are shown in **every mode** (also "fixed list only" / "automatic detection only"), so the look is the same and the caption still says where the entries come from. An empty group is not shown (e.g. "Detected" before a loadout screen was seen).
- An entry that is in both (fixed and detected) appears once, in "Fixed list" – as on the Hellboard.
- Data: the overview builds the groups itself from the multi-function settings and the locked loadout (`LoadoutTransferBuilder` stays the single place that decides the order on the board; the overview only labels the groups). Unit tests for the grouping (all three modes, duplicates, empty groups).

## 4. "Recognize loadout now" only when the game is running

**Today:** the button is always shown; without the game the recognition only reports that no screen was found.

**Plan:**

- The overview button is **shown only while Helldivers 2 is running** (game status *running*, also when it is in the background or minimized – recognition needs a visible game window, but hiding the button while alt-tabbed would be irritating; it is disabled while minimized).
- The tray menu entry "Recognize loadout now" is disabled while the game is not running.
- The hotkey stays registered; pressed without the game it shows the toast "Helldivers 2 is not running" instead of an empty recognition result.
- Unchanged: *Diagnostics → Analyze image file* works without the game (testing with screenshots).

## 5. Testing

- The headless screenshot tool renders the overview at 900, 1200 and 1600 px width with long and short texts and 2/8/16 multi-function entries – checked visually, and kept as a script for later changes.
- Unit tests for the multi-function grouping and for the visibility of the recognize button (game states).
- Check on the user's machine: resize the window live, dark/light Windows theme (the app stays dark), high DPI (150 %).

## 6. Order and effort

| Step | Effort |
|-|-|
| Status chain with wrap panel and chevrons | small |
| Multi-function groups with frames and wrapping (incl. view model and tests) | medium |
| Recognize button / tray / hotkey depending on the game | small |
| Background (chosen option) incl. resize test | small to medium |

All in one round of work; the background last, so the layout changes can be judged on it.

## 7. Open questions for the user

1. **Background:** option 1, 2, 3 or 4 – or a combination (e.g. 1 + hazard stripes of 2)?
2. **Arrows in the status chain:** chevron-shaped entries (recommended) or no direction marks at all?
3. **Group frames** in every mode (recommended) or only with "fixed list + automatic detection"?
4. **Recognize button without the game:** hidden (as requested) – and should the hotkey without the game show a short notice (recommended) or do nothing?
