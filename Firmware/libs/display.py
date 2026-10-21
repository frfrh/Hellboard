# SSD1306 OLED (128x64, rotated). Rows 0-15 are yellow, rows 16-63 blue (fixed by the hardware).
# Names are only shortened here, by the display width (protocol: the PC always sends full names);
# with scrollText = 'long' names longer than a line scroll back and forth, with 'loop' they run
# continuously like a ticker, followed by a gap (decision F16). Plays the hellpod animation after a
# transfer (decision F17); while it runs, it replaces the normal screen.

from machine import Pin, I2C
import time

import hellpod

WIDTH = 128
HEIGHT = 64
COLUMNS = WIDTH // 8        # 8x8 font

SCROLL_PAUSE_MS = 1500      # pause at the start and at the end of a scrolling name
SCROLL_STEP_MS = 300        # one character per step
LOOP_GAP = '    '           # 'loop': spaces between the end of a name and its next start
I2C_FREQ = 400000           # the SSD1306 is specified up to 400 kHz; set explicitly, not left to the default
ANIMATION_MS = 500


class Display:
    def __init__(self):
        self._oled = None
        self._shown = None
        self._scroll = {}
        self._player = hellpod.Player(time.ticks_ms, time.ticks_diff)
        try:
            from ssd1306 import SSD1306_I2C
            i2c = I2C(1, sda=Pin(14), scl=Pin(15), freq=I2C_FREQ)
            self._oled = SSD1306_I2C(WIDTH, HEIGHT, i2c, addr=0x3C)
            time.sleep_ms(100)
            self._oled.rotate(1)
        except Exception as e:
            self.error = str(e)
        else:
            self.error = None

    @property
    def ready(self):
        return self._oled is not None

    def show(self, lines, scroll='off', now=None):
        """lines: (y, text) pairs; redraws only when the visible content changed."""
        now = time.ticks_ms() if now is None else now
        visible = tuple((y, self._visible(y, text, scroll, now)) for y, text in lines)
        if self._oled is None or visible == self._shown:
            return
        self._shown = visible
        self._oled.fill(0)
        for y, text in visible:
            self._oled.text(text, 0, y)
        self._oled.show()

    def blank(self):
        """Screen saver (decision F15): everything dark."""
        if self._oled is None or self._shown == ():
            return
        self._shown = ()
        self._oled.fill(0)
        self._oled.show()

    def logo(self, version):
        """Start-up logo (decision F16) with the firmware version in the blue part."""
        if self._oled is None:
            return
        try:
            import framebuf
            import logo
            image = framebuf.FrameBuffer(bytearray(logo.BITMAP), logo.WIDTH, logo.HEIGHT, framebuf.MONO_HLSB)
            self._oled.fill(0)
            self._oled.blit(image, 0, 0)
            text = fit('v' + version)
            self._oled.text(text, WIDTH - 8 * len(text), HEIGHT - 8)
            self._oled.show()
            self._shown = None
        except Exception as e:
            self.error = str(e)

    def prepare(self, scene):
        """Renders background and sprite of a hellpod scene ahead (about 0.6 s), so a transfer
        starts the animation without delay."""
        if self._oled is not None:
            hellpod.SCENES[scene].prepare()

    def play(self, scene):
        """Starts a hellpod scene; replaces a running animation at once."""
        if self._oled is not None:
            self._player.start(hellpod.SCENES[scene], ANIMATION_MS)

    def animate(self):
        """Shows the next animation frame; False when no animation runs (any more)."""
        if not self._player.running:
            return False
        try:
            if self._player.frame(self._oled, self._oled.show):
                return True
        except Exception:
            self._player.stop()         # no endless repetition of the same error
            raise
        finally:
            self._shown = None          # the normal screen is redrawn afterwards
        return False

    def _visible(self, y, text, scroll, now):
        text = clean(text)
        if scroll not in ('long', 'loop') or len(text) <= COLUMNS:
            self._scroll.pop(y, None)
            return text[:COLUMNS]
        state = self._scroll.get(y)
        if state is None or state[0] != text:
            state = (text, now)             # a new name starts at its beginning
            self._scroll[y] = state
        if scroll == 'loop':
            # ticker: pause once at the start, then run on without jumping back
            elapsed = time.ticks_diff(now, state[1])
            if elapsed < SCROLL_PAUSE_MS:
                return text[:COLUMNS]
            band = text + LOOP_GAP
            offset = ((elapsed - SCROLL_PAUSE_MS) // SCROLL_STEP_MS + 1) % len(band)
            return (band + band)[offset:offset + COLUMNS]
        steps = len(text) - COLUMNS
        cycle = 2 * SCROLL_PAUSE_MS + steps * SCROLL_STEP_MS
        t = time.ticks_diff(now, state[1]) % cycle
        offset = 0 if t < SCROLL_PAUSE_MS else min(steps, (t - SCROLL_PAUSE_MS) // SCROLL_STEP_MS + 1)
        return text[offset:offset + COLUMNS]


def clean(text):
    """Characters the 8x8 font cannot show become '?'."""
    return ''.join(c if ' ' <= c <= '~' else '?' for c in text)


def fit(text, width=COLUMNS):
    """A name cut to one display line (static display)."""
    return clean(text)[:width]
