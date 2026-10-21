# SPDX-License-Identifier: GPL-3.0-or-later
#
# Renders the hellpod animations on the PC (needs Pillow; a stand-in replaces MicroPython's
# framebuf): one GIF per scene and a contact sheet with all frames, in the display colors
# (rows 0-15 yellow, 16-63 blue).
# Usage: python preview.py [out_dir] [fps] [duration_ms]

import os
import sys
import types

from PIL import Image

WIDTH, HEIGHT, SCALE = 128, 64, 4
YELLOW = (255, 214, 0)
BLUE = (90, 190, 255)


class FrameBuffer:
    """The parts of MicroPython's framebuf that hellpod.py uses (one value per pixel, any format)."""
    def __init__(self, buffer=None, width=WIDTH, height=HEIGHT, fmt=None):
        self.width, self.height = width, height
        self.pixels = bytearray(width * height)

    def fill(self, c):
        self.pixels[:] = bytes([c]) * len(self.pixels)

    def pixel(self, x, y, c=None):
        if 0 <= x < self.width and 0 <= y < self.height:
            if c is None:
                return self.pixels[y * self.width + x]
            self.pixels[y * self.width + x] = c

    def fill_rect(self, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.pixel(xx, yy, c)

    def blit(self, src, x, y, key=-1):
        for yy in range(src.height):
            for xx in range(src.width):
                c = src.pixels[yy * src.width + xx]
                if c != key:
                    self.pixel(x + xx, y + yy, c)

    def line(self, x0, y0, x1, y1, c):
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.pixel(x0, y0, c)
            if x0 == x1 and y0 == y1:
                return
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def image(self):
        img = Image.new('RGB', (WIDTH, HEIGHT))
        for y in range(HEIGHT):
            color = YELLOW if y < 16 else BLUE
            for x in range(WIDTH):
                if self.pixels[y * WIDTH + x]:
                    img.putpixel((x, y), color)
        return img.resize((WIDTH * SCALE, HEIGHT * SCALE), Image.NEAREST)


sys.modules['framebuf'] = types.SimpleNamespace(FrameBuffer=FrameBuffer, MONO_VLSB=0, GS2_HMSB=1)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'Firmware', 'libs'))
import hellpod  # noqa: E402  (Firmware/libs/hellpod.py; needs the framebuf stand-in above)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    fps = float(sys.argv[2]) if len(sys.argv) > 2 else 30
    duration = float(sys.argv[3]) if len(sys.argv) > 3 else 500
    os.makedirs(out, exist_ok=True)
    fb = FrameBuffer()
    frame_ms = 1000 / fps
    for name, scene in hellpod.SCENES.items():
        frames = []
        t = 0
        while t < duration:
            scene.draw(fb, t / duration)
            frames.append(fb.image())
            t += frame_ms
        fb.fill(0)
        frames.append(fb.image())       # back to the loadout (here: empty)
        durations = [int(frame_ms)] * (len(frames) - 1) + [700]
        frames[0].save(os.path.join(out, 'hellpod-%s.gif' % name), save_all=True,
                       append_images=frames[1:], duration=durations, loop=0)
        columns = 4
        rows = (len(frames) + columns - 1) // columns
        sheet = Image.new('RGB', (columns * (WIDTH * SCALE + 8), rows * (HEIGHT * SCALE + 8)), (40, 40, 40))
        for i, frame in enumerate(frames):
            sheet.paste(frame, ((i % columns) * (WIDTH * SCALE + 8), (i // columns) * (HEIGHT * SCALE + 8)))
        sheet.save(os.path.join(out, 'hellpod-%s-frames.png' % name))
        print(name, len(frames) - 1, 'frames')


if __name__ == '__main__':
    main()
