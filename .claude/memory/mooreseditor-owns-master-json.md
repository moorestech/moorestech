---
name: mooreseditor-owns-master-json
description: mooreseditor.app reverts external text edits to moorestech_master JSON (blocks.json etc.) within seconds
metadata: 
  node_type: memory
  type: project
  originSessionId: 27686cc8-631a-48cf-8b8e-342f8e21d05e
---

`/Users/katsumi/moorestech/tools/mac/mooreseditor.app` (MooresEditor GUI) を起動中は、`/Users/katsumi/moorestech_master` 配下のマスタJSON（blocks.json / items.json / research.json 等）を `Write`/`Edit`/`sed` で外部編集しても、数秒以内に mooreseditor が自身のメモリ状態から書き戻して**リバートされる**。`.mooreseditor/nodeGraph.v1.json` が git status に出ていたら mooreseditor 稼働中のサイン。

**Why:** Unity がPrefab/Sceneを所有するのと同じで、mooreseditor がこれらJSONの所有者。外部からの直接編集は整合性・未保存変更と衝突する。

**How to apply:** マスタJSONを変える必要があるときは (1) mooreseditor 上でユーザーに設定してもらう（正規ルート、未保存作業も守られる）か、(2) ユーザーに mooreseditor を閉じてもらってから編集する。プロセス確認は `ps aux | grep -i mooreseditor`。勝手に kill しない（未保存作業が失われる）。関連: [[worktree-master-symlink]]
