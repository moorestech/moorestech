# gearConsumptionを持つBlockParamはスキーマのIGearConsumptionParamで束ねる

- 日付: 2026-09-06 / 出所: AskUserQuestion（PR #1323 レビュー D3）
- 決定: `VanillaSchema/blocks.yml` に `IGearConsumptionParam` を追加し gearConsumption を持つ9定義へ付与。client `GetGearConsumption` と server `BlockMasterUtil.ExtractGearConsumption` の具体型 switch を interface 判定1行へ畳む
- 棄却案: client 側だけ server 正本の9型に追記（二重管理を残す）
- 理由: 次の歯車ブロック追加で片側だけ漏れて BaseRpm/BaseTorque が0表示になる再発を根絶する
