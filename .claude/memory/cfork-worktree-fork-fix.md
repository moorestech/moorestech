---
name: cfork-worktree-fork-fix
description: cfork(~/.local/bin)はcd+セッションID resumeでfork。cmux新splitはcwd非継承、stale surfaceはworkspaceフォールバック
metadata: 
  node_type: memory
  type: project
  originSessionId: ffc1032f-3d2e-4b65-b1ee-10e1060b6f1e
---

`~/.local/bin/cfork`（cmux内でセッションをfork起動するzshスクリプト、2026-07-05修正）の要点:

- cmuxの`new-split`で作ったペインは呼び出し元のcwdを**継承しない**（workspaceルートで開く）。gtr-ccd起動のworktreeセッションをforkするには`cd ${(qq)PWD} && `を前置する必要がある
- **`cmux send`でインタラクティブzshにタイプ入力させる文字列は`${(q)}`でなく`${(qq)}`（シングルクォート形式）**: `(q)`は`!`をエスケープせず、対話zshのbang-history展開で`event not found`になりコマンド全体が黙って死ぬ
- `claude --continue`は「cwdの最新セッション」を拾うため誤爆する。Claudeセッション内のBash環境には`CLAUDE_CODE_SESSION_ID`があるので`--resume $CLAUDE_CODE_SESSION_ID --fork-session`が正確
- 長時間セッションでは`CMUX_SURFACE_ID`が古くなり`cmux new-split`が"Surface not found"になる。`--workspace $CMUX_WORKSPACE_ID`（UUIDは有効なまま）でリトライすると通る
- codex側は`CODEX_THREAD_ID`＋`codex --yolo fork <id>`でID指定fork（alias依存回避で直呼び）。exec作成セッションもforkできる。rolloutは`~/.codex/sessions/YYYY/MM/DD/`
- `claude --resume <id>`は同一gitリポジトリならworktree⇔main間でも解決できるが、`--continue`と非gitの`--resume`はcwdリテラル一致のみ。失効IDはexit 1（ピッカーには落ちない）
- バックアップ: `cfork.bak-20260705-original`（修正前）/ `cfork.bak-20260705-fixed`（フォールバック追加前）
- 検証時は[[server-tests-immutable-package]]同様moorestech配下を避ける: 稼働中セッションのproject dirを汚すため、scratchpadにテストrepoを作ってそこから実行
