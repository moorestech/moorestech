#!/usr/bin/env python3
"""Windows 通し検証に合格した staging ビルドを playtest ブランチへライブ設定し、読み戻しで確認する。

Usage: promote_playtest.py <run-dir> (--mac-checked | --skip-mac-check "<ユーザー指示の引用>")
コミットは <runs>/<label>.release.log の commit= から読む。
"""
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import steam_partner_api as api

args = sys.argv[1:]
if len(args) < 2 or args[1] not in ("--mac-checked", "--skip-mac-check") or (args[1] == "--skip-mac-check" and len(args) != 3):
    sys.exit(__doc__)
root = Path(args[0]).expanduser()
label = root.name
release_log = root.parent / (label + ".release.log")
found = re.search(r"commit=([0-9a-f]{40})", release_log.read_text(errors="replace"))
if not found:
    sys.exit(f"{release_log} に commit= がありません")
commit = found.group(1)
# 受け口到達・ACK まで通った run だけを反映する（phase の result.json だけでは足りない）
logs = [release_log, root.parent / (label + ".upload-verify.log")]
if not any(f.exists() and f"[verify] passed: {label}" in f.read_text(errors="replace") for f in logs):
    sys.exit("Windows 通し検証の合格（[verify] passed）がログにありません")
validated = json.loads((root / "validated-steam-build.json").read_text())
if validated.get("steamBuildLabel") != label:
    sys.exit("validated-steam-build.json のラベルが一致しません")
build_id = validated["buildId"]
before = api.call("GetAppBetas/v1/", {})
(root / "betas-before-promotion.json").write_text(json.dumps(before, ensure_ascii=False, indent=2))
if before["response"]["betas"]["playtest-staging"]["BuildID"] != build_id:
    sys.exit("検証後に staging が別ビルドへ変わっています")
api.call("SetAppBuildLive/v2/", {"buildid": str(build_id), "betakey": "playtest",
                                 "description": "moorestech playtest " + label}, post=True)
after = api.call("GetAppBetas/v1/", {})
(root / "betas-after-promotion.json").write_text(json.dumps(after, ensure_ascii=False, indent=2))
if after["response"]["betas"]["playtest"]["BuildID"] != build_id:
    sys.exit("読み戻した playtest のビルドが一致しません")
# 次回のパッチノート比較元を引けるように反映記録を残す
released = {"steamBuildLabel": label, "buildId": build_id, "commit": commit,
            "macCheck": "checked" if args[1] == "--mac-checked" else "skipped: " + args[2]}
(root / "released.json").write_text(json.dumps(released, ensure_ascii=False, indent=2))
print(json.dumps({"success": True, **released}, ensure_ascii=False))
