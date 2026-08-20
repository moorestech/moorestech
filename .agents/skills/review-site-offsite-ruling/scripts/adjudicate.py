#!/usr/bin/env python3
"""裁定サイトのadjudications.jsonへ、既存裁定を保ったまま差分だけを反映する。

Merge a partial set of adjudications into the review site without clobbering existing ones.

サイトのPOSTはitems全置換なので、素で叩くと既存裁定が消える。このスクリプトはGETしてから
マージし、readbackまで行う。completedは既存値を引き継ぎ、--complete / --reopen で明示した時だけ
変える（trueにするとpollerが無人applyを起動するため、書き戻しの副作用で変わってはいけない）。

The site's POST replaces the whole items array, so a naive POST drops existing rulings.
This script GETs first, merges, POSTs, and reads back. completed is carried forward and only
changes with --complete / --reopen, because flipping it hands the PR to (or pulls it back from)
the unattended apply poller.
"""
from __future__ import annotations

import argparse
import json
import sys
import urllib.error
import urllib.request

DEFAULT_BASE = "http://127.0.0.1:8931"


def api(base: str, path: str, payload: dict | None = None) -> dict:
    url = f"{base}{path}"
    data = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(
        url, data=data, method="POST" if data else "GET",
        headers={"Content-Type": "application/json"} if data else {},
    )
    # 外部境界: HTTPエラー本文にサーバ側の検証理由が入るので握って表示する
    # External boundary: surface the server-side validation reason carried in the error body
    try:
        with urllib.request.urlopen(req, timeout=15) as res:
            return json.loads(res.read().decode())
    except urllib.error.HTTPError as exc:
        body = exc.read().decode(errors="replace")
        print(f"HTTP {exc.code} {url}\n{body}", file=sys.stderr)
        raise SystemExit(2)
    except urllib.error.URLError as exc:
        print(f"裁定サイトへ到達できない ({url}): {exc.reason}", file=sys.stderr)
        raise SystemExit(2)


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--pr", required=True, help="PR番号")
    p.add_argument("--decisions", required=True,
                   help='反映する裁定のJSONファイル。形式: [{"id":"F01","decision":"reject","comment":"..."}, ...]')
    p.add_argument("--base", default=DEFAULT_BASE, help=f"サイトのベースURL (default: {DEFAULT_BASE})")
    p.add_argument("--complete", action="store_true",
                   help="completed:true で保存する。全非suppressed指摘の裁定が揃っている時のみ受理され、"
                        "reject以外が1件でもあればpollerが無人applyを起動する")
    p.add_argument("--reopen", action="store_true",
                   help="completedをfalseへ戻す。既に対応中/完了へ遷移したPRを巻き戻すことになるので、"
                        "pollerの状態を確認したうえでのみ使う")
    p.add_argument("--dry-run", action="store_true", help="POSTせずマージ結果を表示する")
    args = p.parse_args()

    with open(args.decisions, encoding="utf-8") as f:
        incoming = json.load(f)
    if not isinstance(incoming, list):
        raise SystemExit("--decisions のJSONは配列であること")

    current = api(args.base, f"/api/pr/{args.pr}")
    findings = current.get("findings") or {}
    adjudications = current.get("adjudications") or {}
    existing = adjudications.get("items") or []

    # completedは既存値を引き継ぐ。落とすとpollerの遷移済みPRを黙って裁定待ちへ巻き戻す
    # Carry completed forward; dropping it silently rewinds a PR the poller already advanced
    completed = bool(adjudications.get("completed"))
    if args.complete:
        completed = True
    if args.reopen:
        completed = False

    merged: dict[str, dict] = {}
    for item in existing + incoming:
        entry = {
            "id": item["id"],
            "decision": item["decision"],
            "comment": item.get("comment", ""),
            "auto_recommended": bool(item.get("auto_recommended", False)),
        }
        merged[entry["id"]] = entry

    all_ids = [f["id"] for f in findings.get("findings", [])]
    suppressed = {f["id"] for f in findings.get("findings", []) if f.get("suppressed")}
    undecided = [i for i in all_ids if i not in suppressed and i not in merged]

    body = {"items": list(merged.values()), "completed": completed}
    print(json.dumps({"merged": len(merged), "undecided": undecided, "completed": body["completed"]},
                     ensure_ascii=False))
    if args.dry_run:
        print(json.dumps(body, ensure_ascii=False, indent=2))
        return

    api(args.base, f"/api/pr/{args.pr}/adjudications", body)
    saved = (api(args.base, f"/api/pr/{args.pr}").get("adjudications") or {})
    print(json.dumps(
        {"readback": [(i["id"], i["decision"]) for i in saved.get("items", [])],
         "completed": saved.get("completed")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
