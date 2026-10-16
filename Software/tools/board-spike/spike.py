# SPDX-License-Identifier: GPL-3.0-or-later
#
# Hellboard board spike (implementation plan P7, hellboard-protocol.md §8).
# Runs from RAM via the raw REPL (Run-BoardSpike.ps1); writes nothing to the flash.
# 1. Read-only checks of the MicroPython capabilities the new firmware needs.
# 2. Echo phase: reads JSON lines from sys.stdin non-blocking (select.poll) while a
#    simulated main loop keeps running, and answers like the real protocol would.

import sys
import gc

results = {}


def check(name, fn):
    try:
        results[name] = fn()
    except Exception as e:  # noqa: BLE001 - every failure is a result
        results[name] = "FAIL " + repr(e)


def release_keys():
    # main.py may have been interrupted while a key was held down.
    import usb_hid
    usb_hid.report(usb_hid.KEYBOARD, bytes(8))
    return "ok"


def json_roundtrip():
    import json
    return json.dumps(json.loads('{"a":[1,"b",null,true]}'))


def sha256_abc():
    import hashlib
    import binascii
    return binascii.hexlify(hashlib.sha256(b"abc").digest()).decode()


def flash_info():
    import os
    s = os.statvfs("/")
    return {"block": s[0], "free_kb": s[0] * s[3] // 1024, "total_kb": s[0] * s[2] // 1024}


def poll_stdin():
    import select
    p = select.poll()
    p.register(sys.stdin, select.POLLIN)
    return "ok"


check("release_keys", release_keys)
check("micropython", lambda: ".".join(str(x) for x in sys.implementation.version))
check("platform", lambda: sys.platform)
check("json", json_roundtrip)
check("sha256", sha256_abc)
check("base64", lambda: __import__("binascii").a2b_base64(b"SGVsbG8=").decode())
check("bootloader_available", lambda: hasattr(__import__("machine"), "bootloader"))
check("rename_available", lambda: hasattr(__import__("os"), "rename"))
check("flash", flash_info)
check("files", lambda: __import__("os").listdir("/"))
check("poll_stdin", poll_stdin)
gc.collect()
check("ram_free", gc.mem_free)

import json  # noqa: E402
import select  # noqa: E402
import time  # noqa: E402

print(json.dumps({"v": 1, "event": "spike", "results": results}))

# ---- Echo phase ----------------------------------------------------------------------
poller = select.poll()
poller.register(sys.stdin, select.POLLIN)
buffer = ""
loops = 0
deadline = time.ticks_add(time.ticks_ms(), 20000)
while time.ticks_diff(deadline, time.ticks_ms()) > 0:
    loops += 1
    while poller.poll(0):
        ch = sys.stdin.read(1)
        if ch == "\n":
            line = buffer.strip()
            buffer = ""
            if not line.startswith("{"):
                continue
            try:
                msg = json.loads(line)
            except Exception as e:  # noqa: BLE001
                print(json.dumps({"v": 1, "ok": False, "error": "invalid_json", "message": repr(e)}))
                continue
            print(json.dumps({"v": 1, "ack": msg.get("id"), "ok": True, "echo": msg, "loops": loops}))
            if msg.get("cmd") == "quit":
                deadline = time.ticks_ms()
        elif ch != "\r":
            buffer += ch
    time.sleep_ms(5)  # stands for the button/display/LED work of the real main loop

print(json.dumps({"v": 1, "event": "spike_done", "loops": loops}))
