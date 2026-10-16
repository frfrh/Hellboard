# Keyboard output of stratagem codes. The only module that uses the HID API (decision F6),
# so the switch to the official MicroPython (F10, phase P7b) only touches this file.
# Scancodes: https://deskthority.net/wiki/Scancode

import time
import keyboard

ARROWS = {'U': 0x52, 'D': 0x51, 'L': 0x50, 'R': 0x4f}
WASD = {'U': 0x1a, 'D': 0x16, 'L': 0x04, 'R': 0x07}


class Hid:
    def __init__(self, config):
        self._keyboard = keyboard.Keyboard()
        self.configure(config)

    def configure(self, config):
        self._modifier = config['modifierKey']
        self._toggle = config['modifierToggle']
        self._delay = config['keyDelayMs']
        self._keys = WASD if config['directionKeys'] == 'wasd' else ARROWS

    def send(self, code):
        """Opens the stratagem menu with the modifier key and enters the code (U/D/L/R)."""
        k = self._keyboard
        k.press(self._modifier)
        time.sleep_ms(self._delay)
        if self._toggle:
            k.release(self._modifier)
            time.sleep_ms(self._delay)

        for direction in code:
            key = self._keys[direction]
            k.press(key)
            time.sleep_ms(self._delay)
            k.release(key)
            time.sleep_ms(self._delay)

        if not self._toggle:
            k.release(self._modifier)

    def release_all(self):
        self._keyboard.release_all()
