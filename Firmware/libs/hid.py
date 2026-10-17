# Keyboard output of stratagem codes. The only module that uses the HID API (decision F6),
# so the switch to the official MicroPython (F10, phase P7b) only touches this file.
# Scancodes: https://deskthority.net/wiki/Scancode

import time
import keyboard

ARROWS = {'U': 0x52, 'D': 0x51, 'L': 0x50, 'R': 0x4f}
WASD = {'U': 0x1a, 'D': 0x16, 'L': 0x04, 'R': 0x07}


class Hid:
    def __init__(self, config, idle=None):
        """idle() is called while waiting between key events, e.g. to keep reading the serial port (protocol section 1)."""
        self._keyboard = keyboard.Keyboard()
        self._idle = idle
        self.configure(config)

    def configure(self, config):
        """Key timings of decision F15: menu delay after the stratagem key, arrow held, pause between arrows."""
        self._modifier = config['modifierKey']
        self._toggle = config['modifierToggle']
        self._menu_delay = config['menuDelayMs']
        self._press = config['keyPressMs']
        self._release = config['keyReleaseMs']
        self._keys = WASD if config['directionKeys'] == 'wasd' else ARROWS

    def send(self, code):
        """Opens the stratagem menu with the modifier key and enters the code (U/D/L/R)."""
        k = self._keyboard
        k.press(self._modifier)
        self._wait(self._menu_delay)
        if self._toggle:
            k.release(self._modifier)
            self._wait(self._release)

        for direction in code:
            key = self._keys[direction]
            k.press(key)
            self._wait(self._press)
            k.release(key)
            self._wait(self._release)

        if not self._toggle:
            k.release(self._modifier)

    def _wait(self, ms):
        if self._idle:
            self._idle()
        time.sleep_ms(ms)

    def release_all(self):
        self._keyboard.release_all()
