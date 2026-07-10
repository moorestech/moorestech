# タスク: unity-playmode-recorded-playtest の実走

以下を実行してください:

Skill({skill: "unity-playmode-recorded-playtest", args: "新規プレイテストシナリオを作成して実行・検証して: ブロック設置はすべてUI経路（ビルドメニューからのキーマウ操作設置）で行い、ベルトコンベアで鉄鉱石の粉を石窯へ搬入し、石窯の精錬出力を別のベルトコンベア経由で木のコンベアチェストへ収納するラインを組む。鉄鉱石の粉5個を投入し、精錬でできた鉄インゴット5個が全数チェストへ届くこと（紛失・重複なし）を検証する。設置座標は x, z とも -20〜-5 の帯（足場上の未使用エリア）を使うこと。シナリオファイルは tools/playtest/scenarios/ に新規作成すること"})

処理が終了 (成功 / 失敗 / 中断いずれでも) したら:

1. /Users/katsumi/moorestech-worktrees/playtest/.mso/live-trial/20260707-163557-sonnet-l2-playtest/out/status.json に完了マーカーを 1 ファイルだけ Write する。内容:
   {"status": "PASS|FAIL|ERROR", "scenario_file": "<作成したシナリオの絶対パス or null>", "result_json_path": "<result.jsonの絶対パス or null>", "asserts_passed": <数値 or null>, "asserts_total": <数値 or null>, "attempts": <シナリオ実行を試みた回数>, "found_product_bug": "<発見したプロダクトバグの説明 or null>", "summary": "<結果の1-2文要約>"}
2. 「DONE: <status>」と 1 行だけ報告する。

制約:
- 途中で人間に質問せず最後まで自走すること。
- skill の手順に忠実に従い、人手の追加判断・省略をしないこと。
- out/ には status.json 以外を書かないこと (中間生成物は skill 側の出力先 (WORK_DIR 外) へ)。
- git commit / git push は行わないこと（評価ハーネス側で管理する）。
