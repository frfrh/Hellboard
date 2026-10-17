# Board configuration (decisions F11, F13): built-in defaults < config.json (shipped with the
# firmware) < saved.json (written by "Set as default" / "Save configuration on the Hellboard" in
# the Companion, F3). Stratagems are given by their id in stratagems.json (config.json) or as
# complete definitions (saved.json, sent by the Companion).

import json
import os

SHIPPED = 'config.json'
SAVED = 'saved.json'

# Keys of the configuration (setConfig / saveConfig) and of the default loadout (saveDefault).
CONFIG_KEYS = ('fixed', 'modifierKey', 'modifierToggle', 'menuDelayMs', 'keyPressMs', 'keyReleaseMs', 'directionKeys',
               'brightness', 'logLevel', 'screenSaverSeconds', 'scrollText')
LOADOUT_KEYS = ('loadout', 'multiFunction')

DEFAULTS = {
    'fixed': {'1_1': 'reinforce', '1_2': 'resupply'},
    'loadout': [None, None, None, None],
    'multiFunction': [],
    'modifierKey': 0x06,
    'modifierToggle': False,
    'menuDelayMs': 50,          # F15: after pressing the stratagem key, until the menu is open
    'keyPressMs': 50,           # F15: arrow key held
    'keyReleaseMs': 50,         # F15: pause between arrows
    'directionKeys': 'arrows',
    'brightness': 80,
    'logLevel': 'info',
    'screenSaverSeconds': 0,    # F15: 0 = off
    'scrollText': 'long',       # F16: 'off', 'long' (back and forth) or 'loop' (ticker)
}


def load():
    """Returns (configuration, files that could not be read, keys that came from saved.json)."""
    config = {}
    for key in DEFAULTS:
        config[key] = DEFAULTS[key]
    problems = []
    saved_keys = []
    for name in (SHIPPED, SAVED):
        data = _read(name, problems)
        if 'keyDelayMs' in data:   # firmware 2.0: one delay for pressing and releasing
            data.setdefault('keyPressMs', data['keyDelayMs'])
            data.setdefault('keyReleaseMs', data['keyDelayMs'])
        for key in data:
            if key in DEFAULTS:
                config[key] = data[key]
                if name == SAVED:
                    saved_keys.append(key)
    return config, problems, saved_keys


def save(values):
    """Merges values into saved.json; written to a temporary file first (power loss safe)."""
    data = _read(SAVED, [])
    for key in values:
        data[key] = values[key]
    temp = SAVED + '.tmp'
    with open(temp, 'w') as f:
        json.dump(data, f)
    try:
        os.rename(temp, SAVED)
    except OSError:
        try:
            os.remove(SAVED)
        except OSError:
            pass
        os.rename(temp, SAVED)


def _read(name, problems):
    try:
        with open(name) as f:
            data = json.load(f)
        if isinstance(data, dict):
            return data
        problems.append(name)
    except OSError:
        pass            # file not present
    except ValueError:
        problems.append(name)
    return {}
