"""Exercise production layout methods with a minimal scene model (no Unity runtime on CI)."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
source = Path(sys.argv[1]) if len(sys.argv) > 1 else root / "Light/UI/Config/ConfigUIPanel.cs"
text = source.read_text(encoding="utf-8-sig")
names = ["MatchesFilter", "ContentStartY", "Relayout", "TryIncrementalVisibility", "ItemBelongsToCurrentPage", "LastRowY", "FindSpawned"]
methods = []
for name in names:
    m = re.search(r"^        (?:private|public|internal) static [^\n]*\b" + name + r"\(", text, re.M)
    if not m:
        if name == "ContentStartY":  # Also permits replay against the supplied pre-fix source.
            continue
        raise SystemExit(f"Missing production method: {name}")
    start = m.start()
    brace = text.find("{", m.end())
    arrow = text.find("=>", m.end())
    if arrow >= 0 and arrow < brace:
        end = text.index(";", arrow) + 1
    else:
        end = text.index("\n        }", brace) + len("\n        }")
    methods.append(text[start:end])
out = root / "Tests/Regression/obj/ConfigMethods.g.cs"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text("#nullable enable annotations\nusing UnityEngine;\nusing LightInDark.Configuration;\nusing LightInDark.Core;\nusing Object = UnityEngine.Object;\npartial class ConfigFixture {\n" + "\n".join(methods) + "\n}\n", encoding="utf-8")
print(f"Using {len(methods)} real config methods from {source}")

popup_source = Path(sys.argv[2]) if len(sys.argv) > 2 else root / "Light/UI/Window/DetailPopup.cs"
popup = popup_source.read_text(encoding="utf-8-sig")
methods = []
for name in ["TryFollowMouse", "GetReferenceWorldZ", "FindUiCameraFor", "Ensure", "Show", "ClampToViewport", "Update", "Hide", "Reset", "Destroy"]:
    m = re.search(r"^    (?:private|public|internal) static [^\n]*\b" + name + r"\(", popup, re.M)
    if not m:
        if name == "ClampToViewport":
            continue
        raise SystemExit(f"Missing popup method: {name}")
    brace = popup.index("{", m.end())
    end = popup.index("\n    }", brace) + len("\n    }")
    methods.append(popup[m.start():end])
(out.parent / "PopupMethods.g.cs").write_text("#nullable enable annotations\nusing UnityEngine;\nusing LightInDark.Core;\nusing Object = UnityEngine.Object;\npartial class PopupFixture {\n" + "\n".join(methods) + "\n}\n", encoding="utf-8")
print(f"Using {len(methods)} real tooltip methods from {popup_source}")
