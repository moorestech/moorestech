# ローカライズ基盤の最終ブランチレビュー範囲

決定: 最終 moores-code-review の base を `80935cb75`（PR #1111 head＝独立レビュー時点）とし、是正19タスク＋masterマージ分だけを6系統フルで回す（AskUserQuestion 2026-08-03）
棄却案: base `95128f904`（origin/masterとのmerge-base）でブランチ全体482ファイルを6系統フル実行する案（分割深掘り17チャンク×3を含み約86エージェント）
理由: Plan 1/2 の範囲は各々の最終 moores-code-review で clean を取得済み（`.superpowers/sdd/progress.md` に記録）。未レビューの delta に絞る方が同じトークンでより深く見られる
リンク: docs/superpowers/plans/2026-08-02-localization-review-remediation.md Task 19
