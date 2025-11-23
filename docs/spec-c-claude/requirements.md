# Requirements Document

## Project Description (Input)
歯車システムにおいて、特定の歯車ネットワークを流れるRPM、トルクが、特定の閾値を超えた時、歯車やシャフトといったGearEnergyTransformブロックが破壊される - blocks.yamlにそのパラメーターのスキーマを追加する - worldDatastore.Removeによってブロックが破壊される。破壊のタイプを、BrokenとManualRemoveで指定できるようにする。これはEnumで定義する。 - Game.Blockアセンブリからブロックの破壊ができるように、IBlockRemoverを定義し、必要に応じてDIコンテナで注入できるようにする。 - VanillaIBlockTemplatesのコンストラクタにIBlockRemoverを定義し、BlockTemplateを経由してGearEnergyTransfomerにこのインスタンスを渡す。 - GearEnegerTransfomerの内部で、ネットワーク全体のトルク、RPMを監視し、一定値を超えたら破壊する。なお、破壊ロジックは以下の通り RPM、トルクのいずれかが指定値を上回っている場合、一定時間、一定確率でブロックを破壊する。 この時間、確率はスキーマで指定できる。 確率は、RPM、トルクが上昇するごとにその倍率によって確率も倍率がかかる。つまり、RPMが2倍になれば、一定時間ごとの破壊される確率も2倍になる。 RPM、トルクが両方とも許容値を超過している場合、その倍率の掛け算で確率が決まる。RPMが2倍、トルクが3倍超過している場合、確率は6倍になる。

## Introduction
本仕様は、歯車システムにおける過負荷破壊メカニズムの実装を定義します。歯車ネットワーク内のRPMおよびトルクが指定された閾値を超えた場合、GearEnergyTransformブロックが確率的に破壊される機能を提供します。これにより、プレイヤーは歯車システムの設計において、適切な負荷管理と耐久性を考慮する必要が生じ、ゲームプレイに深みが加わります。

## Requirements

### Requirement 1: マスターデータスキーマ拡張
**Objective:** ゲーム開発者として、歯車ブロックの破壊パラメータをマスターデータで定義できるようにすることで、ゲームバランスの調整を容易にしたい

#### Acceptance Criteria
1. The マスターデータシステム shall blocks.yamlに以下のパラメータを含むGearEnergyTransformブロック用のスキーマを追加する
   - `maxRpm`: 許容最大RPM（整数型）
   - `maxTorque`: 許容最大トルク（整数型）
   - `overloadCheckIntervalSeconds`: 過負荷チェック間隔（秒、浮動小数点型）
   - `baseBreakageProbability`: 基本破壊確率（0.0～1.0の浮動小数点型）
2. When マスターデータスキーマが更新された場合, the SourceGenerator shall 対応するC#クラスをMooresmaster.Model.BlocksModule名前空間に自動生成する
3. The 自動生成されたクラス shall 全てのパラメータに対して型安全なプロパティアクセスを提供する
4. The マスターデータスキーマ shall デフォルト値の定義をサポートする（省略可能なパラメータのため）

### Requirement 2: ブロック破壊タイプの定義
**Objective:** システム開発者として、ブロックの破壊原因を区別できるようにすることで、ログ記録やイベント処理を適切に実装したい

#### Acceptance Criteria
1. The Game.Block.Interface shall BlockRemovalTypeという名前のenumを定義する
2. The BlockRemovalType enum shall 以下の値を含む
   - `ManualRemove`: プレイヤーによる手動削除
   - `Broken`: システムによる破壊（過負荷等）
3. The BlockRemovalType enum shall Game.Block.Interface名前空間に配置される
4. The BlockRemovalType enum shall 将来的な破壊タイプの追加を考慮した拡張可能な設計とする

### Requirement 3: ブロック破壊インターフェースの定義
**Objective:** システム開発者として、依存性注入を介してブロック破壊機能を利用できるようにすることで、テスタビリティと保守性を向上させたい

#### Acceptance Criteria
1. The Game.Block.Interface shall IBlockRemoverという名前のインターフェースを定義する
2. The IBlockRemover interface shall 以下のメソッドシグネチャを持つ
   - `void RemoveBlock(BlockPositionInfo position, BlockRemovalType removalType)`
3. The IBlockRemover shall Game.Block.Interface名前空間に配置される
4. The IBlockRemover shall XMLドキュメントコメントで各パラメータの意味を明記する

### Requirement 4: ブロック破壊機能の実装
**Objective:** システム開発者として、IBlockRemoverインターフェースの具体実装を提供することで、実際のブロック破壊処理を実行できるようにしたい

#### Acceptance Criteria
1. The Game.Block shall BlockRemoverという名前のクラスでIBlockRemoverを実装する
2. When RemoveBlock(position, removalType)が呼び出された場合, the BlockRemover shall worldDatastore.Remove()を使用してブロックを削除する
3. When RemoveBlock(position, removalType)が呼び出された場合, the BlockRemover shall 削除理由（removalType）をログまたはイベントシステムに記録する
4. The BlockRemover shall Game.Block名前空間に配置される
5. The BlockRemover shall worldDatastoreへの依存をコンストラクタインジェクションで受け取る

### Requirement 5: DIコンテナへの登録
**Objective:** システム開発者として、IBlockRemoverをDIコンテナに登録することで、他のコンポーネントから自動注入できるようにしたい

#### Acceptance Criteria
1. The Server.Boot shall IBlockRemoverをDIコンテナにシングルトンとして登録する
2. The DI登録 shall BlockRemoverを具体的な実装として指定する
3. When DIコンテナが構築された場合, the システム shall IBlockRemoverの解決時にBlockRemoverのインスタンスを返す
4. The DI登録 shall サーバー起動時の初期化フェーズで実行される

### Requirement 6: VanillaIBlockTemplatesへの注入
**Objective:** システム開発者として、VanillaIBlockTemplatesのコンストラクタでIBlockRemoverを受け取ることで、ブロックテンプレート生成時に破壊機能を利用可能にしたい

#### Acceptance Criteria
1. The VanillaIBlockTemplates shall コンストラクタでIBlockRemoverを受け取るパラメータを追加する
2. The VanillaIBlockTemplates shall 受け取ったIBlockRemoverをプライベートフィールドに保存する
3. When BlockTemplateを生成する際, the VanillaIBlockTemplates shall IBlockRemoverをBlockTemplateに渡す
4. The VanillaIBlockTemplates shall 既存のコンストラクタ引数との互換性を保つ

### Requirement 7: BlockTemplateからGearEnergyTransformerへの伝達
**Objective:** システム開発者として、BlockTemplateを経由してGearEnergyTransformerにIBlockRemoverを渡すことで、各歯車ブロックが破壊機能を利用できるようにしたい

#### Acceptance Criteria
1. The BlockTemplate shall IBlockRemoverをGearEnergyTransformerのコンストラクタまたは初期化メソッドに渡す
2. The GearEnergyTransformer shall 受け取ったIBlockRemoverをプライベートフィールドに保存する
3. When GearEnergyTransformerが生成された場合, the GearEnergyTransformer shall IBlockRemoverがnullでないことを前提とする
4. The BlockTemplate shall 他のブロックタイプへの影響を最小限に抑える

### Requirement 8: 歯車ネットワークのRPM・トルク監視
**Objective:** プレイヤーとして、歯車ネットワークのRPMとトルクが適切に監視されることで、過負荷状態を検出できるようにしたい

#### Acceptance Criteria
1. The GearEnergyTransformer shall マスターデータから`overloadCheckIntervalSeconds`を読み込む
2. When 指定された時間間隔が経過した場合, the GearEnergyTransformer shall 所属する歯車ネットワーク全体の現在のRPMとトルクを取得する
3. The GearEnergyTransformer shall 取得したRPMとトルクをマスターデータの`maxRpm`および`maxTorque`と比較する
4. The 監視処理 shall Core.Update更新システムを利用して定期的に実行される
5. When GearEnergyTransformerが破壊された場合, the システム shall 監視処理を停止する

### Requirement 9: 過負荷時の破壊確率計算
**Objective:** プレイヤーとして、RPMやトルクの超過度合いに応じた破壊確率が適用されることで、過負荷の危険性を直感的に理解したい

#### Acceptance Criteria
1. When RPMが`maxRpm`を超過した場合, the GearEnergyTransformer shall 超過倍率を計算する（現在のRPM / maxRpm）
2. When トルクが`maxTorque`を超過した場合, the GearEnergyTransformer shall 超過倍率を計算する（現在のトルク / maxTorque）
3. When RPMのみが超過している場合, the GearEnergyTransformer shall 最終破壊確率 = baseBreakageProbability × RPM超過倍率 として計算する
4. When トルクのみが超過している場合, the GearEnergyTransformer shall 最終破壊確率 = baseBreakageProbability × トルク超過倍率 として計算する
5. When RPMとトルクの両方が超過している場合, the GearEnergyTransformer shall 最終破壊確率 = baseBreakageProbability × RPM超過倍率 × トルク超過倍率 として計算する
6. The 計算された破壊確率 shall 0.0～1.0の範囲に制限される（1.0を超えた場合は1.0とする）

### Requirement 10: 確率的ブロック破壊の実行
**Objective:** プレイヤーとして、計算された確率に基づいてブロックが破壊されることで、予測可能かつゲーム性のある破壊メカニズムを体験したい

#### Acceptance Criteria
1. When 過負荷チェック間隔ごとに確率判定が実行される場合, the GearEnergyTransformer shall 0.0～1.0の乱数を生成する
2. When 生成された乱数が計算された破壊確率以下の場合, the GearEnergyTransformer shall IBlockRemover.RemoveBlock()を呼び出してブロックを破壊する
3. When RemoveBlock()を呼び出す際, the GearEnergyTransformer shall BlockRemovalType.Brokenを指定する
4. When RemoveBlock()を呼び出す際, the GearEnergyTransformer shall 自身のBlockPositionInfoを渡す
5. When ブロックが破壊された場合, the GearEnergyTransformer shall それ以降の監視処理を停止する
6. When 乱数が破壊確率を上回った場合, the GearEnergyTransformer shall ブロックを破壊せず、次のチェック間隔まで監視を継続する

### Requirement 11: エラーハンドリングと境界条件
**Objective:** システム開発者として、不正なデータや境界条件に対して適切に動作することで、システムの安定性を確保したい

#### Acceptance Criteria
1. When `maxRpm`または`maxTorque`が0以下の場合, the GearEnergyTransformer shall 過負荷チェックを無効化する（破壊しない）
2. When `overloadCheckIntervalSeconds`が0以下の場合, the GearEnergyTransformer shall デフォルト値（例：1.0秒）を使用する
3. When `baseBreakageProbability`が0.0以下の場合, the GearEnergyTransformer shall 破壊確率を0.0として扱う（破壊しない）
4. When `baseBreakageProbability`が1.0以上の場合, the GearEnergyTransformer shall 破壊確率を1.0として扱う
5. If 歯車ネットワークの取得に失敗した場合, then the GearEnergyTransformer shall 過負荷チェックをスキップし、次の間隔まで待機する
6. The GearEnergyTransformer shall try-catchを使用せず、条件分岐とnullチェックでエラーを処理する

### Requirement 12: テスタビリティとモック対応
**Objective:** テスト開発者として、GearEnergyTransformerの過負荷破壊ロジックを単体テストできるようにすることで、品質を保証したい

#### Acceptance Criteria
1. The IBlockRemover shall モック化可能なインターフェースとして設計される
2. The GearEnergyTransformer shall IBlockRemoverへの依存をコンストラクタまたはプロパティで受け取る
3. When ユニットテストを実行する際, the テストコード shall モックIBlockRemoverを注入してRemoveBlockの呼び出しを検証できる
4. The テスト用マスターデータ shall 過負荷破壊パラメータを含むGearEnergyTransformブロックを定義する
5. The テストコード shall 異なる超過倍率における破壊確率の計算ロジックを検証する

