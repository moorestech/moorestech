# applyのdirty判定は全面エージェント判断にする

決定: pr-adjudicated-apply の Step 3 で working tree が dirty だったとき、エージェントが毎回すべての dirty ファイルの diff を読んで「意味のない自動変更 / 意味のある変更」を判定する。既知ファイルのallowlistは持たない

棄却案: `_CompileRequester.cs` と `.moorestech-external-revisions.json` を名指しallowlistに載せ、外だけエージェント判断するハイブリッド／allowlistのみ・外は従来どおり中止

理由: 自動変更系のファイルは今後も増える。名指しリストは必ず陳腐化し、載っていないだけで無人パイプラインが止まる。判定はdiffを読めば足りる

リンク: [[2026-08-14-pushまで全自動化し人間はマージボタンのみ]]
