# SSD1306 OLED (128x64, rotated). Names are only shortened here, by the display width
# (protocol: the PC always sends full names; a scrolling text may follow later).

from machine import Pin, I2C
import time

WIDTH = 128
HEIGHT = 64


class Display:
    def __init__(self):
        self._oled = None
        self._lines = None
        try:
            from ssd1306 import SSD1306_I2C
            i2c = I2C(1, sda=Pin(14), scl=Pin(15))
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

    def show(self, lines):
        """lines: (y, text) pairs; redraws only when something changed."""
        if self._oled is None or lines == self._lines:
            return
        self._lines = lines
        self._oled.fill(0)
        for y, text in lines:
            self._oled.text(text, 0, y)
        self._oled.show()
