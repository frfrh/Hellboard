# Board spike

Checks on the real Hellboard whether the installed MicroPython (v1.17 build) supports everything the new firmware and the updater need (implementation plan P7, [hellboard-protocol.md](../../doc/spec/hellboard-protocol.md) §8):

- Ctrl-C stops `main.py`, raw REPL works (basis of the firmware updater)
- JSON lines over the USB serial port, read **non-blocking** with `select.poll` while the main loop keeps running
- `json`, `hashlib.sha256`, base64, `os.rename`, `machine.bootloader`
- free RAM and flash

`spike.py` is executed from RAM; **nothing is written to the board's flash**. At the end the board is soft-reset and `main.py` runs again.

## Run

1. Connect the Hellboard. Close VS Code / disconnect MicroPico, so the COM port is free.
2. In PowerShell:

```powershell
cd Software\tools\board-spike
.\Run-BoardSpike.ps1             # finds the board by USB VID 2E8A
.\Run-BoardSpike.ps1 -Port COM5  # if several RP2040 boards are connected
```

3. All checks should show `PASS`. Paste the "Raw results" line into the chat.

If the script stops with an error, unplug and replug the board – it then starts normally with `main.py`.
