# Board configuration (decision F11): built-in defaults < config.json (shipped with the
# firmware) < saved.json (written by "Set as default" in the Companion, decision F3).
# Stratagems are referenced by their id in stratagems.json.

import json

FILES = ('config.json', 'saved.json')

DEFAULTS = {
    'fixed': {'1_1': 'reinforce', '1_2': 'resupply'},
    'loadout': [None, None, None, None],
    'multiFunction': [],
    'modifierKey': 0x06,
    'modifierToggle': False,
    'keyDelayMs': 50,
    'directionKeys': 'arrows',
    'brightness': 80,
    'logLevel': 'info',
}


def load(files=FILES):
    """Returns the merged configuration and the list of files that could not be read."""
    config = {}
    for key in DEFAULTS:
        config[key] = DEFAULTS[key]
    problems = []
    for name in files:
        try:
            with open(name) as f:
                data = json.load(f)
        except OSError:
            continue        # file not present
        except ValueError:
            problems.append(name)
            continue
        for key in data:
            if key in DEFAULTS:
                config[key] = data[key]
    return config, problems
