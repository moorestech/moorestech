# リプレイ評価: 期待検出リスト（実レビュー22指摘）

各fixtureに対しレンズ/決定論チェックを走らせた際、検出されるべき指摘。
「検出器」列が本ハーネスの担当。リプレイで検出できなければ配管の退行（レンズ・selector・スクリプトを疑う）。
注意: レンズはこの22件から作られているため、全件検出は「配管が正しい」証明であって汎化の証明ではない。
汎化はレンズ作成に使っていないPRのブラインドリプレイ（README参照）で別途確認する。

## pr978-r1（review 4642535092）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 1 | BlockMasterのJSON挿入処理は不要 | BlockMaster.cs | master-data-defense（プリフィル） |
| 2 | アイドル判定のドメイン漏れ | GearEnergyTransformerComponent.cs:62 | domain-boundary（観点1） |
| 3 | _isActiveはドメイン境界越え | GearEnergyTransformerComponent.cs:43 | domain-boundary（観点1） |
| 4 | 何がアクティブかを知るべきでない | GearEnergyTransformerComponent.cs:32 | domain-boundary（観点1） |
| 5 | 発電機系のドメイン越境revert | SimpleGearGenerator/GearToElectric | domain-boundary（観点1） |
| 6 | テンプレートのラムダ判定 | VanillaGearMapObjectMinerTemplate.cs:49 | domain-boundary（観点1・Template） |
| 7 | UpdateTicksForSpeedChange適切か | VanillaBeltConveyorInventoryItem.cs:69 | （設計質問。検出対象外＝正当な人間判断） |

## pr978-r2（review 4642994960）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 8 | ??フォールバック全体不要 | BlockMaster.cs:24 | 決定論 master_default_fallback |
| 9 | optionalでなく必須 | VanillaSchema/blocks.yml:290 | 決定論 candidates.schema_optional_true → master-data-defense裁定 |
| 10 | 毎フレーム実行は非効率 | GearBeltConveyorComponent.cs:39 | domain-boundary（観点2） |
| 11 | 毎フレーム判定不要 | VanillaGearMapObjectMinerProcessorComponent.cs:142 | domain-boundary（観点2） |
| 12 | DefaultIdlePowerRate参照排除 | VanillaElectricPumpTemplate.cs:35 | 決定論 master_default_fallback |
| 13 | 不要な一時変数 | VanillaElectricPumpTemplate.cs | master-data-defense（一時変数） |

## pr988（review 4644402652）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 14 | イベントパケット新設+購読へ | ItemStackLevelEventHandler.cs | server-state-sync（3点セット/Applier） |
| 15 | Applier 2種は不要になる | Challenge/ResearchItemStackLevelApplier.cs | server-state-sync（Applier禁止） |
| 16 | 初期データで直接取得 | VanillaApiWithResponse.cs | server-state-sync（初期データ） |
| 17 | Lookup static公開/変更DI分離 | ItemStackLevelDataStore.cs | datastore-access-separation |

## pr987（comment r3537879954）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 18 | 共用体structの抽象化 | BuildMenuEntry.cs:20 | type-driven-structure（観点1） |

## pr996（review 4661573483）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 19 | Contextへのバカバカ入れ | IPlaceSystem.cs | type-driven-structure（観点2） |
| 20 | Selectorのプロパティ羅列 | PlaceSystemSelector.cs | type-driven-structure（観点2） |
| 21 | DTOをプロトコル階層に置くな | BlueprintPacketDto.cs | 決定論 packet_response_root / type-driven-structure（観点4） |

## pr997（review 4645972495）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 22 | 3interfaceを束ねる複合interface | ElectricWireConnectorComponent.cs | type-driven-structure（観点3） |
| 23 | ディレクトリ整理（依存可視化） | Util/ElectricWire/ | 決定論 dir_file_limit / type-driven-structure（観点4） |

## pr1000（comment r3556923781）

| # | 指摘 | 対象 | 検出器 |
|---|---|---|---|
| 24 | _gearServiceへ委譲し重複解消 | GearChainPoleComponent.cs:239 | domain-boundary（観点3）/ precedent-alignment |
