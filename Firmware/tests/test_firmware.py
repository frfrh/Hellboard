# Tests the firmware logic (protocol, configuration, buttons) on the PC with CPython; the
# hardware modules (machine, usb_hid, ssd1306, select, stdin) are replaced by stubs.
# Run from the repository root:  python -B Firmware/tests/test_firmware.py   (-B: no __pycache__ in Firmware/libs)
import contextlib, io, json, os, shutil, sys, tempfile, time, types
sys.dont_write_bytecode = True     # no __pycache__ next to the firmware files

FW = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOC = os.path.join(os.path.dirname(FW), 'doc')

_t = [0]
time.sleep_ms = lambda ms: _t.__setitem__(0, _t[0] + ms)
time.ticks_ms = lambda: _t[0]
time.ticks_diff = lambda a, b: a - b
time.ticks_add = lambda a, b: a + b

pressed = set()
reports = []
spi_writes = []

machine = types.ModuleType('machine')
class Pin:
    IN = 0; PULL_UP = 1
    def __init__(self, no, *a): self.no = no
    def value(self): return 0 if self.no in pressed else 1
class SPI:
    MSB = 0
    def __init__(self, *a, **k): pass
    def write(self, data): spi_writes.append(bytes(data))
class I2C:
    def __init__(self, *a, **k): i2c_freq.append(k.get('freq'))
machine.Pin, machine.SPI, machine.I2C = Pin, SPI, I2C
sys.modules['machine'] = machine
usb_hid = types.ModuleType('usb_hid'); usb_hid.KEYBOARD = 1
usb_hid.report = lambda kind, report: reports.append(bytes(report))
sys.modules['usb_hid'] = usb_hid
framebuf = types.ModuleType('framebuf'); framebuf.MONO_HLSB, framebuf.MONO_VLSB, framebuf.GS2_HMSB = 0, 1, 2
class FrameBuffer:          # one value per pixel, enough for the hellpod animation
    def __init__(self, data, w, h, mode=0):
        self.width, self.height, self.pixels = w, h, bytearray(w * h)
    def fill(self, c): self.pixels[:] = bytes([c]) * len(self.pixels)
    def pixel(self, x, y, c):
        if 0 <= x < self.width and 0 <= y < self.height: self.pixels[y * self.width + x] = c
    def fill_rect(self, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w): self.pixel(xx, yy, c)
    def line(self, x0, y0, x1, y1, c):
        steps = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(steps + 1): self.pixel(x0 + (x1 - x0) * i // steps, y0 + (y1 - y0) * i // steps, c)
    def blit(self, src, x, y, key=-1):
        for yy in range(src.height):
            for xx in range(src.width):
                c = src.pixels[yy * src.width + xx]
                if c != key: self.pixel(x + xx, y + yy, c)
framebuf.FrameBuffer = FrameBuffer
sys.modules['framebuf'] = framebuf
ssd = types.ModuleType('ssd1306')
screen = []        # every shown screen: list of (y, text); 'logo' for the start-up logo; 'anim' for an animation frame
i2c_freq = []
class SSD1306_I2C(FrameBuffer):
    def __init__(self, w, h, *a, **k):
        super().__init__(None, w, h)
        self.buf, self.buffer = [], bytearray(w * h // 8)
    def rotate(self, r): pass
    def fill(self, c): self.buf = []; super().fill(c)
    def text(self, t, x, y): self.buf.append((y, t))
    def blit(self, fb, x, y, key=-1):
        if key == -1: self.buf.append('logo')
        else:
            self.buf.append('anim'); super().blit(fb, x, y, key)
    def show(self): screen.append(list(self.buf)); self.buf = []
ssd.SSD1306_I2C = SSD1306_I2C
sys.modules['ssd1306'] = ssd

class FakeStdin:
    def __init__(self): self.data = ''
    def read(self, n):
        c, self.data = self.data[:n], self.data[n:]
        return c
stdin = FakeStdin()
select = types.ModuleType('select'); select.POLLIN = 1
class Poll:
    def register(self, *a): pass
    def poll(self, timeout): return [1] if stdin.data else []
select.poll = Poll
sys.modules['select'] = select

sys.path[:0] = [FW + '/libs', FW + '/libs/3rd']
work = tempfile.mkdtemp()
shutil.copy(FW + '/config.json', work)
shutil.copy(DOC + '/stratagems.json', work)
os.chdir(work)

out = io.StringIO()
real_stdin = sys.stdin
with contextlib.redirect_stdout(out):
    sys.stdin = stdin
    import app
    import stratagems
    a = app.App()

# start-up logo with the firmware version, the normal screen only after LOGO_MS (F16)
assert screen[-1][0] == 'logo' and screen[-1][1][1] == 'vdev', screen[-1]
a.draw()
assert screen[-1][0] == 'logo', 'still the logo'
_t[0] += app.LOGO_MS + 1
a.draw()
assert screen[-1][2] == (24, 'Grenade Launcher'), screen[-1]

def request(obj):
    out.truncate(0); out.seek(0)
    stdin.data += json.dumps(obj) + '\r\n'
    with contextlib.redirect_stdout(out):
        a.protocol.poll()
    answers = [json.loads(l) for l in out.getvalue().splitlines() if l.startswith('{') and '"ack"' in l]
    assert len(answers) == 1, out.getvalue()
    return answers[0]

assert i2c_freq[-1] == 400000, 'the display is only specified up to 400 kHz'

r = request({'v': 1, 'id': 1, 'cmd': 'ping'})
assert r['ok'] and r['fw'] == app.FIRMWARE_VERSION and r['protocol'] == 1, r

r = request({'v': 1, 'id': 2, 'cmd': 'getState'})
assert r['source'] == 'builtin' and r['configSaved'] is True, r
assert r['config']['fixed']['1_1']['id'] == 'reinforce' and r['config']['fixed']['1_1']['sequence'] == 'UDRLU', r
assert r['stratagems'][0]['name'] == 'Grenade Launcher'

mg = {'name': 'Machine Gun', 'color': 'blue', 'sequence': 'DLDUR'}
assert a.config['transferFeedback'] == 'hellpodHorizontal', 'F17: the animation is the default'
assert request({'v': 1, 'id': 60, 'cmd': 'setConfig', 'config': {'transferFeedback': 'flash'}})['ok']
r = request({'v': 1, 'id': 3, 'cmd': 'setLoadout', 'stratagems': [mg, None, None, None], 'missionStratagems': [{'name': 'Hellbomb', 'color': 'yellow', 'sequence': 'DULDURDU'}]})
assert r['ok'], r

# feedback (F4, F17 'flash'): message instead of the separator, all LEDs flash white, then PC indicator
import display
assert a.separator() == 'Loadout received', a.separator()
spi_writes.clear()
a.show_leds()                       # flash phase 1: bright white
_t[0] += app.FLASH_STEP_MS
a.show_leds()                       # phase 2: off
assert len(spi_writes) == 2 and spi_writes[0] != spi_writes[1]
white = bytes(spi_writes[0])
assert len(set(white[i:i + 9] for i in range(0, len(white), 9))) == 1, 'all LEDs the same color'
assert spi_writes[1] == bytes(b'\x92\x49\x24' * 3 * 8), 'all LEDs off in the flash pause'
_t[0] += app.MESSAGE_MS
assert a.separator() == '--------------PC', a.separator()
assert a.flash is not None
a.show_leds()
assert a.flash is None, 'flash finished'
_t[0] += app.PC_TIMEOUT_MS
assert a.separator() == app.SEPARATOR, 'no request for 15 s: PC indicator off'
assert display.fit('StA-X3 W.A.S.P. Launcher') == 'StA-X3 W.A.S.P. '
assert display.fit('Castellan’s') == 'Castellan?s'
assert a.slots['2_1'].name == 'Machine Gun' and a.slots['2_2'] is None and a.multi[0].name == 'Hellbomb'

# F17: the hellpod animation instead of the LED flash; a new transfer restarts it at once
rc = request({'v': 1, 'id': 61, 'cmd': 'setConfig', 'config': {'transferFeedback': 'hellpodVertical'}})
assert rc['ok'] and rc['config']['transferFeedback'] == 'hellpodVertical' and a.prepare_due, rc
a.prepare_animation(); a.prepare_due = False
assert request({'v': 1, 'id': 62, 'cmd': 'setLoadout', 'stratagems': [mg, None, None, None], 'missionStratagems': []})['ok']
assert a.flash is None and a.separator() == 'Loadout received', 'no LED flash, the message stays'
spi_writes.clear()
a.show_leds()
assert spi_writes == [] or spi_writes[-1] != bytes(b'\x92\x49\x24' * 3 * 8), 'LEDs keep their colors'
a.draw()
assert 'anim' in screen[-1], 'animation frame (pod sprite) instead of the names'
started = a.display._player.started
_t[0] += 300
assert request({'v': 1, 'id': 63, 'cmd': 'setLoadout', 'stratagems': [mg, None, None, None], 'missionStratagems': []})['ok']
assert a.display._player.started == _t[0] != started, 'the second transfer restarts the animation at once'
_t[0] += display.ANIMATION_MS - 1
a.draw()
assert 'anim' in screen[-1], 'still running: restarted, not continued'
_t[0] += 1
a.draw()
assert screen[-1][2] == (24, 'Machine Gun'), 'normal screen after the animation'
rc = request({'v': 1, 'id': 64, 'cmd': 'setConfig', 'config': {'transferFeedback': 'fireworks'}})
assert rc['error'] == 'invalid_config', rc
rc = request({'v': 1, 'id': 65, 'cmd': 'setConfig', 'config': {'transferFeedback': 'hellpodHorizontal', 'unknownKey': 1}})
assert rc['ok'], 'unknown keys are ignored (that is how older firmware handles transferFeedback)'
assert a.config['transferFeedback'] == 'hellpodHorizontal' and 'unknownKey' not in a.config

# repeated id -> same answer, not executed again
r2 = request({'v': 1, 'id': 3, 'cmd': 'setLoadout', 'stratagems': [None, None, None, None], 'missionStratagems': []})
assert r2 == r and a.slots['2_1'].name == 'Machine Gun'

r = request({'v': 1, 'id': 4, 'cmd': 'setLoadout', 'stratagems': [mg], 'missionStratagems': []})
assert not r['ok'] and r['error'] == 'invalid_loadout', r
assert a.separator() == 'Transfer error' and a.flash == 'error'
spi_writes.clear()
a.show_leds()
red = bytes(spi_writes[0])
assert red != bytes(b'\x92\x49\x24' * 3 * 8) and red != white, 'errors flash red, also with the animation selected'
assert not a.display._player.running
r = request({'v': 1, 'id': 5, 'cmd': 'setLoadout', 'stratagems': [{'name': 'X', 'color': 'blue', 'sequence': 'UXD'}, None, None, None], 'missionStratagems': []})
assert r['error'] == 'invalid_loadout', r

r = request({'v': 1, 'id': 6, 'cmd': 'setConfig', 'config': {'modifierKey': 224, 'brightness': 120, 'fixed': {'1_2': {'id': 'sos_beacon', 'name': 'SOS Beacon', 'color': 'yellow', 'sequence': 'UDRU'}}}})
assert r['ok'] and r['configSaved'] is False and r['config']['modifierKey'] == 224, r
assert a.slots['1_2'].name == 'SOS Beacon' and a.slots['1_1'].name == 'Reinforce'
r = request({'v': 1, 'id': 7, 'cmd': 'setConfig', 'config': {'keyDelayMs': 5}})
assert r['error'] == 'invalid_config', r

r = request({'v': 1, 'id': 8, 'cmd': 'saveConfig'})
assert r['ok'] and r['configSaved'] is True, r
assert a.separator() == 'Config saved' and a.display._player.running and a.flash is None, 'F17: saves follow transferFeedback'
r = request({'v': 1, 'id': 9, 'cmd': 'saveDefault'})
assert r['ok'], r
assert a.separator() == 'Default saved' and a.display._player.running and a.flash is None
saved = json.load(open('saved.json'))
assert saved['modifierKey'] == 224 and saved['loadout'][0]['name'] == 'Machine Gun' and saved['fixed']['1_2']['name'] == 'SOS Beacon', saved

r = request({'v': 1, 'id': 10, 'cmd': 'getLog'})
assert any('Configuration saved' in e['msg'] for e in r['entries']), r

r = request({'v': 1, 'id': 11, 'cmd': 'nope'})
assert r['error'] == 'unknown_command'
r = request({'v': 2, 'id': 12, 'cmd': 'ping'})
assert r['error'] == 'unsupported_version'

def poll(ms=10):
    _t[0] += ms
    with contextlib.redirect_stdout(out):
        a.handle_buttons()

def hold(pin, ms):
    pressed.add(pin)
    for _ in range(ms // 10):
        poll()
    pressed.discard(pin)
    for _ in range(5):
        poll()

def sends():
    # a send starts when the modifier is pressed while nothing else is
    previous = bytes(8)
    count = 0
    for r in reports:
        if previous == bytes(8) and r[0] == 0x01 and r.count(0) == 7:
            count += 1
        previous = r
    return count

# modifier key now 224 (left Ctrl = modifier bit 0x01 in the report) and Machine Gun code
reports.clear()
hold(29, 100)
assert reports[0][0] == 0x01, reports[0]
arrows = {0x52: 'U', 0x51: 'D', 0x50: 'L', 0x4f: 'R'}
assert ''.join(arrows[r[2]] for r in reports if r[2] in arrows) == 'DLDUR'

# F14: holding a stratagem button sends it only once
reports.clear()
hold(29, 2000)
assert sends() == 1, sends()

# F14: debouncing - a 10 ms bounce does not count as a second press
reports.clear()
pressed.add(29); poll(); poll(); poll(); pressed.discard(29); poll(); pressed.add(29); poll(); pressed.discard(29)
for _ in range(10):
    poll()
assert sends() == 1, sends()

# F14: cycle button - short press forward, long press backward (repeated while held)
names = lambda: a.multi[a.index].name
a.multi = [stratagems.Stratagem('a', 'A', 'yellow', 'UUU'), stratagems.Stratagem('b', 'B', 'yellow', 'DDD'), stratagems.Stratagem('c', 'C', 'yellow', 'LLL')]
a.index = 0
hold(0, 100)
assert names() == 'B', names()
hold(0, 450)
assert names() == 'A', 'long press goes back'
hold(0, 450 + 300 + 20)
assert names() == 'B', 'held: back twice (A -> C -> B)'

# F15: key timings; keyDelayMs of an older Companion sets press and release
r = request({'v': 1, 'id': 20, 'cmd': 'setConfig', 'config': {'menuDelayMs': 400, 'keyDelayMs': 80}})
assert r['ok'] and r['config']['menuDelayMs'] == 400 and r['config']['keyPressMs'] == 80 and r['config']['keyReleaseMs'] == 80, r
start = _t[0]
hold(29, 30)
assert _t[0] - start >= 400 + 5 * 160, 'menu delay + 5 arrows x (80 + 80) ms'
r = request({'v': 1, 'id': 21, 'cmd': 'setConfig', 'config': {'menuDelayMs': 5000}})
assert r['error'] == 'invalid_config', r

# F15: screen saver - dark after the timeout; the cycle button only wakes, a stratagem button still sends
r = request({'v': 1, 'id': 22, 'cmd': 'setConfig', 'config': {'screenSaverSeconds': 1, 'menuDelayMs': 50, 'keyPressMs': 50, 'keyReleaseMs': 50}})
assert r['ok'], r
_t[0] += 3000
assert a.asleep()
a.draw()
assert screen[-1] == [], 'display dark'
index = a.index
hold(0, 100)
assert a.index == index and not a.asleep(), 'cycle button only woke the display'
_t[0] += 3000
reports.clear()
hold(29, 50)
assert sends() == 1, 'a stratagem button sends while the display is dark'
request({'v': 1, 'id': 23, 'cmd': 'setConfig', 'config': {'screenSaverSeconds': 0}})

# F16: scrolling text - only names longer than a line, pause, scroll to the end, pause, back
import display
d = display.Display()
long_name = 'StA-X3 W.A.S.P. Launcher'
assert d._visible(24, long_name, 'off', 0) == long_name[:16]
assert d._visible(24, long_name, 'long', 0) == long_name[:16]
assert d._visible(24, long_name, 'long', display.SCROLL_PAUSE_MS + display.SCROLL_STEP_MS * 3) == long_name[4:20], 'first step right after the pause'
assert d._visible(24, long_name, 'long', display.SCROLL_PAUSE_MS + display.SCROLL_STEP_MS * (len(long_name) - 16)) == long_name[-16:], 'stops at the end'
cycle = 2 * display.SCROLL_PAUSE_MS + (len(long_name) - 16) * display.SCROLL_STEP_MS
assert d._visible(24, long_name, 'long', cycle + 10) == long_name[:16], 'starts again'
assert d._visible(24, 'Machine Gun', 'long', 5000) == 'Machine Gun'

# F16 'loop': ticker - after the first pause the name runs on, followed by a gap, without jumping back
d2 = display.Display()
band = long_name + display.LOOP_GAP
at = lambda steps: display.SCROLL_PAUSE_MS + display.SCROLL_STEP_MS * steps
assert d2._visible(24, long_name, 'loop', 0) == long_name[:16]
assert d2._visible(24, long_name, 'loop', at(len(long_name) - 1)) == (band + band)[len(long_name):len(long_name) + 16], 'end, gap, start'
assert d2._visible(24, long_name, 'loop', at(len(band) - 1)) == long_name[:16], 'one full turn: back at the start without a jump'
assert d2._visible(24, long_name, 'loop', at(len(band))) == long_name[1:17], 'and on'
assert d2._visible(24, 'Machine Gun', 'loop', 5000) == 'Machine Gun', 'short names stay'
assert request({'v': 1, 'id': 25, 'cmd': 'setConfig', 'config': {'scrollText': 'loop'}})['ok']
r = request({'v': 1, 'id': 24, 'cmd': 'setConfig', 'config': {'scrollText': 'sideways'}})
assert r['error'] == 'invalid_config', r

# restart: saved.json wins over config.json
with contextlib.redirect_stdout(out):
    b = app.App()
assert b.source == 'default' and b.slots['2_1'].name == 'Machine Gun' and b.slots['1_2'].name == 'SOS Beacon'
assert b.config['modifierKey'] == 224 and b.config == b.persisted

# line too long
out.truncate(0); out.seek(0)
stdin.data += '{' + 'x' * 5000 + '}\n'
with contextlib.redirect_stdout(out):
    a.protocol.poll()
assert 'line_too_long' in out.getvalue()

# enterUpdateMode ends the main loop
r = request({'v': 1, 'id': 13, 'cmd': 'enterUpdateMode'})
assert r['ok'] and a.running is False

# a new connection reuses ids: after a ping the board must not answer from the old connection
r_old = request({'v': 1, 'id': 1, 'cmd': 'setConfig', 'config': {'brightness': 10}})
request({'v': 1, 'id': 900, 'cmd': 'ping'})
r_new = request({'v': 1, 'id': 1, 'cmd': 'setConfig', 'config': {'brightness': 99}})
assert r_new['config']['brightness'] == 99, 'executed again, not answered from the old connection'

# MicroPython has no Exception.__init__ (protocol errors became internal_error on the board)
for name in os.listdir(FW + '/libs'):
    if name.endswith('.py'):
        assert 'Exception.__init__(' not in open(FW + '/libs/' + name, encoding='utf-8').read(), name
r = request({'v': 1, 'id': 901, 'cmd': 'setConfig', 'config': {'scrollText': 'sideways'}})
assert r['error'] == 'invalid_config', r

sys.stdin = real_stdin
print('Firmware tests passed')
