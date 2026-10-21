# Hellboard application: button handling, display, LEDs and the protocol to the Companion
# (decisions F6, F11, F13, F14-F16; button behavior, key timings, screen saver and scrolling text
# based on the branch changesMartin by JoeGurt). The initial assignment comes from config.json / saved.json, the
# Companion replaces loadout and configuration at runtime (RAM only until it asks to save).

import json
import sys
import time
from machine import Pin

import config
import log
import stratagems
from buttons import Button, PRESS, HOLD, RELEASE
from display import Display
from hid import Hid
from leds import Leds
from protocol import Protocol, ProtocolError, VERSION as PROTOCOL_VERSION, send


def _firmware_version():
    """version.json is written by the installer from the version in Firmware/manifest.json."""
    try:
        with open('version.json') as f:
            return json.load(f).get('version', 'unknown')
    except (OSError, ValueError):
        return 'dev'


FIRMWARE_VERSION = _firmware_version()

# Button name -> GPIO. Row 1: fixed 1_1 / 1_2, multi-function 1_3, cycle key 1_4. Row 2: loadout.
PINS = {'1_4': 0, '1_3': 1, '1_2': 2, '1_1': 3, '2_1': 29, '2_2': 28, '2_3': 27, '2_4': 26}
# Order of the buttons (and their LEDs in the chain); buttons are handled in this order.
ORDER = ('1_4', '1_3', '1_2', '1_1', '2_1', '2_2', '2_3', '2_4')
FIXED = ('1_1', '1_2')
LOADOUT = ('2_1', '2_2', '2_3', '2_4')
CYCLE = '1_4'
MULTI = '1_3'
LOOP_MS = 10                # F14: buttons are polled every 10 ms
CONFIRM_MS = 150            # all LEDs white after a key press
LOGO_MS = 2000              # F16: start-up logo
MAX_MISSION = 16
LOG_LEVELS = ('debug', 'info', 'warn', 'error')

# Feedback on the board (decision F4); for a received loadout the hellpod animation can replace the
# LED flash (transferFeedback, decision F17)
ANIMATIONS = {'hellpodHorizontal': 'horizontal', 'hellpodVertical': 'vertical'}
MESSAGE_MS = 2000           # a message replaces the separator line for this long
FLASH_STEP_MS = 120         # LED flash: on, off, on, off
FLASH_STEPS = 4
PC_TIMEOUT_MS = 15000       # the Companion pings every 10 s; no request for longer = no PC
SEPARATOR = '----------------'


def _copy(value):
    return json.loads(json.dumps(value))


class App:
    def __init__(self):
        cfg, problems, saved_keys = config.load()
        log.set_level(cfg['logLevel'])
        log.info('Firmware %s starting' % FIRMWARE_VERSION)
        for name in problems:
            log.error('%s is not valid JSON, ignored' % name)

        fixed = cfg['fixed'] if isinstance(cfg['fixed'], dict) else {}
        values = [fixed.get(b) for b in FIXED] + list(cfg['loadout']) + list(cfg['multiFunction'])
        try:
            table = stratagems.load([v for v in values if isinstance(v, str)])
        except OSError:
            log.error('stratagems.json not found - no stratagems available')
            table = {}

        def resolve(value):
            if isinstance(value, str):
                if value not in table:
                    log.warn('Unknown stratagem id: %s' % value)
                return table.get(value)
            if isinstance(value, dict):
                try:
                    return stratagems.from_definition(value)
                except ValueError as e:
                    log.warn('Invalid stratagem in configuration: %s' % e)
            return None

        self.slots = {}
        for button in FIXED:
            self.slots[button] = resolve(fixed.get(button))
        loadout = list(cfg['loadout']) + [None] * 4
        for i, button in enumerate(LOADOUT):
            self.slots[button] = resolve(loadout[i])
        self.multi = [s for s in (resolve(v) for v in cfg['multiFunction']) if s]
        self.index = 0
        self.source = 'default' if 'loadout' in saved_keys or 'multiFunction' in saved_keys else 'builtin'

        self.config = {}
        for key in config.CONFIG_KEYS:
            self.config[key] = cfg[key]
        self.config['fixed'] = self._fixed_definitions()
        self.persisted = _copy(self.config)
        self.running = True

        self.protocol = Protocol(self.handle_request)
        self.hid = Hid(cfg, idle=self.protocol.receive)
        self.leds = Leds(cfg['brightness'])
        self.display = Display()
        if not self.display.ready:
            log.warn('Display not available: %s' % self.display.error)
        self.prepare_animation()
        self.prepare_due = False
        self.buttons = {}
        for name in PINS:
            self.buttons[name] = Button(Pin(PINS[name], Pin.IN, Pin.PULL_UP))
        now = time.ticks_ms()
        self.last_activity = now     # F15: button press or transfer; the screen saver starts after that
        self.cycle_waking = False    # the current press of the cycle button only woke the display
        self.confirm_until = now
        self.display.logo(FIRMWARE_VERSION)
        self.logo_until = time.ticks_add(now, LOGO_MS)
        self.last_request = None
        self.message = None
        self.message_since = 0
        self.flash = None           # None, 'colors' (received / saved) or 'error'
        self.flash_since = 0

    def prepare_animation(self):
        scene = ANIMATIONS.get(self.config['transferFeedback'])
        if scene:
            try:
                self.display.prepare(scene)
            except MemoryError:
                log.error('Not enough memory for the hellpod animation')

    def stratagem(self, button):
        if button == MULTI:
            return self.multi[self.index] if self.multi else None
        return self.slots.get(button)

    def run(self):
        log.info('Ready: %s' % self._describe())
        send({'v': PROTOCOL_VERSION, 'event': 'hello', 'fw': FIRMWARE_VERSION, 'protocol': PROTOCOL_VERSION})
        while self.running:
            try:
                self.protocol.poll()
                self.handle_buttons()
                self.draw()
                self.show_leds()
                if self.prepare_due:
                    self.prepare_due = False
                    self.prepare_animation()
            except Exception as e:
                log.error('Main loop: %s' % e)
                self.hid.release_all()
                time.sleep_ms(1000)
            time.sleep_ms(LOOP_MS)

        # enterUpdateMode: leave everything off and return to the REPL.
        self.hid.release_all()
        self.leds.show([None] * len(ORDER))
        self.display.show(((24, 'Update...'),))

    # ---- buttons, display, LEDs --------------------------------------------------------------

    def handle_buttons(self):
        """F14: a stratagem button sends once per press; the cycle button moves forward on a short
        press and backward on a long press (repeated while held). While the screen saver is active,
        a stratagem button still sends, the cycle button only wakes the display."""
        now = time.ticks_ms()
        for button in ORDER:
            event = self.buttons[button].poll(now)
            if event is None:
                continue
            asleep = self.asleep(now)
            self.last_activity = now
            if button == CYCLE:
                if event == PRESS:
                    self.cycle_waking = asleep
                elif not self.cycle_waking and self.multi:
                    step = 1 if event == RELEASE else -1
                    self.index = (self.index + step) % len(self.multi)
                    log.info('Multi-function button: %s' % self.multi[self.index].name)
                    self.confirm(now)
            elif event == PRESS:
                stratagem = self.stratagem(button)
                if stratagem and stratagem.code:
                    log.debug('Sending %s' % stratagem)
                    self.confirm(now)
                    self.show_leds()
                    self.hid.send(stratagem.code)

    def confirm(self, now):
        self.confirm_until = time.ticks_add(now, CONFIRM_MS)

    def asleep(self, now=None):
        """F15: screen saver after screenSaverSeconds without activity (0 = off)."""
        seconds = self.config['screenSaverSeconds']
        now = time.ticks_ms() if now is None else now
        return seconds > 0 and time.ticks_diff(now, self.last_activity) > seconds * 1000

    def color(self, button):
        if button == CYCLE:
            return 'white' if self.multi else None
        stratagem = self.stratagem(button)
        return stratagem.color if stratagem else None

    def pc_connected(self):
        return self.last_request is not None and time.ticks_diff(time.ticks_ms(), self.last_request) < PC_TIMEOUT_MS

    def notify(self, message, flash=None):
        """Shows a message for MESSAGE_MS instead of the separator line and optionally flashes the LEDs (F4)."""
        now = time.ticks_ms()
        self.message = message
        self.message_since = now
        self.last_activity = now        # wakes the display, so the message can be read
        if flash:
            self.flash = flash
            self.flash_since = now

    def show_leds(self):
        if time.ticks_diff(self.confirm_until, time.ticks_ms()) > 0:
            self.leds.confirm(len(ORDER))
            return
        if self.flash and time.ticks_diff(time.ticks_ms(), self.flash_since) < FLASH_STEP_MS * FLASH_STEPS:
            if time.ticks_diff(time.ticks_ms(), self.flash_since) // FLASH_STEP_MS % 2:
                self.leds.show([None] * len(ORDER))
            elif self.flash == 'error':
                self.leds.show(['red'] * len(ORDER), bright=True)
            else:
                self.leds.show([self.color(b) for b in ORDER], bright=True)
            return
        self.flash = None
        self.leds.show([self.color(b) for b in ORDER])

    def separator(self):
        if self.message and time.ticks_diff(time.ticks_ms(), self.message_since) < MESSAGE_MS:
            return self.message
        self.message = None
        return SEPARATOR[:-2] + 'PC' if self.pc_connected() else SEPARATOR

    def draw(self):
        now = time.ticks_ms()
        if time.ticks_diff(self.logo_until, now) > 0:
            return
        if self.display.animate():
            return
        if self.asleep(now):
            self.display.blank()
            return

        def name(button):
            stratagem = self.stratagem(button)
            return stratagem.name if stratagem else '-'

        self.display.show((
            (4, name(MULTI)),
            (18, self.separator()),
            (24, name('2_1')),
            (34, name('2_2')),
            (44, name('2_3')),
            (54, name('2_4')),
        ), self.config['scrollText'], now)

    # ---- protocol (hellboard-protocol.md section 4) --------------------------------------------

    def handle_request(self, command, request):
        if not self.pc_connected():
            self.notify('PC connected')
        self.last_request = time.ticks_ms()
        try:
            return self._handle(command, request)
        except ProtocolError as e:
            if e.code != 'unknown_command':
                self.notify('Transfer error', 'error')
            raise

    def _handle(self, command, request):
        if command == 'ping':
            return {'board': 'hellboard', 'fw': FIRMWARE_VERSION, 'protocol': PROTOCOL_VERSION,
                    'micropython': '.'.join(str(x) for x in sys.implementation.version)}
        if command == 'getState':
            return {'stratagems': [self._definition(self.slots[b]) for b in LOADOUT],
                    'missionStratagems': [s.to_dict() for s in self.multi],
                    'config': self.config, 'configSaved': self.config == self.persisted, 'source': self.source}
        if command == 'setLoadout':
            return self._set_loadout(request)
        if command == 'setConfig':
            return self._set_config(request.get('config'))
        if command == 'saveDefault':
            self._save({'loadout': [self._definition(self.slots[b]) for b in LOADOUT],
                        'multiFunction': [s.to_dict() for s in self.multi]})
            log.info('Loadout saved as default')
            self.notify('Default saved', 'colors')
            return {}
        if command == 'saveConfig':
            self._save(self.config)
            self.persisted = _copy(self.config)
            log.info('Configuration saved')
            self.notify('Config saved', 'colors')
            return {'configSaved': True}
        if command == 'getLog':
            return {'entries': log.entries()}
        if command == 'enterUpdateMode':
            log.info('Entering update mode')
            self.running = False
            return {}
        raise ProtocolError('unknown_command', str(command))

    def _set_loadout(self, request):
        items = request.get('stratagems')
        mission = request.get('missionStratagems')
        if not isinstance(items, list) or len(items) != 4:
            raise ProtocolError('invalid_loadout', 'stratagems must have 4 entries')
        if not isinstance(mission, list) or len(mission) > MAX_MISSION:
            raise ProtocolError('invalid_loadout', 'missionStratagems must have 0-%d entries' % MAX_MISSION)
        try:
            loadout = [stratagems.from_definition(s) if s is not None else None for s in items]
            multi = [stratagems.from_definition(s) for s in mission]
        except ValueError as e:
            raise ProtocolError('invalid_loadout', str(e))

        for i, button in enumerate(LOADOUT):
            self.slots[button] = loadout[i]
        self.multi = multi
        self.index = 0
        self.source = 'pc'
        log.info('Loadout received: %s' % self._describe())
        scene = ANIMATIONS.get(self.config['transferFeedback'])
        if scene and self.display.ready:
            self.notify('Loadout received')
            self.flash = None               # a running LED flash of an earlier transfer ends
            self.display.play(scene)        # replaces a running animation at once
        else:
            self.notify('Loadout received', 'colors')
        return {}

    def _set_config(self, changes):
        if not isinstance(changes, dict):
            raise ProtocolError('invalid_config', 'config missing')
        try:
            fixed = None
            if 'fixed' in changes:
                fixed = {}
                given = changes['fixed'] or {}
                for button in FIXED:
                    value = given.get(button, self.config['fixed'].get(button))
                    fixed[button] = stratagems.from_definition(value) if value is not None else None
            checks = (
                ('modifierKey', lambda v: isinstance(v, int) and 4 <= v <= 231),
                ('modifierToggle', lambda v: isinstance(v, bool)),
                ('menuDelayMs', lambda v: isinstance(v, int) and 0 <= v <= 1000),
                ('keyPressMs', lambda v: isinstance(v, int) and 20 <= v <= 200),
                ('keyReleaseMs', lambda v: isinstance(v, int) and 20 <= v <= 200),
                ('directionKeys', lambda v: v in ('arrows', 'wasd')),
                ('brightness', lambda v: isinstance(v, int) and 0 <= v <= 255),
                ('logLevel', lambda v: v in LOG_LEVELS),
                ('screenSaverSeconds', lambda v: isinstance(v, int) and 0 <= v <= 3600),
                ('scrollText', lambda v: v in ('off', 'long', 'loop')),
                ('transferFeedback', lambda v: v == 'flash' or v in ANIMATIONS),
            )
            if 'keyDelayMs' in changes:     # Companion 0.3: one delay for pressing and releasing
                changes = dict(changes)
                changes.setdefault('keyPressMs', changes['keyDelayMs'])
                changes.setdefault('keyReleaseMs', changes['keyDelayMs'])
            for key, valid in checks:
                if key in changes and not valid(changes[key]):
                    raise ValueError('invalid %s' % key)
        except ValueError as e:
            raise ProtocolError('invalid_config', str(e))

        changed = []
        for key, valid in checks:
            if key in changes and self.config[key] != changes[key]:
                self.config[key] = changes[key]
                changed.append(key)
        if fixed is not None:
            for button in FIXED:
                self.slots[button] = fixed[button]
            definitions = self._fixed_definitions()
            if definitions != self.config['fixed']:
                self.config['fixed'] = definitions
                changed.append('fixed')

        if 'transferFeedback' in changed:
            self.prepare_due = True         # after the answer: preparing takes longer than the PC waits
        self.hid.configure(self.config)
        self.leds.set_brightness(self.config['brightness'])
        log.set_level(self.config['logLevel'])
        if changed:
            log.info('Configuration changed: %s' % ', '.join(changed))
        return {'config': self.config, 'configSaved': self.config == self.persisted}

    def _save(self, values):
        try:
            config.save(values)
        except OSError as e:
            log.error('Saving failed: %s' % e)
            raise ProtocolError('storage_error', str(e))

    def _fixed_definitions(self):
        result = {}
        for button in FIXED:
            result[button] = self._definition(self.slots.get(button))
        return result

    @staticmethod
    def _definition(stratagem):
        return stratagem.to_dict() if stratagem else None

    def _describe(self):
        names = ', '.join(s.name if s else '-' for s in (self.stratagem(b) for b in LOADOUT))
        return names + ' | ' + (', '.join(s.name for s in self.multi) or '-')
