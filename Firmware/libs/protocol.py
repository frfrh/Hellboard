# Hellboard protocol over the USB serial port (Software/doc/spec/hellboard-protocol.md, decision F1):
# newline-delimited JSON, read non-blocking from sys.stdin so the main loop never waits for the PC.

import json
import select
import sys

VERSION = 1
MAX_LINE = 4096
REMEMBERED_ANSWERS = 16


class ProtocolError(Exception):
    def __init__(self, code, message=''):
        Exception.__init__(self, message)
        self.code = code
        self.message = message


class Protocol:
    def __init__(self, handler):
        """handler(command, request) returns a result dict or raises ProtocolError."""
        self._handler = handler
        self._poll = select.poll()
        self._poll.register(sys.stdin, select.POLLIN)
        self._buffer = []
        self._length = 0
        self._overflow = False
        self._lines = []
        self._answers = {}
        self._answer_ids = []

    def receive(self):
        """Reads the available input into complete lines (also called while a macro is sent)."""
        while self._poll.poll(0):
            ch = sys.stdin.read(1)
            if ch == '\n':
                if self._overflow:
                    send({'v': VERSION, 'ok': False, 'error': 'line_too_long', 'message': 'max %d bytes' % MAX_LINE})
                else:
                    self._lines.append(''.join(self._buffer))
                self._buffer = []
                self._length = 0
                self._overflow = False
            elif ch != '\r':
                if self._length >= MAX_LINE:
                    self._overflow = True
                else:
                    self._buffer.append(ch)
                    self._length += 1

    def poll(self):
        """Reads the input and handles all complete requests."""
        self.receive()
        while self._lines:
            self._handle(self._lines.pop(0).strip())

    def _handle(self, line):
        if not line.startswith('{'):
            return      # echo or other text, e.g. from a terminal
        try:
            request = json.loads(line)
            if not isinstance(request, dict):
                raise ValueError('not an object')
        except ValueError as e:
            send({'v': VERSION, 'ok': False, 'error': 'invalid_json', 'message': str(e)})
            return

        id = request.get('id')
        if id is not None and id in self._answers:
            print(self._answers[id])        # repeated request: same answer (protocol section 3)
            return

        if request.get('v') != VERSION:
            answer = _error(id, 'unsupported_version', 'v must be %d' % VERSION)
        else:
            try:
                result = self._handler(request.get('cmd'), request) or {}
                answer = {'v': VERSION, 'ack': id, 'ok': True}
                for key in result:
                    answer[key] = result[key]
            except ProtocolError as e:
                answer = _error(id, e.code, e.message)
            except Exception as e:
                answer = _error(id, 'internal_error', str(e))

        text = json.dumps(answer)
        print(text)
        if id is not None:
            self._answers[id] = text
            self._answer_ids.append(id)
            if len(self._answer_ids) > REMEMBERED_ANSWERS:
                self._answers.pop(self._answer_ids.pop(0), None)


def send(message):
    print(json.dumps(message))


def _error(id, code, message):
    return {'v': VERSION, 'ack': id, 'ok': False, 'error': code, 'message': message}
