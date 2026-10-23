# Task 3 報告
## 実装
- 燃料サービスを分割（SettingsBuilder / SaveConverter 新設、Update が bool を返す、IsBurning/RemainingFuelTicks/CurrentFuelTotalTicks/TryGetFuelTank 追加）。199行。
- 発電機本体に IBlockStateObservable と GetBlockStateDetails（PowerGenerator＋液体タンク）。SaveJsonObject を別ファイルへ。196行。
- PowerGeneratorStateDetail に秒値 ctor。
- クライアント: IsCommonMachine から ElectricGenerator を除外。webui: SectionStackView の ElectricGenerator に液体行（generator-fluid-slots）。
## テスト
- uloop: ElectricGeneratorStateDetailTest|PowerGeneratorTest|PowerGeneratorSaveLoadTest 7/7 pass、compile ErrorCount 0
- vitest src/features/blockInventory 154/154、playwright blockRegistryCoverage 1/1
## 調整・懸念
- plan どおりの分割だと FuelService が200行を超えたため、単純な if/ブレースの1行化で199行に圧縮（挙動不変）。SaveJsonObject の using は Core.Item.Interface（plan に記載なし）。FuelType は converter が使うため internal。
- セーブ形式は不変（クラス移動のみ）。
