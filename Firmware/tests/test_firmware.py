# Tests the firmware logic (protocol, configuration, buttons) on the PC with CPython; the
# hardware modules (machine, usb_hid, ssd1306, select, stdin) are replaced by stubs.
# Run from the repository root:  python Firmware/tests/test_firmware.py
import contextlib, io, json, os, shutil, sys, tempfile, time, types

FW = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOC = os.path.join(os.path.dirname(FW), 'doc')

_t = [0]
time.sleep_ms = lambda ms: _t.__setitem__(0, _t[0] + ms)
time.ticks_ms = lambda: _t[0]
time.ticks_diff = lambda a, b: a - b

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
    def __init__(self, *a, **k): pass
machine.Pin, machine.SPI, machine.I2C = Pin, SPI, I2C
sys.modules['machine'] = machine
usb_hid = types.ModuleType('usb_hid'); usb_hid.KEYBOARD = 1
usb_hid.report = lambda kind, report: reports.append(bytes(report))
sys.modules['usb_hid'] = usb_hid
ssd = types.ModuleType('ssd1306')
class SSD1306_I2C:
    def __init__(self, *a, **k): pass
    def rotate(self, r): pass
    def fill(self, c): pass
    def text(self, t, x, y): pass
    def show(self): pass
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
    a = app.App()

def request(obj):
    out.truncate(0); out.seek(0)
    stdin.data += json.dumps(obj) + '\r\n'
    with contextlib.redirect_stdout(out):
        a.protocol.poll()
    answers = [json.loads(l) for l in out.getvalue().splitlines() if l.startswith('{') and '"ack"' in l]
    assert len(answers) == 1, out.getvalue()
    return answers[0]

r = request({'v': 1, 'id': 1, 'cmd': 'ping'})
assert r['ok'] and r['fw'] == app.FIRMWARE_VERSION and r['protocol'] == 1, r

r = request({'v': 1, 'id': 2, 'cmd': 'getState'})
assert r['source'] == 'builtin' and r['configSaved'] is True, r
assert r['config']['fixed']['1_1']['id'] == 'reinforce' and r['config']['fixed']['1_1']['sequence'] == 'UDRLU', r
assert r['stratagems'][0]['name'] == 'Grenade Launcher'

mg = {'name': 'Machine Gun', 'color': 'blue', 'sequence': 'DLDUR'}
r = request({'v': 1, 'id': 3, 'cmd': 'setLoadout', 'stratagems': [mg, None, None, None], 'missionStratagems': [{'name': 'Hellbomb', 'color': 'yellow', 'sequence': 'DULDURDU'}]})
assert r['ok'], r

# feedback (F4): message instead of the separator, LEDs flash in their colors, then PC indicator
import display
assert a.separator() == 'Loadout received', a.separator()
spi_writes.clear()
a.show_leds(False)                       # flash phase 1: bright colors
_t[0] += app.FLASH_STEP_MS
a.show_leds(False)                       # phase 2: off
assert len(spi_writes) == 2 and spi_writes[0] != spi_writes[1]
assert spi_writes[1] == bytes(b'\x92\x49\x24' * 3 * 8), 'all LEDs off in the flash pause'
_t[0] += app.MESSAGE_MS
assert a.separator() == '--------------PC', a.separator()
assert a.flash is not None
a.show_leds(False)
assert a.flash is None, 'flash finished'
_t[0] += app.PC_TIMEOUT_MS
assert a.separator() == app.SEPARATOR, 'no request for 15 s: PC indicator off'
assert display.fit('StA-X3 W.A.S.P. Launcher') == 'StA-X3 W.A.S.P. '
assert display.fit('Castellan’s') == 'Castellan?s'
assert a.slots['2_1'].name == 'Machine Gun' and a.slots['2_2'] is None and a.multi[0].name == 'Hellbomb'

# repeated id -> same answer, not executed again
r2 = request({'v': 1, 'id': 3, 'cmd': 'setLoadout', 'stratagems': [None, None, None, None], 'missionStratagems': []})
assert r2 == r and a.slots['2_1'].name == 'Machine Gun'

r = request({'v': 1, 'id': 4, 'cmd': 'setLoadout', 'stratagems': [mg], 'missionStratagems': []})
assert not r['ok'] and r['error'] == 'invalid_loadout', r
assert a.separator() == 'Transfer error' and a.flash == 'error'
spi_writes.clear()
a.show_leds(False)
red = bytes(spi_writes[0])
assert red != bytes(b'\x92\x49\x24' * 3 * 8)
r = request({'v': 1, 'id': 5, 'cmd': 'setLoadout', 'stratagems': [{'name': 'X', 'color': 'blue', 'sequence': 'UXD'}, None, None, None], 'missionStratagems': []})
assert r['error'] == 'invalid_loadout', r

r = request({'v': 1, 'id': 6, 'cmd': 'setConfig', 'config': {'modifierKey': 224, 'brightness': 120, 'fixed': {'1_2': {'id': 'sos_beacon', 'name': 'SOS Beacon', 'color': 'yellow', 'sequence': 'UDRU'}}}})
assert r['ok'] and r['configSaved'] is False and r['config']['modifierKey'] == 224, r
assert a.slots['1_2'].name == 'SOS Beacon' and a.slots['1_1'].name == 'Reinforce'
r = request({'v': 1, 'id': 7, 'cmd': 'setConfig', 'config': {'keyDelayMs': 5}})
assert r['error'] == 'invalid_config', r

r = request({'v': 1, 'id': 8, 'cmd': 'saveConfig'})
assert r['ok'] and r['configSaved'] is True, r
r = request({'v': 1, 'id': 9, 'cmd': 'saveDefault'})
assert r['ok'], r
saved = json.load(open('saved.json'))
assert saved['modifierKey'] == 224 and saved['loadout'][0]['name'] == 'Machine Gun' and saved['fixed']['1_2']['name'] == 'SOS Beacon', saved

r = request({'v': 1, 'id': 10, 'cmd': 'getLog'})
assert any('Configuration saved' in e['msg'] for e in r['entries']), r

r = request({'v': 1, 'id': 11, 'cmd': 'nope'})
assert r['error'] == 'unknown_command'
r = request({'v': 2, 'id': 12, 'cmd': 'ping'})
assert r['error'] == 'unsupported_version'

# modifier key now 224 (left Ctrl = modifier bit 0x01 in the report) and Machine Gun code
reports.clear()
pressed.add(29)
with contextlib.redirect_stdout(out):
    a.handle_buttons()
pressed.clear()
assert reports[0][0] == 0x01, reports[0]
arrows = {0x52: 'U', 0x51: 'D', 0x50: 'L', 0x4f: 'R'}
assert ''.join(arrows[r[2]] for r in reports if r[2] in arrows) == 'DLDUR'

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

sys.stdin = real_stdin
print('Firmware tests passed')
