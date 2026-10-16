# Log entries for the Companion (decision F12): every entry is printed as a protocol event
# {"v":1,"event":"log",...} and the last entries are kept in RAM for the getLog command.
# Nothing is written to flash.

import json
import time

LEVELS = ('debug', 'info', 'warn', 'error')
MAX_ENTRIES = 32
MAX_LENGTH = 200

_min_level = 1
_entries = []


def set_level(name):
    global _min_level
    if name in LEVELS:
        _min_level = LEVELS.index(name)


def entries():
    return list(_entries)


def debug(message):
    _log(0, message)


def info(message):
    _log(1, message)


def warn(message):
    _log(2, message)


def error(message):
    _log(3, message)


def _log(level, message):
    if level < _min_level:
        return
    entry = {'level': LEVELS[level], 't': time.ticks_ms(), 'msg': str(message)[:MAX_LENGTH]}
    _entries.append(entry)
    if len(_entries) > MAX_ENTRIES:
        _entries.pop(0)
    try:
        print(json.dumps({'v': 1, 'event': 'log', 'level': entry['level'], 't': entry['t'], 'msg': entry['msg']}))
    except Exception:
        pass
