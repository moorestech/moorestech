#!/usr/bin/env python3
"""アップロード直後の playtest-staging が今回のラベルのビルドで、Win/Mac depot が揃うかを確かめ validated-steam-build.json を書く。

Usage: validate_staging.py <run-dir>   (run-dir の名前がラベル)
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import steam_partner_api as api

if len(sys.argv) != 2 or sys.argv[1] in ("-h", "--help"):
    sys.exit(__doc__)
root = Path(sys.argv[1]).expanduser()
label = root.name
status = json.loads((root / "steam-upload-status.json").read_text())
if status["state"] != "uploaded":
    sys.exit("アップロードが完了していません: " + status["state"])
betas = api.call("GetAppBetas/v1/", {})["response"]["betas"]
builds = api.call("GetAppBuilds/v1/", {"count": "20"})["response"]["builds"]
build_id = betas["playtest-staging"]["BuildID"]
build = builds.get(str(build_id))
# staging は「Merged build <上げたビルド> into <前のビルド>」になるので、上げたビルド（Description=ラベル）の depot と照合する
# 同じラベルで上げ直した場合は最新のビルドを採る
sources = sorted((int(k), v) for k, v in builds.items() if v.get("Description") == "moorestech playtest staging " + label)
if not build or not sources:
    sys.exit("staging のビルドか、今回のラベルで上げたビルドが見つかりません")
source = sources[-1][1]
for depot in ("1958161", "1958162"):
    expected = source.get("depots", {}).get(depot, {}).get("DepotVersionGID")
    if not expected or expected != build.get("depots", {}).get(depot, {}).get("DepotVersionGID"):
        sys.exit("staging の depot manifest が今回のアップロードと一致しません: " + depot)
result = {"steamBuildLabel": label, "buildId": build_id, "branch": "playtest-staging", "depots": list(build["depots"])}
(root / "validated-steam-build.json").write_text(json.dumps(result, indent=2))
print(json.dumps(result))
