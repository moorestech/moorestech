# タスク: brainstorming の実走

以下を実行してください:

Skill({skill: "brainstorming", args: "チェスト（Chest）の中身をワンクリックで種類ごとに自動整列（ソート）できる機能を作りたい。設計から一緒に考えてほしい。"})

処理が終了 (成功 / 失敗 / 中断いずれでも) したら:

1. /Users/katsumi/moorestech-worktrees/tree1/.mso/live-trial/20260706-181434-brainstorming-accept/out/status.json に完了マーカーを 1 ファイルだけ Write する。内容:
   {"status": "PASS|FAIL|ERROR", "design_doc": "<作成した設計文書の絶対パス or null>"}
2. 「DONE: <status>」と 1 行だけ報告する。

制約:
- ユーザーへの確認・質問は skill の手順どおり AskUserQuestion 等で行ってよい（ユーザーの応答が返ってくる）。それ以外の場面では人間の介入を待たず自走すること。
- skill の手順に忠実に従い、人手の追加判断・省略をしないこと。ユーザーが対話中に指示した終了・スコープ変更には従ってよい。
- out/ には status.json 以外を書かないこと (中間生成物は skill 側の出力先 (WORK_DIR 外) へ)。
