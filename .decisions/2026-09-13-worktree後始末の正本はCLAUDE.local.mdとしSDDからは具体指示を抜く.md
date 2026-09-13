# worktree後始末の正本は CLAUDE.local.md とし SDD からは具体指示を抜く

2026-09-13 裁定。

## 決定
`subagent-driven-development/references/workspace-isolation.md:63`「worktreeは完了後も削除しない（作業消失防止。cleanupは人間の指示があった時のみ）」を削除する。worktree の後始末ポリシーの正本は CLAUDE.local.md（PRを出したらその場で `moores-wt rm <name>`）。

## 棄却案
- **SDD側が正（作業消失防止のため削除しない）** — 棄却。この環境では放置Editorのメモリ圧迫と孤児worktreeのmasterピン保持が実害として観測されており、CLAUDE.local.md の「PR作成直後が撤収の既定タイミング」裁定を巻き戻すことになる。
- **層で分ける（SDDは「環境ローカル規約に従う」とだけ書く）** — 採らなかったが、グローバル `~/.agents` 側を同一化する際にこの形が必要になる可能性がある（moores-wt は moorestech 環境限定のため）。

## 理由
同一規則が2箇所に複製され、複製同士が矛盾していた典型例（指示ファイル剪定監査 2026-09-04「横断パターン: 同一規則の多重複製と複製間の矛盾」）。正本を1つ決めて他を消す。

## リンク
- CLAUDE.local.md「タスク毎の使い捨てworktree」
- 監査: `moorestech_logs/harness/pr-independent-review/docs/skill-prune-audit.html` 計画・プロセス系
