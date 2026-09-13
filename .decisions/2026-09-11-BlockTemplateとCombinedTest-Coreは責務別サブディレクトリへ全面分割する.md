# BlockTemplate と CombinedTest/Core は責務別サブディレクトリへ全面分割する

日付: 2026-09-11
出所: ユーザー裁定 2026-09-11 AskUserQuestion（moores-code-review D1 / ボイドパイプ最終レビュー）→ 選択「責務別に全面分割（推奨）」

決定: `Game.Block/Factory/BlockTemplate/`（32本）と `Tests/CombinedTest/Core/`（42本）を Fluid/Gear/Train/Machine/Transport 等の責務別サブディレクトリへ全面分割し、各10本以下にする（PR feature/void-pipe 内で実施）。
棄却案: 本PRが触った流体系だけ先に切り出す（親ディレクトリが27本・35本前後で超過のまま残る）。
理由: 平置き慣習のまま新ブロック種別を足すたびに10ファイル規約違反が積み上がり、後の一括分割の移動量とコンフリクト面が増え続けるため。
リンク: [[2026-09-11-ボイドパイプは終端の吸い込み口ブロックにする]]
