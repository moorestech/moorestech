# タスク: writing-plans の実走

以下を実行してください:

Skill({skill: "writing-plans", args: "/Users/katsumi/moorestech/.mso/live-trial/20260707-171539-writing-plans/input/old-spec.md のFPS建設モード設計書から実装計画を作成する。計画ファイルはユーザー設定として /Users/katsumi/moorestech/.mso/live-trial/20260707-171539-writing-plans/deliverables/plan.md に保存する（docs/superpowers/plans/ より優先）。gitへのコミットおよびリポジトリ内ファイルの変更は行わない（コードベースの読み取り・調査は可）。計画完成後の実行ハンドオフ（Subagent-Driven / Inline の選択提示）は行わず、計画の保存で終了する"})

処理が終了 (成功 / 失敗 / 中断いずれでも) したら:

1. /Users/katsumi/moorestech/.mso/live-trial/20260707-171539-writing-plans/out/status.json に完了マーカーを 1 ファイルだけ Write する。内容:
   {"status": "PASS|FAIL|ERROR のいずれか（計画をplan.mdへ保存完了=PASS）", "plan_path": "保存した計画ファイルの絶対パス"}
2. 「DONE: <status>」と 1 行だけ報告する。

制約:
- 途中で人間に質問せず最後まで自走すること。
- skill の手順に忠実に従い、人手の追加判断・省略をしないこと。
- out/ には status.json 以外を書かないこと (中間生成物は skill 側の出力先 (WORK_DIR 外) へ)。
