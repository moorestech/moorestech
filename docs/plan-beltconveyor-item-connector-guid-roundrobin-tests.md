## 概要
ベルトコンベア上のアイテムエンティティが「入力元/出力先の ConnectorGuid」を持つこと、ならびにベルトコンベアの `IBlockInventoryInserter` が接続先をラウンドロビンで選択することを、サーバー/クライアント両側のテストで担保する。

## 現状調査メモ（根拠）
- サーバーは `Game.Entity.Interface/BeltConveyorItemEntityStateMessagePack` に `SourceConnectorGuid` / `GoalConnectorGuid` を追加済み。
- サーバーは `Server.Protocol.PacketResponse.Util/CollectBeltConveyorItems` で `beltConveyorItem.StartConnector?.ConnectorGuid` と `GoalConnector?.ConnectorGuid` をエンティティデータに詰めている。
- ラウンドロビンは `Game.Block.Blocks.BeltConveyor/VanillaBeltConveyorBlockInventoryInserter` に `_roundRobinIndex` を持ち、`InsertItem()` と `GetFirstGoalConnector()` が `GetNextTarget()` を通して巡回選択している。
- ベルトコンベアのブロックセーブは `Game.Block.Blocks.BeltConveyor/VanillaBeltConveyorInventoryItem` が JSON 化しており、現状は `itemStack` と `remainingTime` のみ（コネクター情報は復元不能というコメントあり）。

## テストで担保したいこと（観点）
### 1) ConnectorGuid がエンティティデータに入る（サーバー）
- ベルト上アイテムの `StartConnector` / `GoalConnector` が非 null の場合、`CollectBeltConveyorItems` が `BeltConveyorItemEntityStateMessagePack.SourceConnectorGuid/GoalConnectorGuid` を正しく設定する。
- `StartConnector` / `GoalConnector` が null の場合、`SourceConnectorGuid/GoalConnectorGuid` は null（許容する設計なら）である。

### 2) ラウンドロビンで出力先が回る（サーバー）
- `VanillaBeltConveyorBlockInventoryInserter.GetFirstGoalConnector()` が接続先のコネクターを巡回して返し、接続数回呼ぶと同じ順序でループする。
- `VanillaBeltConveyorBlockInventoryInserter.InsertItem()` が接続先を偏らせず（接続数 N 回の挿入で N 個の接続先に 1 回ずつ）に分配する。
- `InsertItemContext` に入る `SourceConnector` / `TargetConnector` が `ConnectedInfo.SelfConnector` / `ConnectedInfo.TargetConnector` と一致する。

### 3) セーブ/ロード後もルーティング情報が保持される（サーバー）
- 目標：セーブデータが「入力元/出力先の ConnectorGuid」を保持し、ロード後も同じ出力先に流れる（少なくとも Goal 側）。
- ここは現状実装が未完の可能性があるため、テスト追加と同時に保存形式・復元方式の確定が必要（下の「確認事項」参照）。

### 4) クライアント側でのシリアライズ整合性（クライアント）
- クライアント環境でも `BeltConveyorItemEntityStateMessagePack` の MessagePack シリアライズ/デシリアライズが `SourceConnectorGuid/GoalConnectorGuid` を保持できる。

## 変更対象ファイル（予定）
### サーバー（既存テストの拡張）
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/CollectBeltConveyorItemsTest.cs` - EntityData をデシリアライズして ConnectorGuid を検証するテストを追加/拡張
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/InsertItemContextTest.cs` - 既存の PathId 検証に加えて `ConnectorGuid` も検証（追加）
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/BeltConveyorSaveLoadTest.cs` - ベルト上アイテムの ConnectorGuid 保存/復元を検証するケースを追加

### サーバー（新規テスト）
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/VanillaBeltConveyorBlockInventoryInserterRoundRobinTest.cs` - ラウンドロビン選択と `InsertItemContext` の整合性を検証

### クライアント（新規テスト）
- `moorestech_client/Assets/Scripts/Client.Tests/BeltConveyorItemEntityStateMessagePackTest.cs` - MessagePack の往復で Guid が保持されることを検証

## 実装ステップ（テスト作成）
1. サーバー：`VanillaBeltConveyorBlockInventoryInserterRoundRobinTest` を追加
   - `BlockConnectorComponent<IBlockInventory>` をテスト内で用意し、`ConnectedTargets` を `DummyBlockInventory` 3 つで埋める。
   - `GetFirstGoalConnector()` を複数回呼び、(a) 3 回分が全て異なる (b) 4 回目が 1 回目と一致、を検証する（順序自体には依存しない）。
   - `InsertItem()` を 3 回呼び、各 `DummyBlockInventory.InsertedItems` が 1 件ずつになることを検証する。
   - `DummyBlockInventory.InsertedContexts` を見て、各挿入の `SourceConnector/TargetConnector` が対応する `ConnectedInfo` と同一参照であることを検証する。

2. サーバー：`CollectBeltConveyorItemsTest` を拡張
   - `VanillaBeltConveyorInventoryItem` を作る際に `BlockConnectInfoElement` を固定 Guid で生成して `StartConnector` / `GoalConnector` に設定する。
   - `CollectBeltConveyorItems.CollectItem(entityFactory)` の戻り `IEntity.GetEntityData()` を `MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>` し、`SourceConnectorGuid/GoalConnectorGuid` を検証する。

3. サーバー：`InsertItemContextTest` を拡張
   - 既存の `CreateInventoryConnector()` は Guid を内部で生成しているため、テスト内の `sourceConnector/targetConnector` 変数と比較して `ConnectorGuid` 一致を追加でアサートする。
   - 目的は「PathId ではなく Guid を主キーとして扱う設計」への移行をテストで固定すること。

4. サーバー：`BeltConveyorSaveLoadTest` を拡張（設計確定後）
   - 「セーブに ConnectorGuid を含める」「ロードで同じ出力先に流れる」を検証するテストを追加する。
   - 期待する復元方法に応じて、テストは以下のどちらかに寄せる：
     - A案: ロード時点で `GoalConnector` を復元できる（`BlockConnectorComponent` の既存 `BlockConnectInfoElement` を Guid で引き当て、同一参照を再設定）
     - B案: ロード時は Guid だけ保持し、接続確立後の Update 等で参照に解決する（その場合の責務/タイミングをテストで定義）

5. クライアント：`BeltConveyorItemEntityStateMessagePackTest` を追加
   - `BeltConveyorItemEntityStateMessagePack` を Guid 付きで作成 → `MessagePackSerializer.Serialize/Deserialize` の往復で Guid が一致することを検証する。

## 実行するテスト（実装後）
※プランモードでは実行しない。実装フェーズで必ず実行する。
- サーバー: `./tools/unity-test.sh moorestech_server "^Tests\\.CombinedTest\\.Server\\.CollectBeltConveyorItemsTest$"`
- サーバー: `./tools/unity-test.sh moorestech_server "^Tests\\.CombinedTest\\.Core\\.InsertItemContextTest$"`
- サーバー: `./tools/unity-test.sh moorestech_server "^Tests\\.UnitTest\\.Core\\.Other\\.VanillaBeltConveyorBlockInventoryInserterRoundRobinTest$"`
- サーバー: `./tools/unity-test.sh moorestech_server "^Tests\\.UnitTest\\.Game\\.SaveLoad\\.BeltConveyorSaveLoadTest$"`
- クライアント（コンパイルチェック）: `./tools/unity-test.sh moorestech_client "^0"`
- クライアント（対象テスト）: `./tools/unity-test.sh moorestech_client "^Client\\.Tests\\.BeltConveyorItemEntityStateMessagePackTest$" isGui`

## 要件確定（ユーザー回答反映）
1. セーブ/ロードで ConnectorGuid を完全に保持し、ロード後も同じ出力先に流す（必須）。
2. `BeltConveyorItemEntityStateMessagePack.SourceConnectorGuid/GoalConnectorGuid` は null 許容。
   - 例：ロード直後で参照解決前、デバッグ的にコネクタ無しで生成された場合など。
3. スプリッター挙動は「ベルトに入った時点で Goal をラウンドロビンで確定し、アイテムごとに固定」。
