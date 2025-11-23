# Research & Design Decisions

## Summary
- **Feature**: gear-overload-breakage
- **Discovery Scope**: Extension (既存歯車システムへの機能拡張)
- **Key Findings**:
  - 既存のGearEnergyTransformerクラスとGearNetworkシステムが確立されており、これらを拡張する形で実装可能
  - DIコンテナパターンとしてMicrosoft.Extensions.DependencyInjectionが採用されており、IBlockRemoverをシングルトン登録する
  - Core.Updateシステム(GameUpdater.UpdateObservable)を利用した定期的な監視処理が既存パターンとして存在
  - blocks.yamlスキーマの拡張はSourceGeneratorによって自動生成されるため、YAMLファイルの更新のみで型安全なアクセスが可能

## Research Log

### GearEnergyTransformerとGearNetworkの既存実装調査
- **Context**: 歯車過負荷監視ロジックを実装するため、既存の歯車システムアーキテクチャを理解する必要があった
- **Sources Consulted**:
  - `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearEnergyTransformerComponent.cs`
  - `moorestech_server/Assets/Scripts/Game.Gear/Common/GearNetwork.cs`
  - `moorestech_server/Assets/Scripts/Game.Gear/Common/GearNetworkDatastore.cs`
- **Findings**:
  - `GearEnergyTransformer`は`IGearEnergyTransformer`インターフェースを実装し、`BlockInstanceId`、`CurrentRpm`、`CurrentTorque`プロパティを持つ
  - `GearNetwork`は`CurrentGearNetworkInfo`プロパティを通じてネットワーク全体のRPM/トルク情報を提供
  - `GearNetworkDatastore`は静的シングルトンパターンで、`GetGearNetwork(BlockInstanceId)`メソッドで各ブロックが所属するネットワークを取得可能
  - `GearEnergyTransformer`のコンストラクタは`Torque requiredTorque`, `BlockInstanceId`, `IBlockConnectorComponent<IGearEnergyTransformer>`を受け取る
  - `Destroy()`メソッドで`GearNetworkDatastore.RemoveGear(this)`を呼び出し、ネットワークから自身を削除
- **Implications**:
  - 過負荷監視ロジックは`GearEnergyTransformer`内に実装し、`GearNetworkDatastore.GetGearNetwork(BlockInstanceId)`でネットワーク情報を取得
  - ブロック破壊時は`IBlockRemover.RemoveBlock()`を呼び出し、それが`WorldBlockDatastore.RemoveBlock()`→`Block.Destroy()`→`GearEnergyTransformer.Destroy()`の順で実行される

### WorldBlockDatastoreとブロック削除メカニズム
- **Context**: ブロック破壊処理の実装パターンを確認する必要があった
- **Sources Consulted**:
  - `moorestech_server/Assets/Scripts/Game.World/DataStore/WorldBlockDatastore.cs`
  - `moorestech_server/Assets/Scripts/Game.World.Interface/DataStore/IWorldBlockDatastore.cs`
- **Findings**:
  - `IWorldBlockDatastore.RemoveBlock(Vector3Int pos)`は既存のインターフェースメソッド
  - `WorldBlockDatastore.RemoveBlock(Vector3Int pos)`の実装は以下の処理を行う：
    1. 座標からBlockInstanceIdを取得
    2. WorldBlockUpdateEventを発火（イベントリスナーへの通知）
    3. `Block.Destroy()`を呼び出し（各コンポーネントのクリーンアップ）
    4. 内部辞書から削除（`_blockMasterDictionary`、`_coordinateDictionary`）
  - `BlockPositionInfo`クラスは座標、方向、サイズを保持し、ブロックの位置情報として使用される
- **Implications**:
  - `IBlockRemover`の実装では`IWorldBlockDatastore`への依存を持ち、`RemoveBlock(Vector3Int)`を呼び出す
  - ブロック破壊タイプ（`BlockRemovalType`）をログ/イベントシステムに記録するロジックが必要
  - `GearEnergyTransformer`は`BlockPositionInfo`を保持していないため、`IBlock.BlockPositionInfo`を通じて取得する必要がある

### VanillaIBlockTemplatesとBlockTemplateパターン
- **Context**: IBlockRemoverを各GearEnergyTransformerに注入する経路を確認
- **Sources Consulted**:
  - `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`
  - `moorestech_server/Assets/Scripts/Game.Block.Interface/IBlockTemplate.cs`
- **Findings**:
  - `VanillaIBlockTemplates`のコンストラクタは現在`IBlockOpenableInventoryUpdateEvent`のみを受け取る
  - 各ブロックタイプは`Dictionary<string, IBlockTemplate>`で管理され、BlockTypeConst（例：`BlockTypeConst.Gear`）をキーとする
  - `IBlockTemplate`インターフェースは`IBlock CreateBlock(...)`メソッドを定義
  - `VanillaGearTemplate`、`VanillaShaftTemplate`等の既存テンプレートが存在
- **Implications**:
  - `VanillaIBlockTemplates`のコンストラクタに`IBlockRemover`パラメータを追加
  - 歯車関連の`BlockTemplate`実装（`VanillaGearTemplate`、`VanillaShaftTemplate`等）にIBlockRemoverを渡す
  - `GearEnergyTransformer`のコンストラクタシグネチャを拡張し、`IBlockRemover`を受け取るように変更

### DIコンテナとサービス登録パターン
- **Context**: IBlockRemoverをDIコンテナに登録する方法を確認
- **Sources Consulted**:
  - `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- **Findings**:
  - DIフレームワーク：`Microsoft.Extensions.DependencyInjection`
  - 登録パターン：`services.AddSingleton<IInterface, Implementation>()`
  - 初期化フェーズは2段階：
    1. `initializerCollection`（ServerContext用）：`IItemStackFactory`, `IBlockFactory`, `IWorldBlockDatastore`等
    2. `services`（ゲームプレイ用）：`IPlayerInventoryDataStore`, `IEntitiesDatastore`等
  - `VanillaIBlockTemplates`はinitializerCollectionに登録され、`ServerContext`経由で利用可能
  - `IWorldBlockDatastore`は`WorldBlockDatastore`として既に登録済み
- **Implications**:
  - `IBlockRemover`と`BlockRemover`は`initializerCollection`に追加（`VanillaIBlockTemplates`のコンストラクタで必要なため）
  - `BlockRemover`は`IWorldBlockDatastore`への依存をコンストラクタインジェクションで受け取る
  - 登録順序：`IWorldBlockDatastore` → `IBlockRemover` → `VanillaIBlockTemplates`

### Core.Updateシステムと定期実行パターン
- **Context**: 過負荷チェックを定期的に実行するメカニズムを調査
- **Sources Consulted**:
  - `moorestech_server/Assets/Scripts/Core.Update/GameUpdater.cs`
  - `moorestech_server/Assets/Scripts/Game.Gear/Common/GearNetworkDatastore.cs`
- **Findings**:
  - `GameUpdater.UpdateObservable`はIObservable<Unit>を提供し、ゲームループごとに通知
  - `GameUpdater.UpdateSecondTime`で前回更新からの経過秒数を取得可能
  - `GearNetworkDatastore`はコンストラクタで`GameUpdater.UpdateObservable.Subscribe(Update)`を登録
  - UniRxを利用したReactive拡張がプロジェクト全体で採用されている
- **Implications**:
  - `GearEnergyTransformer`内で`GameUpdater.UpdateObservable.Subscribe()`を使用して定期チェックを実装
  - 経過時間の累積には`GameUpdater.UpdateSecondTime`を使用
  - `Destroy()`メソッドで購読を解除（`IDisposable`パターン）

### blocks.yamlスキーマ拡張パターン
- **Context**: 過負荷パラメータをマスターデータに追加する方法を確認
- **Sources Consulted**:
  - `VanillaSchema/blocks.yml`
- **Findings**:
  - 既存のインターフェース定義例：`IGearMachineParam`（`requireTorque`, `requiredRpm`）、`IGearConnectors`（`gear`）
  - SourceGeneratorが`Mooresmaster.Model.BlocksModule`名前空間にクラスを自動生成
  - `defineInterface`セクションで新しいインターフェースを定義可能
  - プロパティには`key`, `type`, `default`を指定
  - データ型：`integer`, `number`（浮動小数点）, `string`, `uuid`, `enum`, `ref`等
- **Implications**:
  - `IGearOverloadParam`インターフェースを`defineInterface`に追加
  - パラメータ：`maxRpm` (integer), `maxTorque` (number), `overloadCheckIntervalSeconds` (number), `baseBreakageProbability` (number)
  - デフォルト値を設定してオプショナルパラメータとする（既存ブロックへの影響を最小化）

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| GearEnergyTransformer内包型 | 過負荷監視ロジックをGearEnergyTransformer内部に実装 | 既存アーキテクチャとの整合性が高い、シンプルな実装 | GearEnergyTransformerの責務が増加 | 既存の`SupplyPower()`メソッド同様、監視処理も内部で完結させる設計が自然 |
| 独立監視サービス型 | GearOverloadMonitorServiceを作成し、全歯車を監視 | 責務の分離が明確 | 追加のDI設定、GearNetworkDatastoreとの重複ロジック | 既存パターンから逸脱し、複雑化する可能性 |
| GearNetworkレベル監視型 | GearNetwork.ManualUpdate()内で過負荷チェック | ネットワーク全体の状態を一箇所で管理 | GearNetworkの責務が増加、個別ブロックの破壊処理が複雑化 | ネットワーク単位での破壊判定は可能だが、個別ブロック破壊には不向き |

**選択**: **GearEnergyTransformer内包型**
**理由**: 既存の`GearEnergyTransformer`は`SupplyPower()`で供給されたRPM/トルクを受け取り、内部状態を更新する責務を持つ。過負荷監視もこの延長として実装することで、既存アーキテクチャとの整合性を保ちつつ、シンプルな実装が可能。

## Design Decisions

### Decision: ブロック破壊タイプをEnumで定義
- **Context**: 要件2で`BlockRemovalType` enumを定義し、破壊原因を区別する必要がある
- **Alternatives Considered**:
  1. Enumで定義 - `ManualRemove`, `Broken`
  2. 文字列ベースの識別 - `"manual"`, `"broken"`
- **Selected Approach**: Enum型を使用し、`Game.Block.Interface`名前空間に配置
- **Rationale**: 型安全性を確保し、将来的な破壊タイプの追加（例：`Expired`, `Collision`）に対応しやすい。プロジェクト全体で強い型付けが推奨されている。
- **Trade-offs**: Enum追加によるインターフェース変更が発生するが、新規インターフェースのため後方互換性の問題なし
- **Follow-up**: `IBlockRemover.RemoveBlock()`のシグネチャに`BlockRemovalType`を含める

### Decision: IBlockRemoverインターフェースの導入
- **Context**: 要件3で依存性注入を介したブロック破壊機能を提供する必要がある
- **Alternatives Considered**:
  1. `IWorldBlockDatastore`を直接注入 - 既存インターフェースを再利用
  2. `IBlockRemover`を新規定義 - 破壊専用の抽象化
- **Selected Approach**: `IBlockRemover`インターフェースを新規定義し、`void RemoveBlock(BlockPositionInfo, BlockRemovalType)`メソッドを提供
- **Rationale**:
  - 単一責任の原則：ブロック破壊のみに特化したインターフェース
  - テスタビリティ：モック化が容易で、単体テストで破壊呼び出しを検証可能
  - 拡張性：将来的に破壊時の追加ロジック（アニメーション、サウンド等）を追加しやすい
- **Trade-offs**: 新規インターフェース追加によるコード量増加、ただし保守性とテスタビリティの向上がメリットとして大きい
- **Follow-up**: `BlockRemover`クラスで`IBlockRemover`を実装し、`IWorldBlockDatastore`をラップする

### Decision: 確率計算ロジックの実装場所
- **Context**: 要件9で過負荷時の破壊確率を計算し、要件10で確率的にブロックを破壊する
- **Alternatives Considered**:
  1. GearEnergyTransformer内のローカル関数 - `#region Internal`パターン
  2. 独立したCalculatorクラス - `OverloadProbabilityCalculator`
- **Selected Approach**: `GearEnergyTransformer`内のローカル関数として実装し、`#region Internal`で隠蔽
- **Rationale**:
  - プロジェクトのコーディング規約に従い、複雑なロジックをローカル関数で整理
  - 確率計算ロジックはGearEnergyTransformer固有の処理であり、他コンポーネントで再利用されない
  - メインフローが一目で理解できる構造を維持
- **Trade-offs**: GearEnergyTransformer内部のコード行数が増加するが、`#region`による整理で可読性を確保
- **Follow-up**: `CalculateBreakageProbability()`ローカル関数を実装し、倍率計算ロジックを実装

### Decision: 監視処理の更新タイミング
- **Context**: 要件8で定期的なRPM/トルク監視が必要
- **Alternatives Considered**:
  1. `GameUpdater.UpdateObservable`を使用 - 毎フレーム更新
  2. UniRxの`Observable.Interval()`を使用 - 指定間隔で更新
  3. `SupplyPower()`呼び出し時にチェック - イベント駆動
- **Selected Approach**: `GameUpdater.UpdateObservable.Subscribe()`を使用し、経過時間を累積して`overloadCheckIntervalSeconds`ごとにチェック
- **Rationale**:
  - 既存の`GearNetworkDatastore.Update()`と同じパターンを踏襲
  - フレームレートに依存しない定期チェックを実現
  - `GameUpdater.UpdateSecondTime`で経過時間を取得可能
- **Trade-offs**: 毎フレームSubscribeが実行されるが、累積時間チェックで実際の処理は間隔ごとに制限される
- **Follow-up**: `Destroy()`メソッドで`IDisposable.Dispose()`を呼び出し、購読を解除

### Decision: マスターデータパラメータのデフォルト値
- **Context**: 要件1で過負荷パラメータをblocks.yamlに追加する際、既存ブロックへの影響を最小化する必要がある
- **Alternatives Considered**:
  1. デフォルト値なし - 全ブロックで明示的に指定必須
  2. デフォルト値を設定 - 既存ブロックは過負荷チェック無効
- **Selected Approach**: 以下のデフォルト値を設定
  - `maxRpm: 0`, `maxTorque: 0`, `overloadCheckIntervalSeconds: 1.0`, `baseBreakageProbability: 0.0`
- **Rationale**:
  - `maxRpm`または`maxTorque`が0以下の場合、過負荷チェックを無効化（要件11.1）
  - 既存ブロックのJSONデータを変更せずに、新機能を追加可能
  - 後方互換性を保ちながら、段階的な導入が可能
- **Trade-offs**: デフォルト値により暗黙的な動作が発生するが、ドキュメントで明示することで緩和
- **Follow-up**: テスト用マスターデータに過負荷パラメータを含むブロックを追加

## Risks & Mitigations
- **Risk 1**: `GearEnergyTransformer`のコンストラクタシグネチャ変更により、既存の`BlockTemplate`実装に影響が出る可能性
  - **Mitigation**: `IBlockRemover`パラメータをオプショナルにせず、DIコンテナで必ず注入されることを前提とする。歯車関連テンプレートのみを対象とし、他のブロックタイプには影響しない
- **Risk 2**: 確率的破壊により、プレイヤーが予期しないタイミングでブロックが破壊される可能性
  - **Mitigation**: マスターデータで`baseBreakageProbability`を適切に調整し、ゲームバランスをテストで検証。過負荷状態をUIで可視化することも将来的な改善として検討
- **Risk 3**: `GameUpdater.UpdateObservable`への大量Subscribe登録によるパフォーマンス低下
  - **Mitigation**: GearEnergyTransformerのインスタンス数は通常数十～数百程度であり、問題にならないことを想定。必要に応じてプロファイリングで確認

## References
- [moorestechプロジェクト構造](../.kiro/steering/structure.md) - アセンブリ分離原則、DIパターン
- [技術スタック](../.kiro/steering/tech.md) - マスターデータシステム、Core.Update
- [YAMLスキーマ仕様](../../moorestech_server/Assets/yaml仕様書v1.md) - blocks.yamlの記述方法
- [歯車システムドキュメント](https://splashy-relative-f65.notion.site/923bdb103c434e629e7a22b4e1618fdf?pvs=4) - GearNetworkのパワー配分ロジック
