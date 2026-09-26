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
2. クライアント側は `PacketExchangeManager` がpushイベントをmain threadへdispatchし、`VanillaApiEvent` がタグごとに配信する。購読開始前のイベントはbufferし、`InitializeDispatch` で到着順に同期replayする。
3. Train/Rail 系ハンドラは即時適用せず `TrainTickContext.Events.EnqueueEvent(serverTick, tickSequenceId, ...)` に積む。
4. `TrainUnitClientSimulator` が `TrainTickContext.AdvanceController` を呼び、共通driverがexact-keyのeventを同期flushしてからtrainのhash gateを評価する。view更新は引き続きsimulatorが担当する。

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
- `TrainTickContext.Events` に enqueue
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
- `TrainTickContext.Events` に enqueue
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
- `TrainTickContext.Events` に enqueue
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
- `TrainTickContext.Events` に enqueue
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
- `TrainTickContext.Events` に enqueue
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

## tickとstreamの所有者

`Core.Update.TickSynchronization.ServerTickClock` はセッション内uint tickを所有する。保存される累積 `GameUpdater.CurrentTick` とは別instance・別用途であり、save/loadしてもwire tickは新sessionの0から始まる。`MasterTickUpdater` は旧tickのtrain hashを発行し、clockを1回進め、`TrainTickSequenceSource.Sequence.BeginTick` でseq0へ戻してからtrain simulationとdiffを実行する。最初のeventはseq1、順序keyは `((ulong)tick << 32) | seq` のままである。

train/railのpacketは同じ `TrainTickSequenceSource` を使う。他streamは同じclockを使えても、別 `TickSequenceState` を所有する。clientは `TrainTickContext` が `ClientTickState`、`TickEventBuffer`、`ClientTickAdvanceController`、train固有の `TrainUnitHashBuffer` を所有する。共通state/buffer/driverにTrain/Railの型・通信tag・hash判断を持ち込まない。別contextへのwatermark purgeやgate停止の波及はない。

bundleはhash(n-1)とdiff(n)を運び、空diffでもsimulationを起動する。4tick間引き、dummy hash、future-only force-slip、hash不一致時resyncの判断は `TrainUnitHashVerifier` に残る。driverのcatch-up係数と1frame最大4tickも維持する。

## 初期snapshotと再同期

handshake時は `TrainFullSnapshotEventPacket` がrail→trainの順でfull snapshotを応答前にpushする。full snapshotは順序bufferへ積む通常差分と異なり、`TrainFullSnapshotEventNetworkHandler` が到着順に即時適用する。rail cacheを置換してからtrain cacheと車両viewを全再生成し、watermark以下のeventをpurgeする。その後に初期完了sourceを成功させ、`OnFullSnapshotApplied` を通知する。rail/trainの適用失敗はLogErrorと初期待機のfaultへ届く。

`MainGameInitializationFinalizer` は `InitialEventApplyWaiter` で全初期適用を待ち、地形構築後にplayer runtimeを開始する。保存乗車は `MainGameStarter.RestoreLoginState` → `MainGameContainerActivation` → `InitialRideTrainCarRequest` → `TrainHUDScreenState` → `RidingPlayerState` → `TrainCarRideFollowTargetResolver` を通り、生成済み実車両のseat markerへ追従する。

`SendTrainResync` は通信UniTaskを直接返す。ackは要求受付の応答であり、適用完了を意味しない。hash verifierのgateはsnapshot適用通知で解除し、旧要求のack失敗が新しい要求のgateを解除しないよう要求所有を照合する。通信待ち・初期待機・main-thread dispatchのawaitは維持し、差分とsnapshotの適用は同期で行う。

受入テストはraw rail/train payload、適用通知、watermark、適用直後の両hashとtrain/rail/view参照更新を照合する。保存乗車worldの実起動を通し、snapshot適用後のtick前進まで確認する。設計根拠は [ADR 0071](../adr/0071-tick-synchronization-stream-boundaries.md) を参照。

## 補足

- TrainCar の新規/削除は、旧 `trainCarRemoved` 差分ではなく、現在は「単機 TrainUnit snapshot イベント (`va:event:trainUnitSnapshot`)」に統一されている。  
- 各イベントには `serverTick` と `tickSequenceId` が付与され、クライアントは tick 順で適用する。
