#!/usr/bin/env python3
"""Restore the supplied, third-party binary from its lossless base64 representation."""
from pathlib import Path
import base64
import hashlib

root = Path(__file__).resolve().parents[1]
src = root / 'assets/particles/kolka/snow_more_snow.vpcf_c.b64'
dst = root / 'assets/particles/kolka/snow_more_snow.vpcf_c'
data = base64.b64decode(src.read_bytes(), validate=False)
expected = 'e825bb365c19dd54101798972bd331d41572a9b08f7158e18f40fdebe6296ff5'
if hashlib.sha256(data).hexdigest() != expected:
    raise SystemExit('Particle hash mismatch; file was not written.')
dst.write_bytes(data)
print(f'Wrote {dst} ({len(data)} bytes; SHA-256 OK)')
