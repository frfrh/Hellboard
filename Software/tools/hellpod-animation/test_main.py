# SPDX-License-Identifier: GPL-3.0-or-later
#
# Test program for the hellpod animations, run from RAM by Run-HellpodTest.ps1 (appended to
# Firmware/libs/hellpod.py, nothing is written to flash). Uses ssd1306.py and leds.py of the installed firmware.
#
# Buttons: 2_1 / 2_2 = animation A (vertical) / B (horizontal), 2_3 = the current LED flash for comparison,
# 1_1 / 1_2 = A / B in slow motion (x4), 1_4 short = next duration, 1_4 held 2 s = exit
# (hard reset, the normal firmware starts again). Like the firmware main loop nothing blocks: a new
# trigger replaces a running animation or flash at once.

import sys
import time
import machine
from machine import Pin, I2C
from ssd1306 import SSD1306_I2C
from leds import Leds
from hellpod import SCENES, Player     # board: removed when concatenated

PINS = {'1_4': 0, '1_3': 1, '1_2': 2, '1_1': 3, '2_1': 29, '2_2': 28, '2_3': 27, '2_4': 26}
ORDER = ('1_4', '1_3', '1_2', '1_1', '2_1', '2_2', '2_3', '2_4')
COLORS = {'1_4': 'white', '1_3': None, '1_2': 'blue', '1_1': 'blue',
          '2_1': 'yellow', '2_2': 'yellow', '2_3': 'red', '2_4': None}
TRIGGERS = {'2_1': ('A', 1), '2_2': ('B', 1), '1_1': ('A', 4), '1_2': ('B', 4)}    # label, slow motion
SCENE_OF = {'A': 'vertical', 'B': 'horizontal'}
DURATIONS = (500, 700, 1000, 400)
EXIT_MS = 2000
MESSAGE_MS = 2000
DEBOUNCE_MS = 30
FLASH_STEP_MS = 120         # app.py: LED flash on, off, on, off
FLASH_STEPS = 4
LOOP_MS = 10
I2C_FREQ = 400000         # the SSD1306 is specified up to 400 kHz; 1 MHz brought no visible gain
LABELS = ('A top>bottom', 'B right>left', 'LED flash', '')

i2c = I2C(1, sda=Pin(14), scl=Pin(15), freq=I2C_FREQ)
oled = SSD1306_I2C(128, 64, i2c, addr=0x3C)
oled.rotate(1)
leds = Leds(80)
pins = {name: Pin(PINS[name], Pin.IN, Pin.PULL_UP) for name in PINS}
colors = [COLORS[b] for b in ORDER]
for scene in SCENES.values():
    scene.prepare()


def screen(top, middle):
    oled.fill(0)
    oled.text(top[:16], 0, 4)
    oled.text(middle[:16], 0, 18)
    for i, label in enumerate(LABELS):
        oled.text(label, 0, 24 + 10 * i)
    oled.show()


class Button:
    """Debounced; poll() returns 'press' or 'release' on a change."""
    def __init__(self, pin):
        self.pin = pin
        self.down = False
        self.changed = 0
        self.since = 0

    def poll(self, now):
        raw = self.pin.value() == 0
        if raw == self.down or time.ticks_diff(now, self.changed) < DEBOUNCE_MS:
            return None
        self.down = raw
        self.changed = now
        if raw:
            self.since = now
        return 'press' if raw else 'release'


buttons = {name: Button(pins[name]) for name in pins}
player = Player(time.ticks_ms, time.ticks_diff)
duration = 0
status = 'Hellpod %d ms' % DURATIONS[duration]
message_until = 0
flash_since = None
played = None               # scene and duration of the running animation, for the status line
leds.show(colors)
screen(status, '----------------')

# An error must not leave a frozen picture behind: it is shown on the display, all LEDs turn red
# and the traceback goes to the serial port.
try:
    while True:
        now = time.ticks_ms()
        for name in ORDER:
            event = buttons[name].poll(now)
            if name == '1_4':
                if event == 'release':
                    duration = (duration + 1) % len(DURATIONS)
                    status = 'Hellpod %d ms' % DURATIONS[duration]
                    if not player.running:
                        screen(status, '----------------')
                elif buttons[name].down and time.ticks_diff(now, buttons[name].since) >= EXIT_MS:
                    leds.show([None] * 8)
                    oled.fill(0)
                    oled.text('Restart...', 0, 24)
                    oled.show()
                    time.sleep_ms(300)
                    machine.reset()
                continue
            if event != 'press':
                continue
            if name in TRIGGERS:
                key, slow = TRIGGERS[name]
                played = (key, DURATIONS[duration] * slow)
                player.start(SCENES[SCENE_OF[key]], played[1])        # replaces a running animation at once
                flash_since = None
                leds.show(colors)
            elif name == '2_3':
                player.stop()
                flash_since = now                           # restarts a running flash
                screen(status, 'Loadout received')
                message_until = time.ticks_add(now, MESSAGE_MS)

        if player.running:
            started = player.started
            if not player.frame(oled, oled.show):
                elapsed = time.ticks_diff(time.ticks_ms(), started)
                status = '%s %dms %dfps' % (played[0], played[1], player.frames * 1000 // max(1, elapsed))
                screen(status, 'Loadout received')
                message_until = time.ticks_add(time.ticks_ms(), MESSAGE_MS)
            continue

        if flash_since is not None:
            t = time.ticks_diff(now, flash_since)
            if t < FLASH_STEP_MS * FLASH_STEPS:
                leds.show([None] * 8 if t // FLASH_STEP_MS % 2 else colors, bright=True)
            else:
                flash_since = None
                leds.show(colors)

        if message_until and time.ticks_diff(now, message_until) >= 0:
            message_until = 0
            screen(status, '----------------')
        time.sleep_ms(LOOP_MS)
except Exception as e:
    sys.print_exception(e)
    leds.show(['red'] * 8)
    text = '%s: %s' % (type(e).__name__, e)
    try:                                # the display itself may be what failed
        oled.fill(0)
        oled.text('Test error', 0, 4)
        for i in range(5):
            oled.text(text[i * 16:i * 16 + 16], 0, 18 + 9 * i)
        oled.show()
    except Exception:
        pass
