# Hellboard

This is a Macro board based on RP2040-zero and Micropython, meant for ordering Stratagems for Helldivers 2 by registering as a HID-Keyboard.

![PXL_20240601_011015119 MP](https://github.com/frfrh/Hellboard/assets/93924020/1debbce7-9e01-4ee8-9f85-ac15d05eea98)

## Folders

##### [Firmware](https://github.com/frfrh/Hellboard/tree/master/Firmware)

Contains the firmware to be loaded into the RP2040 and the Micropython project files.

##### [Hardware](https://github.com/frfrh/Hellboard/tree/master/Hardware)

Contains the 3D files for the case and the button caps.

##### [PCB](https://github.com/frfrh/Hellboard/tree/master/PCB)

Contains the project files for the PCB and the gerber files to order one.
Project is based on KiCad 8.0.2.

## Configuration

Currently the configuration of the board is hardcoded. This is meant to change in (near) future.

You can change the stratagems assigned to the buttons at the top on [main.py](https://github.com/frfrh/Hellboard/blob/master/Firmware/main.py) by replacing the macros for `button_2_1`, `button_2_2`, `button_2_3` and `button_2_4`.

You can change the directional keys in https://github.com/frfrh/Hellboard/blob/master/Firmware/libs/macro.py.

You can change the modifyer key in https://github.com/frfrh/Hellboard/blob/master/Firmware/libs/helldiver.py. `modifierUseToggle = True` means that the button will only be pressed once to open the menue, `modifierUseToggle = False` means that the button stay pressed while entering the macro.

## Build your own

You can use the gerber files in https://github.com/frfrh/Hellboard/tree/master/PCB/gerber to order your own PCBs or order it directly from https://aisler.net/p/AHPZGPKK.
You will also need the following parts:

| Part | Amount | Link |
| - | - | - |
| RP2040-Zero | 1 | [link](https://www.waveshare.com/rp2040-zero.htm) |
| 0,96" OLED Display 128x64 | 1 | [link](https://de.aliexpress.com/item/1005005970901119.html) (the 26.7mm variant, blue PCB) |
| Kailh boxed switches | 8 | [link](https://www.caseking.de/ducky-kailh-box-navy-switches-mechanisch-3-pin-clicky-mx-stem-85g-110-stueck-gakc-444.html) |
| Kailh hot-swap socket | 8 | [link](https://de.aliexpress.com/item/1005006146911562.html) |
| WS2812b-2020 LED | 8 | [link](https://de.aliexpress.com/item/10000006244752.html) |
| N-Channel MOSFET[^levelshifter] | 1 |  |
| 10K 0805 resistor[^levelshifter] | 2 |  |
| M93C86-WMN6P EEPROM[^eeprom] | 1 | [link](https://www.reichelt.de/eeprom-seriell-16kb-2kx8-1kx16-2-5-5v-so-8-m93c86-wmn6p-p280935.html) |
| Button caps[^buttoncaps] | 8 | [link](https://de.aliexpress.com/item/1005003992982246.html) |

You can find icons for the buttons [here](https://github.com/nvigneux/Helldivers-2-Stratagems-icons-svg).


## Setup

1. Install [Visual Studio Code](https://code.visualstudio.com/).
2. Install the [MicroPico extension](https://marketplace.visualstudio.com/items?itemName=paulober.pico-w-go).
3. Flash the firmware onto the RP2040:
    1. Press the button (`Boot`) and connect the Board to a PC.
    2. Copy the [UF2-file](https://github.com/frfrh/Hellboard/blob/master/Firmware/libs/3rd/firmware_with_HID_support.v.1.17.uf2) into the drive `RPI-RP2`.
    3. Re-Connect the board.
4. Load the following Python files into the board. In Visual Studio Code left click on the file, then right click the file and select `Upload current file to Pico`.
    - `Firmware/libs/3rd/keyboard.py`
    - `Firmware/libs/3rd/ssd1306.py`
    - `Firmware/libs/helldiver.py`
    - `Firmware/libs/macro.py`
    - `Firmware/main.py`

[^levelshifter]: I got them by desoldering them from a level shifter I had lying around. [link](https://de.aliexpress.com/item/1005006068381598.html)
[^eeprom]: The EEPROM is currently optional. This is meant for future configuration of the loaded stratagems, currently those are hardcoded.
[^buttoncaps]: I used the button caps from [here](https://de.aliexpress.com/item/1005003992982246.html) but replaced the internals with the ones from `Hardware/Kailh-Cap.stl` as the original ones are not translucent. Any button caps compatible with Kailh boxed switches should do.
