# WS2812-compatible button LEDs, driven via SPI (one SPI byte carries ~3 LED bits).

from machine import Pin, SPI

CONFIRM = 0x80


class Leds:
    def __init__(self, brightness):
        self._spi = SPI(1, baudrate=2500000, polarity=1, phase=1, bits=8, firstbit=SPI.MSB,
                        sck=Pin(10), mosi=Pin(11), miso=Pin(12))
        self.set_brightness(brightness)
        self._last = None

    def set_brightness(self, brightness):
        b = brightness
        self._colors = {
            'yellow': (b << 16) + (b << 8),
            'red': b << 16,
            'green': b << 8,
            'blue': b,
            'white': (b << 16) + (b << 8) + b,
            None: 0,
        }
        self._last = None

    def show(self, colors):
        """colors: one color name (or None = off) per LED, in chain order."""
        self._write([self._colors.get(c, 0) for c in colors])

    def confirm(self, count):
        """All LEDs white, as feedback for a key press."""
        self._write([(CONFIRM << 16) + (CONFIRM << 8) + CONFIRM] * count)

    def _write(self, values):
        if values == self._last:
            return
        self._last = values
        data = bytearray()
        for value in values:
            data.extend(_byte(value >> 8 & 0xFF))   # order on the wire: G, R, B
            data.extend(_byte(value >> 16 & 0xFF))
            data.extend(_byte(value & 0xFF))
        self._spi.write(data)


def _byte(byte):
    data = bytearray(b'\x92\x49\x24')
    data[0] |= 0x01 if (byte & 0x20) else 0
    data[0] |= 0x08 if (byte & 0x40) else 0
    data[0] |= 0x40 if (byte & 0x80) else 0
    data[1] |= 0x04 if (byte & 0x08) else 0
    data[1] |= 0x20 if (byte & 0x10) else 0
    data[2] |= 0x02 if (byte & 0x01) else 0
    data[2] |= 0x10 if (byte & 0x02) else 0
    data[2] |= 0x80 if (byte & 0x04) else 0
    return data
