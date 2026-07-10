---
name: worktree-needs-own-unity
description: Each git worktree needs its OWN running Unity Editor for uloop compile/test to target it
metadata: 
  node_type: memory
  type: project
  originSessionId: 4af4fa5c-5b45-4d86-9cb7-4f7ba0604186
---

uloop compile/run-tests は **project-path の running Unity Editor インスタンス**に対して動く。git worktree を使う場合、その worktree の `moorestech_client` 用に Unity を**個別に起動**しないと `UNITY_NOT_RUNNING` になる（他 worktree の Unity は使えない）。

各 worktree の `moorestech_client/Library` は別物（gitignore・ディスク上 per-worktree）。新規 asmdef/.cs は fresh launch の初回 import で認識される。

**Why:** worktree を間違えて他 Unity に対して compile すると、自分の変更が反映されない・別ブランチを壊す事故になる。
**How to apply:** 作業 worktree で必ず最初に `ps aux | grep Unity` で projectPath を確認し、無ければ `uloop launch ./moorestech_client`。

関連: [[worktree-master-symlink]]
