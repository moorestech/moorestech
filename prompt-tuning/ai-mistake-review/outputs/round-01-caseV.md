# caseV レビュー結果（base→ai）

Critical | PumpFluidVeinTest.cs:90-93 (PlacePoweredPump) | T/L | `new EnergySegment()` をローカルで生成し AddEnergyConsumer/AddGenerator するだけで、WorldEnergySegmentDatastore に登録もtick更新もしていない。電力が実際にポンプへ分配されず powerRate=0 のまま。→ 既存 ElectricPumpTest の RunForSeconds と同様に、tickループ内で毎フレーム `pumpComponent.SupplyEnergy(power)` を呼ぶ方式に書き換える（segment手組みは捨てる）。
Critical | PumpFluidVeinTest.cs:84-87 (PlacePoweredPump) | T | 電力供給がtickループの前に1回行われる想定だが、ループは GenerateFluid を呼ぶ別メソッド内（636/653/670行）にあり、PlacePoweredPump が返った時点で segment は二度と更新されない。PumpOnMatchingFluidVein_GeneratesFluid の `Assert.Greater(amount,0)` が通らない（=正のケースが偽陰性）。→ 各テストで毎tick SupplyEnergy する形に統一する。
Warning | FluidMapVeinDatastore.cs:13-15 | V | `if (mapInfoJson.FluidVeins == null) return;` ＋「後方互換のためnull許容」コメント。本プロジェクトは後方互換不要。兄弟の ItemMapVeinDatastore は null チェック無しで `mapInfoJson.ItemMapVeins` を直接 foreach している。→ null チェックとコメントを削除し ItemMapVeinDatastore と同じ書き方に揃える。
Warning | MapInfoJson.cs:372 / commit message | V | FluidVeins フィールドの「null許容で後方互換」も同じV違反。マスタ/mapは生成物なので後方互換考慮不要。→ コメントの「後方互換」根拠を消す（フィールド自体は必要）。
Warning | MoorestechServerDIContainerGenerator.cs:519-540, 553-584 | D/V | 機能追加（545行の FluidMapVeinDatastore 登録）と無関係に、Gear/Rail/Train 等の既存登録行が丸ごと差し替え差分になっている（インデント・改行コードのみのノイズ変更）。レビュー困難＋意図せぬ行末/空白混入のリスク。→ 機能に関係する1行追加のみ残し、他行の whitespace 差分は revert する。
Warning | MapExportAndSetting.cs:152,160,168,172 | D | GetFluidVeinInfo 追加と同時に、隣の GetMapVeinInfo 内の空行から末尾空白を除去するだけの無関係 whitespace 差分が混入。→ 機能に無関係な行は元に戻し diff を最小化する。
Nit | PumpFluidGenerationUtility.cs:39 | S/M | ループ内 `MasterHolder.FluidMaster.GetFluidId(gen.FluidGuid)` は OrNull でなく直取得。gen.FluidGuid はマスタ由来で通常存在するが、Datastore側(19行)は GetFluidIdOrNull で存在検証している。整合のため、ここも存在しないGuidで例外を投げない方針か確認（マスタ保証ありなら現状可）。
Nit | PumpFluidGenerationUtility.cs:35-42 | P | veins から veinFluidIds(HashSet) を毎tick・毎ポンプで再構築している。生成液体種は少数だが、GetOverVeins も毎tick List を new している。頻出tick処理なので、Vein一致判定は早期に veins.Count==0 で抜ける現状ガード(43行)で実害は小だが、ループ毎の HashSet 確保は不要。→ 必要なら一致確認をループ内 Any に変えアロケーションを避ける。
Nit | FluidMapVeinDatastore.cs:31-40 / ItemMapVeinDatastore.cs:31-40 | D | GetOverVeins の範囲判定ロジックが Item版とほぼ完全コピペ。→ 共通の範囲判定ヘルパー（Vector3Int の包含判定）へ抽出余地あり。

## 最有力の1件
Critical | PumpFluidVeinTest.cs:90-93 | T/L — テストの電力供給が WorldEnergySegmentDatastore 未登録かつ非tick更新で、ポンプに電力が届かない。正ケース PumpOnMatchingFluidVein_GeneratesFluid が偽陰性になり、負ケース2つは「電力が無いから生成されない」という誤った理由で通る（Vein判定を全く検証できていない）。既存 ElectricPumpTest.RunForSeconds と同様、毎tick SupplyEnergy する方式へ修正するのが人間の直し。
