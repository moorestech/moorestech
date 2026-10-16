#!/usr/bin/env python3
"""いま playtest ブランチで配信中のビルドID と、それを作った run・コミットを返す（パッチノートの比較元）。

Usage: find_live_playtest.py [runs-dir]   (既定 ~/hermes-agent/data/services/playtest/runs)
"""
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import steam_partner_api as api

if len(sys.argv) > 2 or (len(sys.argv) == 2 and sys.argv[1] in ("-h", "--help")):
    sys.exit(__doc__)
runs = Path(sys.argv[1] if len(sys.argv) == 2 else "~/hermes-agent/data/services/playtest/runs").expanduser()
live = api.call("GetAppBetas/v1/", {})["response"]["betas"]["playtest"]["BuildID"]
for validated in sorted(runs.glob("*/validated-steam-build.json")):
    if json.loads(validated.read_text()).get("buildId") != live:
        continue
    run = validated.parent
    commit = None
    if (run / "released.json").exists():
        commit = json.loads((run / "released.json").read_text()).get("commit")
    log = runs / (run.name + ".release.log")
    if not commit and log.exists():
        found = re.search(r"commit=([0-9a-f]{40})", log.read_text(errors="replace"))
        commit = found and found.group(1)
    print(json.dumps({"buildId": live, "run": str(run), "commit": commit}))
    sys.exit(0)
sys.exit(f"配信中のビルド {live} に対応する run が {runs} にありません。GetAppBuilds の Description で探す")
