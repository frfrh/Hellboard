# Stratagem data from stratagems.json (doc/stratagems.json of the project, decision F11).
# The file has exactly one stratagem per line, so it is read line by line and only the
# requested entries are parsed - the whole list never has to fit into RAM.

import json

PATH = 'stratagems.json'
COLORS = ('red', 'green', 'blue', 'yellow', 'white')
MAX_NAME = 64
MAX_CODE = 10


class Stratagem:
    def __init__(self, id, name, color, code):
        self.id = id
        self.name = name
        self.color = color      # 'red', 'green', 'blue', 'yellow' or 'white'
        self.code = code        # e.g. 'URDDD'; '' if not known

    def __str__(self):
        return '%s (%s)' % (self.name, self.code)

    def to_dict(self):
        """Definition as in the protocol (hellboard-protocol.md section 4.2)."""
        result = {'name': self.name, 'color': self.color, 'sequence': self.code}
        if self.id:
            result['id'] = self.id
        return result


def from_definition(definition):
    """Stratagem from a protocol definition; raises ValueError if it is invalid."""
    if not isinstance(definition, dict):
        raise ValueError('stratagem must be an object')
    name = definition.get('name')
    color = definition.get('color')
    code = definition.get('sequence')
    if not isinstance(name, str) or not 1 <= len(name) <= MAX_NAME:
        raise ValueError('invalid name')
    if color not in COLORS:
        raise ValueError('invalid color for %s' % name)
    if not isinstance(code, str) or not 1 <= len(code) <= MAX_CODE or any(c not in 'UDLR' for c in code):
        raise ValueError('invalid sequence for %s' % name)
    id = definition.get('id')
    return Stratagem(id if isinstance(id, str) else None, name, color, code)


def load(ids, path=PATH):
    """Returns {id: Stratagem} for the given ids that exist in the file."""
    wanted = set(i for i in ids if i)
    entries = {}
    colors = {}
    with open(path) as f:
        while True:
            line = f.readline()
            if not line:
                break
            line = line.strip()
            if line.endswith(','):
                line = line[:-1]
            if line.startswith('"categories"'):
                colors = json.loads('{' + line + '}')['categories']
            elif line.startswith('{"id"'):
                start = line.find('"', 6) + 1
                if line[start:line.find('"', start)] in wanted:
                    entry = json.loads(line)
                    entries[entry['id']] = entry

    result = {}
    for id in entries:
        entry = entries[id]
        result[id] = Stratagem(id, entry['name'], colors.get(entry['category'], 'white'), entry.get('code') or '')
    return result
