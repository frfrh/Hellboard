# Hellpod impact animation on the display: the feedback for a received loadout instead of the LED
# flash (transferFeedback, decision F17). Two scenes: 'vertical' (the pod falls from the top) and
# 'horizontal' (it flies in from the right, only in the blue part of the display).
#
# Every scene is drawn in "world" coordinates: the pod falls straight towards +y and hits the ground
# at y = ground. A rotated view maps the world onto the display so that the pod flies from right to
# left. Speed matters (floats are slow on the RP2040): the ground is rendered once into background
# buffers and the pod with its dark outline is a sprite, both copied; only the dust is computed per
# frame. A frame takes 1-16 ms to draw plus 26 ms to send at 400 kHz, about 29 frames per second.
# The PC preview (Software/tools/hellpod-animation/preview.py) uses this file as well.

import math
import framebuf

# Pod, nose down; the nose is the bottom pixel of the middle column.
SPRITE = (
    '#.........#',
    '##.#####.##',
    '###########',
    '.#########.',
    '.#.......#.',
    '.#########.',
    '.#########.',
    '.#########.',
    '.#########.',
    '..#######..',
    '..#.....#..',
    '..#######..',
    '..#######..',
    '...#####...',
    '...#####...',
    '....###....',
    '....###....',
    '.....#.....',
)
NOSE_X = 5
POD_LENGTH = len(SPRITE)

IMPACT_AT = 0.34        # share of the duration spent falling
DEPTH = 13              # how deep the nose ends up in the ground: only the rear end stays above it
DIG_IN = 0.15           # share of the impact phase the pod needs to bore into the ground
DUST = 40
DEBRIS = 10
SHAKE = 3               # pixels the impact shakes the picture at first
CLEAR = 2               # sprite value that blit leaves out


def _ease_out(u):
    return 1 - (1 - u) * (1 - u)


class _Random:
    """Small LCG, so the dust looks the same on every run and in the preview."""
    def __init__(self, seed):
        self.state = seed

    def next(self, low, high):
        self.state = (self.state * 1103515245 + 12345) & 0x7FFFFFFF
        return low + (high - low) * (self.state / 0x7FFFFFFF)


class View:
    """World -> display. rotated: the world is 64 wide and 128 high, its +y points to the display's left.
    shake moves everything along the fall axis."""
    def __init__(self, fb, rotated, shake=0):
        self.fb = fb
        self.rotated = rotated
        self.shake = shake

    def map(self, x, y):
        y = int(y) + self.shake
        if self.rotated:
            return 127 - y, int(x)
        return int(x), y

    def pixel(self, x, y):
        sx, sy = self.map(x, y)
        self.fb.pixel(sx, sy, 1)

    def line(self, x0, y0, x1, y1):
        a = self.map(x0, y0)
        b = self.map(x1, y1)
        self.fb.line(a[0], a[1], b[0], b[1], 1)

    def rect(self, x, y, w, h):
        sx, sy = self.map(x, y)
        if self.rotated:
            self.fb.fill_rect(sx - h + 1, sy, h, w, 1)
        else:
            self.fb.fill_rect(sx, sy, w, h, 1)


class Scene:
    """x: where the pod comes down; ground: y of the ground line; width / height: world size;
    top: display rows (from the top) kept empty. The ground is shown in section, so the buried pod
    stays visible."""
    def __init__(self, rotated, x, ground, width, height, top=0):
        self.rotated = rotated
        self.top = top
        self.x = x
        self.ground = ground
        self.width = width
        self.height = height
        self.bg = None

        rnd = _Random(7)
        self.dust = []
        for i in range(DUST):
            angle = rnd.next(0.12, math.pi - 0.12)
            reach = rnd.next(8, 30) if i % 4 else rnd.next(24, 42)
            self.dust.append((math.cos(angle) * reach, -math.sin(angle) * reach * 0.55,
                              i % 2 == 0, rnd.next(0.5, 1.0)))
        self.debris = []
        for i in range(DEBRIS):
            self.debris.append((rnd.next(-70, 70), rnd.next(-150, -80)))
        self.rays = []
        for k in range(7):
            a = math.pi * (k + 0.5) / 7
            self.rays.append((math.cos(a), -math.sin(a) * 0.7))

    def _prepare(self):
        """Background with the ground and the pod sprite, built on first use."""
        bg = framebuf.FrameBuffer(bytearray(128 * 64 // 8), 128, 64, framebuf.MONO_VLSB)
        view = View(bg, self.rotated)
        g = self.ground
        view.rect(0, g, self.width, 1)
        for row in range(2, self.height - g, 3):
            for x in range(row % 5, self.width, 5):
                view.pixel(x, g + row)
        self.bg = bg
        # the background once per shake offset, so every frame starts with a plain copy
        self.backgrounds = {}
        for shake in range(-SHAKE, SHAKE + 1):
            buffer = bytearray(128 * 64 // 8)
            shifted = framebuf.FrameBuffer(buffer, 128, 64, framebuf.MONO_VLSB)
            if self.rotated:
                shifted.blit(bg, -shake, 0)
            else:
                shifted.blit(bg, 0, shake)
            self.backgrounds[shake] = (buffer, shifted)

        # pod pixels as display offsets from the nose, plus a one pixel dark outline around them
        pod = set()
        for row, line in enumerate(SPRITE):
            for col, c in enumerate(line):
                if c == '#':
                    ox, oy = col - NOSE_X, row - (POD_LENGTH - 1)
                    pod.add((-oy, ox) if self.rotated else (ox, oy))
        outline = set()
        for x, y in pod:
            for ddx in (-1, 0, 1):
                for ddy in (-1, 0, 1):
                    outline.add((x + ddx, y + ddy))
        left = min(x for x, y in outline)
        top = min(y for x, y in outline)
        w = max(x for x, y in outline) - left + 1
        h = max(y for x, y in outline) - top + 1
        stride = (w + 3) // 4 * 4
        sprite = framebuf.FrameBuffer(bytearray(stride * h // 4), w, h, framebuf.GS2_HMSB)
        sprite.fill(CLEAR)
        for x, y in outline:
            sprite.pixel(x - left, y - top, 1 if (x, y) in pod else 0)
        self.sprite = sprite
        self.sprite_at = (left, top)

    def _pod(self, view, nose_y):
        sx, sy = view.map(self.x, nose_y)
        view.fb.blit(self.sprite, sx + self.sprite_at[0], sy + self.sprite_at[1], CLEAR)

    def draw(self, fb, progress):
        """progress 0..1 over the whole animation; draws the complete frame."""
        self.prepare()
        if progress < IMPACT_AT:
            self._background(fb)
            self._fall(View(fb, self.rotated), progress / IMPACT_AT)
        else:
            self._impact_frame(fb, (progress - IMPACT_AT) / (1 - IMPACT_AT))
        if self.top:
            fb.fill_rect(0, 0, 128, self.top, 0)

    def _impact_frame(self, fb, u):
        shake = 0
        if u < 0.3:                             # shake along the fall axis, decaying
            shake = int(SHAKE * (1 - u / 0.3) + 0.5)
            if int(u * 40) % 2:
                shake = -shake
        self._background(fb, shake)
        self._impact(View(fb, self.rotated, shake), u)

    def _background(self, fb, shake=0):
        buffer, shifted = self.backgrounds[shake]
        target = getattr(fb, 'buffer', None)        # SSD1306: copying the bytes is much faster
        if target is not None and len(target) == len(buffer):
            target[:] = buffer
        else:
            fb.fill(0)
            fb.blit(shifted, 0, 0)

    def prepare(self):
        if self.bg is None:
            self._prepare()

    def _fall(self, view, p):
        travel = p * p                          # accelerates until the impact
        nose = -2 + (self.ground + 2) * travel
        # speed lines behind the pod; the faster, the longer
        tail = nose - POD_LENGTH + 1
        trail = 6 + 34 * travel
        x = self.x
        view.line(x, tail, x, tail - trail)
        view.line(x - 3, tail, x - 3, tail - trail * 0.55)
        view.line(x + 3, tail, x + 3, tail - trail * 0.55)
        self._pod(view, nose)

    def _impact(self, view, u):
        ix = self.x
        g = self.ground
        view.rect(ix - 11, g - 1, 5, 1)          # crater rim
        view.rect(ix + 7, g - 1, 5, 1)

        # flash: short rays at the moment of the impact
        if u < 0.15:
            r0 = 4 + 40 * u
            r1 = r0 + 9
            for cx, cy in self.rays:
                view.line(ix + cx * r0, g + cy * r0, ix + cx * r1, g + cy * r1)

        # dust fronts running along the ground
        if u < 0.65:
            reach = 5 + 55 * _ease_out(u / 0.65)
            height = 3 if u < 0.35 else 2 if u < 0.5 else 1
            view.rect(ix - reach - 2, g - height, 4, height)
            view.rect(ix + reach - 2, g - height, 4, height)
            view.pixel(ix - reach + 5, g - 1)
            view.pixel(ix + reach - 5, g - 1)

        # dust cloud
        spread = _ease_out(min(1, u * 1.6))
        base = g - 1 - 6 * u
        for cx, cy, big, life in self.dust:
            if u > life:
                continue
            if big and u < life * 0.7:
                view.rect(ix + cx * spread, base + cy * spread, 2, 2)
            else:
                view.pixel(ix + cx * spread, base + cy * spread)

        # debris on ballistic paths (time in seconds as if the impact phase took 0.33 s)
        t = u * 0.33
        fall = g - 2 + 300 * t * t
        for vx, vy in self.debris:
            y = fall + vy * t
            if y < g - 1:
                view.rect(ix + vx * t, y, 2, 2)

        # the pod bores into the ground; its outline cuts the ground line and the soil
        self._pod(view, g + DEPTH * _ease_out(min(1, u / DIG_IN)))


SCENES = {
    'vertical': Scene(False, 64, 52, 128, 64),          # top -> bottom
    'horizontal': Scene(True, 40, 116, 64, 128, 16),    # right -> left, only in the blue part
}


class Player:
    """Plays one scene at a time without blocking: frame() draws the next picture from the main loop.
    start() takes over at once, also while another animation is still running (transfers often
    follow each other quickly while the loadout is chosen in the game)."""
    def __init__(self, ticks_ms, ticks_diff):
        self._ticks_ms = ticks_ms
        self._ticks_diff = ticks_diff
        self.scene = None
        self.frames = 0

    def start(self, scene, duration_ms):
        scene.prepare()                         # background and sprite, before the clock starts
        self.scene = scene
        self.duration = duration_ms
        self.started = self._ticks_ms()
        self.frames = 0

    def stop(self):
        self.scene = None

    @property
    def running(self):
        return self.scene is not None

    def frame(self, fb, show):
        """Draws and shows the current picture; False once the animation is over (nothing drawn)."""
        if self.scene is None:
            return False
        t = self._ticks_diff(self._ticks_ms(), self.started)
        if t >= self.duration:
            self.scene = None
            return False
        self.scene.draw(fb, t / self.duration)
        show()
        self.frames += 1
        return True
