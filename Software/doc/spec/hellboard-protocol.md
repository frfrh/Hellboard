# Hellboard protocol (PC ↔ Hellboard)

| | |
|-|-|
| Status | Draft, version 1 |
| Date | 2026-10-16 |
| Decisions | F1–F9 in [implementation-plan.md](implementation-plan.md) §6.9 |

The Hellboard Companion (PC) sends the locked loadout to the Hellboard and configures it. This document is the contract between the PC software and the firmware; both sides implement exactly this.

## 1. Transport

- **USB CDC serial** of the RP2040 (the MicroPython REPL port), next to the HID keyboard (F1). The baud rate is irrelevant for USB CDC; the PC uses 115200.
- The PC finds the port by USB vendor id **`2E8A`** (Raspberry Pi) and checks the board with `ping` (§4.1).
- While `main.py` runs, it owns `sys.stdin`; it reads lines **non-blocking** (`select.poll`) inside its main loop, so button handling never waits for the PC.
- **Ctrl-C (0x03) stays enabled** (F8). It never appears in JSON text, so it cannot be triggered by protocol data.
- **Flow control:** the board's USB receive buffer is small; when the firmware does not read `sys.stdin`, the PC's write blocks (observed with the old firmware, 2026-10-16). A full `setLoadout` (up to ~3 KB) needs several buffer fills. Therefore the firmware **reads `sys.stdin` at least every 100 ms, also while it is sending a stratagem macro** (between the key events), and the PC uses a write timeout of 3 s. A write timeout on the PC side counts as "board does not answer".

## 2. Framing

- One message per line: **UTF-8 JSON object terminated by `\n`**. `\r\n` is accepted.
- Maximum line length: **4096 bytes** (a full `setLoadout` with 4 + 16 stratagems and full names stays below 3 KB). Longer lines are discarded and answered with error `line_too_long` (without `id`).
- Every message carries the protocol version: `"v": 1`.
- The board may print other text (debug output). **The PC ignores every line that does not start with `{` and contain `"v":`**; the board never starts debug output with `{`.
- The board ignores lines that are not valid JSON and answers with error `invalid_json` (without `id`).

## 3. Requests, responses, events

- **Request** (PC → board): `{"v":1, "id":<int>, "cmd":"<command>", ...parameters}`. `id` is chosen by the PC (incrementing, 1…2³¹−1).
- **Response** (board → PC), exactly one per request: `{"v":1, "ack":<id>, "ok":true, ...result}` or `{"v":1, "ack":<id>, "ok":false, "error":"<code>", "message":"<text>"}`.
- **Event** (board → PC, unsolicited): `{"v":1, "event":"<name>", ...}`.
- The PC waits **500 ms** for a response and retries up to **3 times with the same `id`**. All commands are **idempotent**: repeating a request with an `id` that was already executed just repeats the response.
- Requests are processed in order; the PC sends the next request only after the response to the previous one.

## 4. Commands

### 4.1 `ping`

Checks the connection and reports versions.

```json
→ {"v":1,"id":1,"cmd":"ping"}
← {"v":1,"ack":1,"ok":true,"board":"hellboard","fw":"2.0.0","protocol":1,"micropython":"1.17.0"}
```

The PC refuses to talk to a board whose `protocol` is newer than its own and offers a firmware update when `fw` is older than the bundled firmware.

### 4.2 `setLoadout`

Assigns the locked loadout (F2, F5). Applied immediately, **kept in RAM only** (F3).

```json
→ {"v":1,"id":2,"cmd":"setLoadout",
   "stratagems":[
     {"name":"Eagle 500KG Bomb","color":"red","sequence":"URDDD"},
     {"name":"Grenade Launcher","color":"blue","sequence":"DLULD"},
     null,
     {"name":"Mortar Sentry","color":"green","sequence":"DURRD"}],
   "missionStratagems":[
     {"name":"SOS Beacon","color":"yellow","sequence":"UDRU"},
     {"name":"Hellbomb","color":"yellow","sequence":"DULDURDU"}]}
← {"v":1,"ack":2,"ok":true}
```

| Field | Rules |
|-|-|
| `stratagems` | Exactly 4 entries, in slot order → `button_2_1 … button_2_4`. `null` = slot empty or not recognized: the button does nothing and its LED is off. |
| `missionStratagems` | 0–16 entries → list of the multi-function button `button_1_3`, cycled with `button_1_4` (F5). The index restarts at 0. Empty list → button disabled, LED off, display "–". |
| `name` | The **full display name**, 1–64 characters, UTF-8 (e.g. `"Eagle 500KG Bomb"`, `"StA-X3 W.A.S.P. Launcher"`). The PC **never shortens** names. |
| `color` | `yellow`, `red`, `green`, `blue` or `white` (LED color category, as `categories` in `doc/stratagems.json`). |
| `sequence` | 1–10 characters of `U`, `D`, `L`, `R` (arrow directions). The keys actually sent depend on the configuration (§4.4). |

Errors: `invalid_loadout` (with `message` naming the field).

**Names are shortened only where it is unavoidable – on the OLED display** (decided 2026-10-16). The firmware keeps the full name in memory, in `getState`, in its log output and in the saved default; only the display module fits it to the 16 characters of a display line (characters the OLED font cannot show are replaced there as well). This keeps hardware logs readable and prepares a later **scrolling text (marquee) with the full name** – not part of the current implementation, but the display module gets a single function that renders a name into a line of limited width, so scrolling can be added there without touching protocol or state.

### 4.3 `getState`

Returns what the board currently uses – for the PC status display and after reconnecting.

```json
→ {"v":1,"id":3,"cmd":"getState"}
← {"v":1,"ack":3,"ok":true,
   "stratagems":[…as in setLoadout…],
   "missionStratagems":[…],
   "config":{…as in setConfig…},
   "source":"pc"}
```

`source`: `pc` (received from the PC since power-up), `default` (saved default from flash) or `builtin` (firmware default, nothing saved yet).

### 4.4 `setConfig`

Changes settings (F7). Applied immediately, kept in RAM only. All fields are optional; omitted fields stay unchanged.

```json
→ {"v":1,"id":4,"cmd":"setConfig","config":{
     "modifierKey":224, "modifierToggle":false, "keyDelayMs":50,
     "directionKeys":"arrows", "brightness":80}}
← {"v":1,"ack":4,"ok":true,"config":{…complete config after the change…}}
```

| Field | Meaning | Range | Default |
|-|-|-|-|
| `modifierKey` | HID usage id of the stratagem key (e.g. 224 = left Ctrl) | 4–231 | current `helldiver.py` value |
| `modifierToggle` | `true`: press once to open the menu; `false`: hold while entering | – | `false` |
| `keyDelayMs` | Delay between key down/up events | 20–200 | 50 |
| `directionKeys` | `arrows` or `wasd` | – | `arrows` |
| `brightness` | LED brightness | 0–255 | 80 |

Errors: `invalid_config`.

### 4.5 `saveDefault`

Writes the **current** loadout and configuration to the flash (JSON file in the MicroPython filesystem) as the power-up default (F3). Sent **only** when the user presses "Set as default" in the Companion – never automatically, to keep flash writes to a minimum.

```json
→ {"v":1,"id":5,"cmd":"saveDefault"}
← {"v":1,"ack":5,"ok":true}
```

The file is written to a temporary name and renamed afterwards, so a power loss never leaves a broken default. Errors: `storage_error`.

### 4.6 `enterUpdateMode`

Prepares the firmware update (F8, implementation plan §6.10): releases all keys, turns LEDs off, shows "Update…" on the display, answers and then leaves the main loop to the REPL.

```json
→ {"v":1,"id":6,"cmd":"enterUpdateMode"}
← {"v":1,"ack":6,"ok":true}
```

After the response, the PC continues with the raw REPL (Ctrl-A). Fallback if the board does not answer: Ctrl-C.

### 4.7 `getLog`

Returns the last log entries kept in RAM (up to 32, oldest first), so the PC also sees messages from before it connected (decision F12).

```json
→ {"v":1,"id":7,"cmd":"getLog"}
← {"v":1,"ack":7,"ok":true,"entries":[{"level":"info","t":812,"msg":"Firmware 2.0.0 started"}]}
```

## 5. Events

| Event | When | Content |
|-|-|-|
| `log` | Whenever the firmware logs something (decision F12) | `{"v":1,"event":"log","level":"info","t":12345,"msg":"Loadout received"}` – `level`: `debug`, `info`, `warn`, `error`; `t`: milliseconds since boot; `msg`: ≤ 200 characters. Sent even when no PC listens (USB CDC drops it). |
| `hello` | After power-up / soft reset, once the main loop runs | `{"v":1,"event":"hello","fw":"2.0.0","protocol":1}` – the PC re-sends the last locked loadout. |

## 6. Error codes

| Code | Meaning |
|-|-|
| `invalid_json` | Line is not a JSON object |
| `line_too_long` | Line exceeds 4096 bytes |
| `unsupported_version` | `v` is missing or not supported |
| `unknown_command` | `cmd` is unknown |
| `invalid_loadout` | `setLoadout` parameters violate §4.2 |
| `invalid_config` | `setConfig` parameters violate §4.4 |
| `storage_error` | Writing the default to flash failed |

## 7. Hardware constraints

- No EEPROM is populated; only the RP2040-Zero's RAM and flash are used (implementation plan §6.9).
- Flash writes happen only through `saveDefault` and the firmware updater.

## 8. Verification on the real board

Board spike (`Software/tools/board-spike/`) on the user's Hellboard, 2026-10-16 – **all checks passed**:

| Check | Result |
|-|-|
| MicroPython | 1.17.0 (rp2), 156 KB RAM free, 1364 of 1408 KB flash free (4 KB blocks) |
| Ctrl-C stops `main.py`, raw REPL accepts and runs code from RAM | ✔ |
| JSON lines over `sys.stdin`, read non-blocking with `select.poll` while the main loop keeps running | ✔ (≈ 64 loop iterations between requests 300 ms apart) |
| `json`, `hashlib.sha256`, `binascii` base64, `os.rename`, `machine.bootloader` | ✔ |
| Releasing all HID keys with an empty report | ✔ |

Notes: MicroPython's `json.dumps` writes `": "` and `", "` with spaces – parsers on the PC must not depend on the formatting. The raw REPL prefixes the first output line with `OK`. The board enumerates as a USB composite device (CDC + HID with keyboard, mouse, gamepad and consumer control collections).
