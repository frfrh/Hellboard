# Stratagem data from stratagems.json (doc/stratagems.json of the project, decision F11).
# The file has exactly one stratagem per line, so it is read line by line and only the
# requested entries are parsed - the whole list never has to fit into RAM.

import json

PATH = 'stratagems.json'


class Stratagem:
    def __init__(self, id, name, color, code):
        self.id = id
        self.name = name
        self.color = color      # 'red', 'green', 'blue', 'yellow' or 'white'
        self.code = code        # e.g. 'URDDD'; '' if not known

    def __str__(self):
        return '%s (%s)' % (self.name, self.code)


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
