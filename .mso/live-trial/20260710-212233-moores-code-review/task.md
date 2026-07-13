# タスク: moores-code-review の実走

以下を実行してください:

Skill({skill: "moores-code-review", args: "レビュー対象パッチは /Users/katsumi/moorestech/.mso/live-trial/20260710-212233-moores-code-review/input/pr978-r2.diff（これをPATCH_PATHとして使い、Step 1の作業範囲特定はスキップしてよい）。4カテゴリcontext: 目指す=アイドル時の要求エネルギー低減機能のレビュー / 目指さない=機能追加 / 許容するトレードオフ=なし / 尊重すべき制約=AGENTS.mdとプロジェクト設計規約。注意: このパッチは過去時点のdiffであり、現在のコードでは既に修正済みの箇所が多い。裏取りで現在のコードと一致しない指摘は integration-rules に従い破棄してよいが、パッチ自体に対する指摘（レンズ・決定論チェックの検出結果）は報告に含めること。修正の自動適用はコードが現存一致する場合のみ行い、uloop compileが利用不可なら記録して続行すること。"})

処理が終了 (成功 / 失敗 / 中断いずれでも) したら:

1. /Users/katsumi/moorestech/.mso/live-trial/20260710-212233-moores-code-review/out/status.json に完了マーカーを 1 ファイルだけ Write する。内容:
   {"status": "PASS|FAIL|ERROR", "deterministic_confirmed": <件数 or null>, "lenses_fired": [<起動したレンズ名>], "criticals_reported": <統合報告に載せたCritical件数 or null>, "discarded_as_stale": <裏取りで破棄した件数 or null>}
2. 「DONE: <status>」と 1 行だけ報告する。

制約:
- 途中で人間に質問せず最後まで自走すること。設計判断の保留項目が出た場合はAskUserQuestionを呼ばず、統合報告に「保留項目」として列挙して終了すること（今回はレビュー実走の検証が目的のため）。
- skill の手順に忠実に従い、人手の追加判断・省略をしないこと。
- out/ には status.json 以外を書かないこと (中間生成物は skill 側の出力先 (WORK_DIR 外、/tmp配下) へ)。
