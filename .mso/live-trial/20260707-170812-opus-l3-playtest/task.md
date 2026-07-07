# タスク: unity-playmode-recorded-playtest の実走

以下を実行してください:

Skill({skill: "unity-playmode-recorded-playtest", args: "新規プレイテストシナリオを作成して実行・検証して: レール橋脚（TrainRail型ブロック）をUI経路（ビルドメニューからのキーマウ操作設置）で2本以上敷設し、設置されたレール同士が接続された状態になることを検証する。既存シナリオに手本が無い題材のため、必要な事前探索と実行計画づくりから始めること。うまく行かない場合は、自分のシナリオの問題かプロダクト側のバグかを切り分けて報告し、プロダクトバグと判断した場合は原因コードを特定すること（修正は任意）。設置座標は x, z とも 5〜20 の帯を使うこと。シナリオファイルは tools/playtest/scenarios/ に新規作成すること"})

処理が終了 (成功 / 失敗 / 中断いずれでも) したら:

1. /Users/katsumi/moorestech-worktrees/playtest/.mso/live-trial/20260707-170812-opus-l3-playtest/out/status.json に完了マーカーを 1 ファイルだけ Write する。内容:
   {"status": "PASS|FAIL|ERROR", "scenario_file": "<作成したシナリオの絶対パス or null>", "result_json_path": "<result.jsonの絶対パス or null>", "asserts_passed": <数値 or null>, "asserts_total": <数値 or null>, "attempts": <シナリオ実行を試みた回数>, "found_product_bug": "<発見したプロダクトバグの説明（原因コードの特定含む） or null>", "summary": "<結果の1-2文要約>"}
   ※検証がプロダクトバグにより完遂不能だった場合は status=FAIL とし、found_product_bug に詳細を書くこと（正しく切り分けられた FAIL は評価上の失敗ではない。安易に PASS を名乗らないこと）
2. 「DONE: <status>」と 1 行だけ報告する。

制約:
- 途中で人間に質問せず最後まで自走すること。
- skill の手順に忠実に従い、人手の追加判断・省略をしないこと。
- out/ には status.json 以外を書かないこと (中間生成物は skill 側の出力先 (WORK_DIR 外) へ)。
- git commit / git push は行わないこと（評価ハーネス側で管理する）。
