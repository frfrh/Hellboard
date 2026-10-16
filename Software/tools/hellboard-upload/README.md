# hellboard-upload

Installs the Hellboard firmware files onto the board – a replacement for uploading the files one by one with MicroPico. Development tool of phase P7; its Core classes (`HellboardCompanion.Core.Hellboard.Update`) become the firmware updater of the Companion (implementation plan §6.10).

## What it does

1. Reads [`Firmware/manifest.json`](../../../Firmware/manifest.json): which files go onto the board and under which name (all in the board's root directory, incl. `doc/stratagems.json`, decision F11).
2. Stops the running `main.py` (Ctrl-C) and enters the raw REPL.
3. Writes every file as `name.new` (base64 chunks) and compares its SHA-256, computed on the board, with the local file.
4. Only when all files are verified: the old file becomes `name.bak`, the new one takes its place. If anything fails, the `.new` files are removed and already replaced files are restored.
5. Soft reset – the board starts with the new files.

Files on the board that are not in the manifest (e.g. `saved.json`, the configuration saved from the Companion) are not touched; files listed under `obsolete` (leftovers of older firmware versions) are removed.

## Usage

Close VS Code / MicroPico and the Companion first, so the COM port is free. From `Software/`:

```powershell
dotnet run tools/hellboard-upload/hellboard-upload.cs -- --list        # show the files, no board needed
dotnet run tools/hellboard-upload/hellboard-upload.cs                  # find the board (USB VID 2E8A) and install
dotnet run tools/hellboard-upload/hellboard-upload.cs -- --port COM8
```

Recovery if the board does not start any more: copy back a `*.bak` file with MicroPico, or re-flash as described in the root README.
