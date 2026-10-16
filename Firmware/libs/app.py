# Hellboard application: button handling, display and LEDs (decision F6). Loadout and button
# assignment come from config.json / saved.json, the stratagem data from stratagems.json (F11).

import time
from machine import Pin

import config
import log
import stratagems
from display import Display
from hid import Hid
from leds import Leds

FIRMWARE_VERSION = '2.0.0-dev'

# Button name -> GPIO. Row 1: fixed 1_1 / 1_2, multi-function 1_3, cycle key 1_4. Row 2: loadout.
PINS = {'1_4': 0, '1_3': 1, '1_2': 2, '1_1': 3, '2_1': 29, '2_2': 28, '2_3': 27, '2_4': 26}
# Order of the buttons (and their LEDs in the chain); buttons are handled in this order.
ORDER = ('1_4', '1_3', '1_2', '1_1', '2_1', '2_2', '2_3', '2_4')
LOADOUT = ('2_1', '2_2', '2_3', '2_4')
CYCLE = '1_4'
MULTI = '1_3'
CYCLE_DEBOUNCE_MS = 300
LOOP_MS = 50


class App:
    def __init__(self):
        cfg, problems = config.load()
        log.set_level(cfg['logLevel'])
        log.info('Firmware %s starting' % FIRMWARE_VERSION)
        for name in problems:
            log.error('%s is not valid JSON, ignored' % name)

        fixed = cfg['fixed']
        ids = [fixed.get('1_1'), fixed.get('1_2')] + list(cfg['loadout']) + list(cfg['multiFunction'])
        try:
            table = stratagems.load(ids)
        except OSError:
            log.error('stratagems.json not found - no stratagems available')
            table = {}
        for id in ids:
            if id and id not in table:
                log.warn('Unknown stratagem id: %s' % id)

        self.slots = {'1_1': table.get(fixed.get('1_1')), '1_2': table.get(fixed.get('1_2'))}
        loadout = list(cfg['loadout']) + [None] * 4
        for i, name in enumerate(LOADOUT):
            self.slots[name] = table.get(loadout[i])
        self.multi = [table[id] for id in cfg['multiFunction'] if id in table]
        self.index = 0

        self.hid = Hid(cfg)
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
        log.info('Ready: %s' % ', '.join(s.name if s else '-' for s in (self.stratagem(b) for b in LOADOUT)))
        while True:
            try:
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
