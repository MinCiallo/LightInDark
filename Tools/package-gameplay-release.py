#!/usr/bin/env python3
"""Package the current release and a replayable patch without altering the working tree."""
import difflib
import hashlib
import json
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
destination = Path(sys.argv[1]).resolve()
validation = Path(sys.argv[2]).resolve()
destination.mkdir(parents=True, exist_ok=True)

def git(*args):
    return subprocess.check_output(["git", *args], cwd=root)

def unified(before, after, source, target):
    chunks = []
    for line in difflib.unified_diff(before, after, fromfile=source, tofile=target):
        chunks.append(line if line.endswith("\n") else line+"\n\\ No newline at end of file\n")
    return "".join(chunks).encode()

base = git("rev-parse", "HEAD").decode().strip()
patch = b""
tracked = [p for p in git("diff", "--ignore-space-at-eol", "--numstat").decode().splitlines()
           if p.split("\t")[:2] != ["0", "0"]]
changed = [p.split("\t", 2)[2] for p in tracked]
for name in changed:
    old = git("show", "HEAD:"+name).decode("utf-8")
    current = (root / name).read_text(encoding="utf-8")
    # The extracted workspace contains mixed CRLF/LF. Match the baseline checkout's endings.
    newline = "\r\n" if old.count("\r\n") > old.count("\n") / 2 else "\n"
    current = newline.join(current.splitlines()) + (newline if current.endswith("\n") else "")
    patch += f"diff --git a/{name} b/{name}\n".encode()
    patch += unified(old.splitlines(keepends=True), current.splitlines(keepends=True),"a/"+name,"b/"+name)
new = git("ls-files", "--others", "--exclude-standard", "-z").decode().split("\0")
for name in filter(None, new):
    path = root / name
    if path.suffix not in {".cs", ".csproj", ".md", ".py", ".sh"}:
        continue
    content = path.read_text(encoding="utf-8").splitlines(keepends=True)
    patch += f"diff --git a/{name} b/{name}\nnew file mode 100644\n".encode()
    patch += unified([], content,"/dev/null","b/"+name)
    changed.append(name)
(destination / "changes.patch").write_bytes(patch)
with zipfile.ZipFile(destination / "source-changes.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    for name in sorted(set(changed)):
        archive.write(root / name, name)
shutil.copy2(validation, destination / "validation.log")
shutil.copy2(root / "Docs/能力会议中立与模式-API.md", destination / "API与验收说明.md")
files = {}
for name in ("Light.dll", "LightInDark.dll"):
    source = root / "Output" / name
    shutil.copy2(source, destination / name)
    data = source.read_bytes()
    files[name] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(),
                   "md5": hashlib.md5(data).hexdigest()}
manifest = {"baseCommit": base, "configuration": "Release", "files": files,
            "sourceFiles": sorted(set(changed)), "note": "Includes prior workspace fixes; originals are preserved."}
(destination / "release-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+"\n")
if (root / "Output/official.generated.json").exists():
    shutil.copy2(root / "Output/official.generated.json", destination / "official.generated.json")
with zipfile.ZipFile(destination.with_suffix(".zip"), "w", zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(destination.iterdir()):
        if path.is_file(): archive.write(path, destination.name+"/"+path.name)
print(json.dumps({"destination": str(destination), "sourceFiles": len(set(changed)), "files": files},ensure_ascii=False,indent=2))
