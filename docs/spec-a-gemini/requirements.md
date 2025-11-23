# Requirements Document

## Project Description (Input)
歯車システムにおいて、特定の歯車ネットワークを流れるRPM、トルクが、特定の閾値を超えた時、歯車やシャフトといったGearEnergyTransformブロックが破壊される - blocks.yamlにそのパラメーターのスキーマを追加する - worldDatastore.Removeによってブロックが破壊される。破壊のタイプを、BrokenとManualRemoveで指定できるようにする。これはEnumで定義する。 - Game.Blockアセンブリからブロックの破壊ができるように、IBlockRemoverを定義し、必要に応じてDIコンテナで注入できるようにする。 - VanillaIBlockTemplatesのコンストラクタにIBlockRemoverを定義し、BlockTemplateを経由してGearEnergyTransfomerにこのインスタンスを渡す。 - GearEnegerTransfomerの内部で、ネットワーク全体のトルク、RPMを監視し、一定値を超えたら破壊する。なお、破壊ロジックは以下の通り RPM、トルクのいずれかが指定値を上回っている場合、一定時間、一定確率でブロックを破壊する。 この時間、確率はスキーマで指定できる。 確率は、RPM、トルクが上昇するごとにその倍率によって確率も倍率がかかる。つまり、RPMが2倍になれば、一定時間ごとの破壊される確率も2倍になる。 RPM、トルクが両方とも許容値を超過している場合、その倍率の掛け算で確率が決まる。RPMが2倍、トルクが3倍超過している場合、確率は6倍になる。

## Requirements

### Requirement 1: ブロック破壊基盤の実装
**Objective:** ブロックをプログラムから削除するための抽象化レイヤーを提供し、破壊理由（プレイヤーによる撤去か、過負荷による破損か）を区別可能にする。

#### Acceptance Criteria
1. `Game.Block` アセンブリ内に `IBlockRemover` インターフェースが定義されていること。
2. 破壊理由を表す Enum (`BlockRemoveReason` または指定の名称) が定義され、`Broken` (破損) と `ManualRemove` (手動撤去) が含まれていること。
3. `IBlockRemover` は `worldDatastore.Remove` (または同等の既存メソッド) を呼び出し、指定された理由でブロックを削除できること。
4. `IBlockRemover` の実装クラスはDIコンテナを通じて注入可能であること。

### Requirement 2: マスターデータスキーマの拡張
**Objective:** 各歯車・シャフトブロックごとの耐久限界値および破壊確率計算用のパラメータをYAMLスキーマで定義可能にする。

#### Acceptance Criteria
1. `blocks.yaml` (または関連するブロック定義ファイル) に以下のパラメータが追加されていること：
    - `overloadMaxRpm`: 許容最大RPM
    - `overloadMaxTorque`: 許容最大トルク
    - `destructionCheckInterval`: 破壊判定を行う時間間隔 (秒またはフレーム)
    - `baseDestructionProbability`: 基準となる破壊確率
2. SourceGeneratorにより、上記パラメータがC#のモデルクラス (`BlockParam` 等) に反映され、コードから参照可能であること。

### Requirement 3: 歯車過負荷監視と破壊ロジックの実装
**Objective:** ネットワーク内のエネルギー状態を監視し、閾値を超えた場合に動的に確率計算を行い、ブロックを破壊する。

#### Acceptance Criteria
1. `GearEnergyTransformer` (または対応する歯車コンポーネント) が、自身のネットワークの現在のRPMとトルクを取得できること。
2. スキーマで定義された `destructionCheckInterval` ごとに以下の判定ロジックが実行されること：
    - **RPMのみ超過:** `倍率 = 現在RPM / 許容RPM`
    - **トルクのみ超過:** `倍率 = 現在トルク / 許容トルク`
    - **両方超過:** `倍率 = (現在RPM / 許容RPM) * (現在トルク / 許容トルク)`
    - **破壊確率:** `最終確率 = 基準確率 * 倍率`
3. 計算された確率に基づいて乱数判定を行い、当選した場合に `IBlockRemover` を使用して自身を `Broken` タイプで削除すること。
4. 許容値以内の場合は破壊判定が行われない（または確率0となる）こと。

### Requirement 4: DIとテンプレート連携
**Objective:** システム全体を通して `IBlockRemover` が適切に各ブロックインスタンスに渡されるフローを構築する。

#### Acceptance Criteria
1. `VanillaIBlockTemplates` のコンストラクタが `IBlockRemover` を受け取るように変更されていること。
2. `BlockTemplate` 生成処理において、`GearEnergyTransformer` (または対象ブロック) のインスタンス生成時に `IBlockRemover` が渡されていること。
3. `GearEnergyTransformer` が保持する `IBlockRemover` を使用して自身の削除をリクエストできること。
