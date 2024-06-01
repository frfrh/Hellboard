# Hellboard

This is a Macro board based on RP2040-zero and Micropython, meant for ordering Stratagems for Helldivers 2.

# Setup

1. Install [Visual Studio Code](https://code.visualstudio.com/).
2. Install the [MicroPico extension](https://marketplace.visualstudio.com/items?itemName=paulober.pico-w-go).
3. Flash the firmware onto the RP2040:
    1. Press the button (`Boot`) and connect the Board to a PC.
    2. Copy the [UF2-file](https://github.com/frfrh/Hellboard/blob/master/Firmware/libs/3rd/firmware_with_HID_support.v.1.17.uf2) into the drive `RPI-RP2`.
    3. Re-Connect the board.
4. Load the following Python files into the board. In Visual Studio Code left click on the file, then right click the file and select `Upload current file to Pico`.
    - `libs/3rd/keyboard.py`
    - `libs/3rd/ssd1306.py`
    - `libs/helldiver.py`
    - `libs/macro.py`
    - `main.py`