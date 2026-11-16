#!/usr/bin/env python3
"""Capture serial output from the Wokwi RFC2217 server.

Usage: python tools/serial_capture.py [port] [seconds]

The wokwi.toml must contain:  rfc2217ServerPort = 4000
Telnet negotiation bytes are stripped from the stream.
"""

import socket
import sys
import time

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def strip_telnet(data: bytes) -> bytes:
    out = bytearray()
    i = 0
    while i < len(data):
        b = data[i]
        if b == 255 and i + 2 < len(data):  # IAC + cmd + option
            i += 3
            continue
        out.append(b)
        i += 1
    return bytes(out)


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 4000
    seconds = float(sys.argv[2]) if len(sys.argv) > 2 else 12.0
    sock = socket.create_connection(("localhost", port), timeout=10)
    deadline = time.time() + seconds
    received = bytearray()
    print(f"--- capturing {seconds}s from localhost:{port} ---")
    while time.time() < deadline:
        try:
            sock.settimeout(1.0)
            chunk = sock.recv(4096)
            if not chunk:
                break
            received += strip_telnet(chunk)
        except socket.timeout:
            continue
    sock.close()
    text = received.decode("utf-8", errors="replace")
    print(text)
    print("--- end of capture ---")
    print("--- summary ---")
    if not received:
        print("NO SERIAL DATA RECEIVED")
        return 1
    for key, expected in [('"nhiet_do":', "25"), ('"do_am":', "60"),
                          ('"ap_suat":', "1013")]:
        found = [ln for ln in text.splitlines() if key in ln]
        if found:
            print(f"{key} found, e.g. {found[0].strip()[:120]}")
        else:
            print(f"{key} NOT FOUND")
    return 0


if __name__ == "__main__":
    sys.exit(main())
