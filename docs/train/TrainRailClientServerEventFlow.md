# Train/Rail クライアント起点イベントフロー

このドキュメントは、以下 6 操作について  
「クライアント起点 -> サーバープロトコル層 -> ゲーム層処理 -> 通知 -> クライアント適用」  
の実コード経路をまとめたものです。

- RailNode 新規生成
- RailNode 削除
- RailSegment(レール本体) 新規生成
- RailSegment(レール本体) 削除
- TrainCar 新規生成
- TrainCar 削除

## 共通の通知配送レイヤー

1. サーバー側は各 EventPacket から `EventProtocolProvider.AddBroadcastEvent(...)` を呼ぶ。  
2. クライアント側は `VanillaApiEvent` が `va:event` をポーリングして受信イベントをタグごとに配信する。  
3. Train/Rail 系ハンドラは即時適用せず `TrainUnitFutureMessageBuffer.EnqueueEvent(serverTick, tickSequenceId, ...)` に積む。  
4. `TrainUnitClientSimulator` が tick 進行時に flush して適用する。

---

## 1) RailNode 新規生成

### クライアント起点

- `TrainRailPlaceSystem.ManualUpdate(...)`
- `PlaceSystemUtil.SendPlaceProtocol(...)`
- `VanillaApiSendOnly.PlaceHotBarBlock(...)` (`va:palceHotbarBlock`)

### サーバープロトコル層

- `PlaceBlockFromHotBarProtocol.GetResponse(...)`
- `ServerContext.WorldBlockDatastore.TryAddBlock(...)`

### ゲーム層処理

- ブロック生成時に `VanillaTrainRailTemplate.New(...)` / `VanillaTrainStationTemplate.New(...)` が `RailComponent` を生成
- `RailComponent` コンストラクタで `IRailGraphDatastore.AddNodePair(...)`
- `RailGraphDatastore.AddNodePairInternal(...)` で `_nodeInitializationNotifier.Notify(...)`

### 通知

- `RailNodeCreatedEventPacket` が `IRailGraphDatastore.GetRailNodeInitializedEvent()` を購読
- `va:event:railNodeCreated` をブロードキャスト

### クライアント適用

- `RailGraphCacheNetworkHandler.OnRailNodeCreated(...)`
- `TrainUnitFutureMessageBuffer` に enqueue
- flush 時に `RailGraphClientCache.UpsertNode(...)` と `ClientStationReferenceRegistry.ApplyStationReference(...)`

---

## 2) RailNode 削除

### クライアント起点

- `BlockGameObjectChild.Delete()`
- `VanillaApiSendOnly.BlockRemove(...)` (`va:removeBlock`)

### サーバープロトコル層

- `RemoveBlockProtocol.GetResponse(...)`
- `ServerContext.WorldBlockDatastore.RemoveBlock(...)`

### ゲーム層処理

- `WorldBlockDatastore.RemoveBlock(...)` -> `block.Destroy()`
- `BlockSystem.Destroy()` -> `BlockComponentManager.Destroy()`
- `RailComponent.Destroy()` -> `RailNode.Destroy()`
- `RailGraphDatastore.RemoveNodeInternal(...)` -> `_nodeRemovalNotifier.Notify(...)`

### 通知

- `RailNodeRemovedEventPacket` が `IRailGraphDatastore.GetRailNodeRemovedEvent()` を購読
- `va:event:railNodeRemoved` をブロードキャスト

### クライアント適用

- `RailGraphCacheNetworkHandler.OnRailNodeRemoved(...)`
- `TrainUnitFutureMessageBuffer` に enqueue
- flush 時に `RailGraphClientCache.RemoveNode(...)`

---

## 3) RailSegment(レール本体) 新規生成

### 代表経路 A: 既存ノード同士を接続

#### クライアント起点

- `TrainRailConnectSystem.SendConnectRailProtocol(...)`
- `VanillaApiSendOnly.ConnectRail(...)` (`va:railConnectionEdit`, Connect)

#### サーバープロトコル層

- `RailConnectionEditProtocol.GetResponse(...)` (Connect 分岐)

#### ゲーム層処理

- `RailConnectionCommandHandler.TryConnect(...)`
- `RailNode.ConnectNode(...)` (表側) + `ConnectOppositeNodes(...)` (裏側)
- `RailGraphDatastore.ConnectNodeInternal(...)` -> `_connectionInitializationNotifier.Notify(...)`

#### 通知

- `RailConnectionCreatedEventPacket` が `IRailGraphDatastore.GetRailConnectionInitializedEvent()` を購読
- `va:event:railConnectionCreated` をブロードキャスト

#### クライアント適用

- `RailGraphConnectionNetworkHandler.OnConnectionCreated(...)`
- `TrainUnitFutureMessageBuffer` に enqueue
- flush 時に `RailGraphClientCache.UpsertConnection(...)`

### 代表経路 B: ピアを置いて接続

- クライアント: `TrainRailConnectSystem.SendConnectRailWithPlacePierProtocol(...)` -> `VanillaApiWithResponse.PlaceRailWithPier(...)` (`va:railConnectWithPlacePier`)
- サーバー: `RailConnectWithPlacePierProtocol.GetResponse(...)` 内で
  - ブロック配置 (Node 生成フロー)
  - 接続処理 (Segment 生成フロー)
- 結果として `railNodeCreated` と `railConnectionCreated` の両方が飛ぶ

---

## 4) RailSegment(レール本体) 削除

### 代表経路 A: 明示的切断

#### クライアント起点

- `DeleteTargetRail.Delete()`
- `VanillaApiSendOnly.DisconnectRail(...)` (`va:railConnectionEdit`, Disconnect)

#### サーバープロトコル層

- `RailConnectionEditProtocol.GetResponse(...)` (Disconnect 分岐)

#### ゲーム層処理

- `RailConnectionCommandHandler.TryDisconnect(...)`
- `RailNode.DisconnectNode(...)` (表側) + `DisconnectOppositeNodes(...)` (裏側)
- `RailGraphDatastore.DisconnectNodeInternal(...)` -> `_connectionRemovalNotifier.Notify(...)`

#### 通知

- `RailConnectionRemovedEventPacket` が `IRailGraphDatastore.GetRailConnectionRemovedEvent()` を購読
- `va:event:railConnectionRemoved` をブロードキャスト

#### クライアント適用

- `RailGraphConnectionNetworkHandler.OnConnectionRemoved(...)`
- `TrainUnitFutureMessageBuffer` に enqueue
- flush 時に `RailGraphClientCache.RemoveConnection(...)`

### 代表経路 B: ブロック削除に伴う消滅

- RailNode 削除時に関連セグメントも消える
- この経路では `railConnectionRemoved` を都度投げず、`railNodeRemoved` 適用時の `RailGraphClientCache.RemoveNode(...)` 側で入出辺が同時に掃除される

---

## 5) TrainCar 新規生成

### クライアント起点

- `TrainCarPlaceSystem.RequestPlacementAsync(...)`
- `VanillaApiWithResponse.PlaceTrainOnRail(...)` (`va:placeTrainCar`)

### サーバープロトコル層

- `PlaceTrainCarOnRailProtocol.GetResponse(...)`

### ゲーム層処理

- `PlaceTrainCarOnRailProtocol` 内で `TrainUnit` を新規生成
- `TrainUnit` コンストラクタで `TrainUpdateService.RegisterTrain(...)`
- その後 `ITrainUnitSnapshotNotifyEvent.NotifySnapshot(createdTrain)`

### 通知

- `TrainUnitSnapshotEventPacket` が `ITrainUnitSnapshotNotifyEvent.OnTrainUnitSnapshotNotified` を購読
- `TrainUnitSnapshotEventMessagePack` を生成し `va:event:trainUnitSnapshot` をブロードキャスト

### クライアント適用

- `TrainUnitSnapshotEventNetworkHandler.OnEventReceived(...)`
- `TrainUnitFutureMessageBuffer` に enqueue
- flush 時に `TrainUnitClientCache.Upsert(...)` + `TrainCarObjectDatastore.OnTrainObjectUpdate(...)`

---

## 6) TrainCar 削除

### クライアント起点

- `TrainCarEntityChildrenObject.Delete()`
- `VanillaApiSendOnly.RemoveTrain(...)` (`va:removeTrainCar`)

### サーバープロトコル層

- `RemoveTrainCarProtocol.GetResponse(...)`

### ゲーム層処理

- `beforeTrains = TrainUpdateService.GetRegisteredTrains()`
- 対象 `TrainUnit.RemoveCar(...)` 実行
- `afterTrains = TrainUpdateService.GetRegisteredTrains()`
- `ITrainUnitSnapshotNotifyEvent.NotifyChangedByBeforeAfter(beforeTrains, afterTrains)`
  - 編成が残る場合: 更新スナップショット通知
  - 編成が消える場合: 削除通知

### 通知

- 5) と同じ `TrainUnitSnapshotEventPacket` で `va:event:trainUnitSnapshot` を送信

### クライアント適用

- `TrainUnitSnapshotEventNetworkHandler` で同一処理
  - `IsDeleted=true`: 編成キャッシュと全車両オブジェクトを削除
  - `IsDeleted=false`: upsert し、差分で消えた車両オブジェクトを削除

---

## 補足

- TrainCar の新規/削除は、旧 `trainCarRemoved` 差分ではなく、現在は「単機 TrainUnit snapshot イベント (`va:event:trainUnitSnapshot`)」に統一されている。  
- 各イベントには `serverTick` と `tickSequenceId` が付与され、クライアントは tick 順で適用する。
