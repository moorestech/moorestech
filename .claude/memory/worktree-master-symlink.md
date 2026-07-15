---
name: worktree-master-symlink
description: Worktree PlayMode fails to load master JSON until ../moorestech_master symlink exists
metadata: 
  node_type: memory
  type: project
  originSessionId: 57b01163-74e6-480e-9df5-805fbca809db
---

git worktree 配下（例 `/Users/katsumi/moorestech-worktrees/tree3`）で Unity を PlayMode 起動すると、マスタ/ローカライズの読込が `DirectoryNotFoundException: .../moorestech-worktrees/moorestech_master/server_v8/...` で失敗する。ゲームは `../moorestech_master` を相対解決するが、worktree の親は `moorestech-worktrees/` なので実体（`/Users/katsumi/moorestech_master`）に届かない。MasterHolder.BlockMaster が null のまま World シーンに遷移せず、placement 等の検証が一切できなくなる。

**How to apply:** worktree で PlayMode を回す前に一度だけ `ln -s /Users/katsumi/moorestech_master /Users/katsumi/moorestech-worktrees/moorestech_master` を作る（全 worktree 共通で1本でよい）。Library が無い worktree には本体から `cp -Rc /Users/katsumi/moorestech/moorestech_client/Library ./moorestech_client/Library`（APFS clonefile で数十秒）。起動は [[worktree-needs-own-unity]] の通り worktree ごとに uloop launch。`UnityMcpSettings.json` が `.bak` のみになったら [[uloop-not-installed-bak-restore]] で復元。
