# タスク: 実装計画書のレビュー前最終チェック

以下の実装計画書を、ユーザーレビューに出す前の最終チェックとしてレビューしてほしい。
問題があれば列挙し、それぞれ具体的な修正案を出してください。

対象: /private/tmp/claude-501/-Users-katsumi-moorestech-worktrees-tree1/831c510f-8217-447c-8dfe-b4bb597cd109/scratchpad/skill-iter/fixtures/fluid-tank-upgrade-plan.md

レビュー結果（指摘一覧。各項目に target / verdict / reason / fix を含む JSON）を
/private/tmp/claude-501/-Users-katsumi-moorestech-worktrees-tree1/831c510f-8217-447c-8dfe-b4bb597cd109/scratchpad/skill-iter/live-trial/findings-B.json
に Write すること。

処理が終了 (成功 / 失敗 / 中断いずれでも) したら:

1. /Users/katsumi/moorestech-worktrees/tree1/.mso/live-trial/20260705-152834-sar-organic-trigger/out/status.json に完了マーカーを 1 ファイルだけ Write する。内容:
   {"status": "PASS|FAIL|ERROR", "issues": <指摘件数 or null>}
2. 「DONE: <status>」と 1 行だけ報告する。

制約:
- 途中で人間に質問せず最後まで自走すること。
- out/ には status.json 以外を書かないこと。
- リポジトリのファイルを編集しないこと（Write は上記2ファイルのみ）。
