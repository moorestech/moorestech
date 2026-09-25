#!/usr/bin/env python3
"""取り込みの箱メタデータに報告者名を結合する。
Combine the box metadata with its reporter persona.
"""
import json
import os
import sys


kind, steam_id, box_id, ready_at, ingested_at, persona_path, output = sys.argv[1:8]

# 一時ファイルを取り込んだら、公開する箱には ingest.json だけを残す
# Consume the temporary persona file so the published box only retains ingest.json
with open(persona_path, encoding="utf-8") as source:
    persona = json.load(source)
os.remove(persona_path)
metadata = {"kind": kind, "steamId": steam_id, "id": box_id, "readyAt": ready_at, "ingestedAt": ingested_at}
metadata.update(persona)
with open(output, "w", encoding="utf-8") as target:
    json.dump(metadata, target, ensure_ascii=False, separators=(",", ":"))
