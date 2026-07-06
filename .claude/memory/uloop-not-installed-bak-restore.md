---
name: uloop-not-installed-bak-restore
description: uloop CLIが突然「not installed」エラーを返すときはUnityMcpSettings.jsonが.bakのみになっている — コピー復元で直る
metadata: 
  node_type: memory
  type: project
  originSessionId: 1840f43e-e358-4f8d-a10b-087f0696bf1c
---

uloopコマンドが「not installed」エラーを返すことがある（2026-06-12のWeb UI移行作業中に5回以上再発）。

**Why:** `moorestech_client/UserSettings/UnityMcpSettings.json` が何らかのタイミングで `.bak` にリネームされたまま残り、uloop CLIが設定を見つけられなくなる。Unity本体は正常稼働している（ポートは生きている）。

**How to apply:** `cp moorestech_client/UserSettings/UnityMcpSettings.json.bak moorestech_client/UserSettings/UnityMcpSettings.json` で復元して再実行。Unityの再起動は不要。このファイルはgit管理外。
