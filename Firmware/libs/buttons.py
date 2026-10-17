# Button input with debouncing and press / hold / release events (decision F14). Ideas from the
# branch changesMartin by JoeGurt: a button acts once per press, the cycle button knows a long press.

import time

DEBOUNCE_MS = 30        # a mechanical switch bounces for a few ms; ignore changes within this time
LONG_PRESS_MS = 400     # held this long: long press ("hold" event)
REPEAT_MS = 300         # while still held: another "hold" event every REPEAT_MS

PRESS = 'press'
HOLD = 'hold'
RELEASE = 'release'     # released before LONG_PRESS_MS (short press)


class Button:
    def __init__(self, pin):
        self._pin = pin
        self.down = False
        self.held = False           # a "hold" event was reported during this press
        self._changed = time.ticks_ms()
        self._next_hold = 0

    def poll(self, now):
        """Returns PRESS, HOLD, RELEASE or None. A release after a long press returns None."""
        raw = not self._pin.value()         # pull-up: 0 = pressed
        if raw != self.down and time.ticks_diff(now, self._changed) >= DEBOUNCE_MS:
            self.down = raw
            self._changed = now
            if raw:
                self.held = False
                self._next_hold = time.ticks_add(now, LONG_PRESS_MS)
                return PRESS
            return None if self.held else RELEASE
        if self.down and time.ticks_diff(now, self._next_hold) >= 0:
            self.held = True
            self._next_hold = time.ticks_add(now, REPEAT_MS)
            return HOLD
        return None
