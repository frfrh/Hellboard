# Hellboard application: button handling, display, LEDs and the protocol to the Companion
# (decisions F6, F11, F13). The initial assignment comes from config.json / saved.json, the
# Companion replaces loadout and configuration at runtime (RAM only until it asks to save).

import json
import sys
import time
from machine import Pin

import config
import log
import stratagems
from display import Display
from hid import Hid
from leds import Leds
from protocol import Protocol, ProtocolError, VERSION as PROTOCOL_VERSION, send

FIRMWARE_VERSION = '2.0.0-dev'

# Button name -> GPIO. Row 1: fixed 1_1 / 1_2, multi-function 1_3, cycle key 1_4. Row 2: loadout.
PINS = {'1_4': 0, '1_3': 1, '1_2': 2, '1_1': 3, '2_1': 29, '2_2': 28, '2_3': 27, '2_4': 26}
# Order of the buttons (and their LEDs in the chain); buttons are handled in this order.
ORDER = ('1_4', '1_3', '1_2', '1_1', '2_1', '2_2', '2_3', '2_4')
FIXED = ('1_1', '1_2')
LOADOUT = ('2_1', '2_2', '2_3', '2_4')
CYCLE = '1_4'
MULTI = '1_3'
CYCLE_DEBOUNCE_MS = 300
LOOP_MS = 50
MAX_MISSION = 16
LOG_LEVELS = ('debug', 'info', 'warn', 'error')


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
        self.pins = {}
        for name in PINS:
            self.pins[name] = Pin(PINS[name], Pin.IN, Pin.PULL_UP)
        self.last_cycle = time.ticks_ms()

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
                pressed = self.handle_buttons()
                self.draw()
                if pressed:
                    self.leds.confirm(len(ORDER))
                else:
                    self.leds.show([self.color(b) for b in ORDER])
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
        pressed = False
        for button in ORDER:
            if self.pins[button].value():
                continue        # pull-up: 0 = pressed
            if button == CYCLE:
                if time.ticks_diff(time.ticks_ms(), self.last_cycle) > CYCLE_DEBOUNCE_MS and self.multi:
                    self.last_cycle = time.ticks_ms()
                    self.index = (self.index + 1) % len(self.multi)
                    log.info('Multi-function button: %s' % self.multi[self.index].name)
                    pressed = True
            else:
                stratagem = self.stratagem(button)
                if stratagem and stratagem.code:
                    log.debug('Sending %s' % stratagem)
                    self.hid.send(stratagem.code)
                    pressed = True
        return pressed

    def color(self, button):
        if button == CYCLE:
            return 'white' if self.multi else None
        stratagem = self.stratagem(button)
        return stratagem.color if stratagem else None

    def draw(self):
        def name(button):
            stratagem = self.stratagem(button)
            return stratagem.name if stratagem else '-'

        self.display.show((
            (4, name(MULTI)),
            (18, '----------------'),
            (24, name('2_1')),
            (34, name('2_2')),
            (44, name('2_3')),
            (54, name('2_4')),
        ))

    # ---- protocol (hellboard-protocol.md section 4) --------------------------------------------

    def handle_request(self, command, request):
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
            return {}
        if command == 'saveConfig':
            self._save(self.config)
            self.persisted = _copy(self.config)
            log.info('Configuration saved')
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
                ('keyDelayMs', lambda v: isinstance(v, int) and 20 <= v <= 200),
                ('directionKeys', lambda v: v in ('arrows', 'wasd')),
                ('brightness', lambda v: isinstance(v, int) and 0 <= v <= 255),
                ('logLevel', lambda v: v in LOG_LEVELS),
            )
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
