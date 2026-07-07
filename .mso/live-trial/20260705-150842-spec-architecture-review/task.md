# タスク: spec-architecture-review の実走

以下を実行してください:

Skill({skill: "spec-architecture-review", args: "/private/tmp/claude-501/-Users-katsumi-moorestech-worktrees-tree1/831c510f-8217-447c-8dfe-b4bb597cd109/scratchpad/skill-iter/fixtures/fluid-tank-upgrade-plan.md の設計書をレビューする"})

skill の手順どおりに設計書の配置決定を全件抽出・検査し、findings（各項目に target / verdict(violation|ok) / violation_type / reason / fix / precedent を含む JSON）を
/private/tmp/claude-501/-Users-katsumi-moorestech-worktrees-tree1/831c510f-8217-447c-8dfe-b4bb597cd109/scratchpad/skill-iter/live-trial/findings-A.json
に Write すること。

処理が終了 (成功 / 失敗 / 中断いずれでも) したら:

1. /Users/katsumi/moorestech-worktrees/tree1/.mso/live-trial/20260705-150842-spec-architecture-review/out/status.json に完了マーカーを 1 ファイルだけ Write する。内容:
   {"status": "PASS|FAIL|ERROR", "violations": <violation判定の件数 or null>, "ok": <ok判定の件数 or null>}
   （PASS = レビューを完走し findings-A.json を書き出せた場合）
2. 「DONE: <status>」と 1 行だけ報告する。

制約:
- 途中で人間に質問せず最後まで自走すること。
- skill の手順に忠実に従い、人手の追加判断・省略をしないこと。
- out/ には status.json 以外を書かないこと (中間生成物は skill 側の出力先 (WORK_DIR 外) へ)。
- リポジトリのファイルを編集しないこと（レビューは読み取りのみ、Write は上記2ファイルのみ）。
