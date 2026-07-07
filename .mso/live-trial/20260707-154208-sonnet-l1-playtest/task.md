# タスク: unity-playmode-recorded-playtest の実走

以下を実行してください:

Skill({skill: "unity-playmode-recorded-playtest", args: "tools/playtest/scenarios/belt-line-via-ui.cs を実行して結果を報告して"})

処理が終了 (成功 / 失敗 / 中断いずれでも) したら:

1. /Users/katsumi/moorestech-worktrees/playtest/.mso/live-trial/20260707-154208-sonnet-l1-playtest/out/status.json に完了マーカーを 1 ファイルだけ Write する。内容:
   {"status": "PASS|FAIL|ERROR", "result_json_path": "<result.jsonの絶対パス or null>", "asserts_passed": <数値 or null>, "asserts_total": <数値 or null>, "attempts": <シナリオ実行を試みた回数>, "summary": "<結果の1-2文要約>"}
2. 「DONE: <status>」と 1 行だけ報告する。

制約:
- 途中で人間に質問せず最後まで自走すること。
- skill の手順に忠実に従い、人手の追加判断・省略をしないこと。
- out/ には status.json 以外を書かないこと (中間生成物は skill 側の出力先 (WORK_DIR 外) へ)。
- git commit / git push は行わないこと（評価ハーネス側で管理する）。
