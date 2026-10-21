# hellpod-animation

Development tools for the hellpod impact animation on the Hellboard display (decision F17), the feedback for a received loadout instead of the LED flash. The animation itself is [`Firmware/libs/hellpod.py`](../../../Firmware/libs/hellpod.py); both tools use that file.

## Preview on the PC

Renders one GIF per scene and a contact sheet with all frames, in the colors of the display (needs Pillow):

```powershell
python Software/tools/hellpod-animation/preview.py <out_dir> [fps] [duration_ms]
```

## Test on the board (RAM only)

`Run-HellpodTest.ps1` loads `hellpod.py` and [`test_main.py`](test_main.py) into the board's RAM via the raw REPL and starts them; **nothing is written to the flash**. Close the Companion and VS Code / MicroPico first, so the COM port is free.

```powershell
Software\tools\hellpod-animation\Run-HellpodTest.ps1             # finds the board by USB VID 2E8A
Software\tools\hellpod-animation\Run-HellpodTest.ps1 -Port COM8
```

| Button | Action |
|-|-|
| 2_1 / 2_2 | Animation A (vertical) / B (horizontal); a new press restarts it at once |
| 2_3 | The LED flash, for comparison |
| 1_1 / 1_2 | A / B in slow motion (x4) |
| 1_4 short | Next duration: 500, 700, 1000, 400 ms |
| 1_4 held 2 s | Hard reset: the normal firmware starts again (replugging the board does the same) |

After an animation the yellow line shows its duration and frame rate. An error shows "Test error" on the display and turns all LEDs red; the traceback goes to the serial port.
