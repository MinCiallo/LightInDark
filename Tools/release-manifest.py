#!/usr/bin/env python3
"""Record release hashes and emit a separate whitelist example, never the live official.json."""
import hashlib
import json
import re
import struct
from pathlib import Path

root = Path(__file__).resolve().parents[1]
version = re.search(r'public const string Version = "([^"]+)"',
                    (root / "Light/LightPlugin.cs").read_text(encoding="utf-8-sig")).group(1)
files = {}
for name in ("LightInDark.dll", "Light.dll"):
    data = (root / "Output" / name).read_bytes()
    digest = hashlib.sha256(data).digest()
    files[name] = {"bytes": len(data), "sha256": digest.hex(),
                   "md5": hashlib.md5(data).hexdigest(), "handshakeHash": struct.unpack("<i", digest[:4])[0]}
manifest = {"version": version, "configuration": "Release", "files": files}
official = {"versions": [{"version": version, "apiHash": files["LightInDark.dll"]["handshakeHash"],
                          "modHash": files["Light.dll"]["handshakeHash"]}]}
for name, content in (("release-manifest.json", manifest), ("official.generated.json", official)):
    path = root / "Output" / name
    path.write_text(json.dumps(content, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(path)
