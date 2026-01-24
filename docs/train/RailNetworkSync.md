# Rail Network Communication

## Overview

- Train entity bootstrap is based on `GetTrainUnitSnapshotsProtocol` only; `RequestWorldDataProtocol` does not include train entities, and `TrainUnitSnapshotApplier` builds entity updates + removes stale ones (`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RequestWorldDataProtocol.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/TrainUnitSnapshotApplier.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/EntityObjectDatastore.cs`)
- 初期同期は `Client.Network.API.VanillaApiWithResponse.InitialHandShake` が `GetRailGraphSnapshotProtocol` と `GetTrainUnitSnapshotsProtocol` を呼び、`RailGraphSnapshotApplier` / `TrainUnitSnapshotApplier` がキャッシュを構築します。(`moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraphSnapshotApplier.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/TrainUnitSnapshotApplier.cs`)
- レールの差分は `RailNodeCreatedEventPacket`, `RailNodeRemovedEventPacket`, `RailConnectionCreatedEventPacket`, `RailConnectionRemovedEventPacket` と `RailGraphHashStateEventPacket` のイベントで同期します。(`moorestech_server/Assets/Scripts/Server.Event/EventReceive`, `moorestech_client/Assets/Scripts/Client.Game/InGame/Train`)
- 列車の差分は新規生成のみ `TrainUnitCreatedEventPacket` が即時配信され、削除や不整合は `TrainUnitHashStateEventPacket` と `GetTrainUnitSnapshotsProtocol` の再同期で補正します。(`moorestech_server/Assets/Scripts/Server.Event/EventReceive`, `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/TrainUnitHashVerifier.cs`)
- ブロックの設置/削除は `PlaceBlockEventPacket` / `RemoveBlockToSetEventPacket` がブロードキャストされ、クライアントは `WorldDataHandler` 経由で `BlockGameObjectDataStore` に反映します。(`moorestech_server/Assets/Scripts/Server.Event/EventReceive/PlaceBlockEventPacket.cs`, `moorestech_server/Assets/Scripts/Server.Event/EventReceive/RemoveBlockEventPacket.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/World/WorldDataHandler.cs`)

## Operation Flows

### 橋脚設置 (TrainRail ブロック)
注: 本ドキュメントでは歴史的経緯により "TrainRail" を橋脚ブロック名として扱います（レール本体ではありません）。

1. Client: `TrainRailPlaceSystem` が `RailBridgePierComponentStateDetail` を含む `PlaceInfo` を作成し、`PlaceSystemUtil.SendPlaceProtocol` -> `VanillaApiSendOnly.PlaceHotBarBlock` を送信します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRail/TrainRailPlaceSystem.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs`, `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`)
2. Server: `PlaceBlockFromHotBarProtocol` が `WorldBlockDatastore.TryAddBlock` を実行し、`VanillaTrainRailTemplate.New` が `RailComponent` を生成して `RailGraphDatastore` にノードを登録します。(`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockFromHotBarProtocol.cs`, `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaTrainRailTemplate.cs`)
3. Broadcast: `PlaceBlockEventPacket` (va:event:blockPlace) と `RailNodeCreatedEventPacket` (va:event:railNodeCreated) が全クライアントへ送信されます。
4. Client apply: `WorldDataHandler` がブロックを反映し、`RailGraphCacheNetworkHandler` が `RailGraphClientCache.UpsertNode` を更新します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/World/WorldDataHandler.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraphCacheNetworkHandler.cs`)

### 駅設置 (TrainStation / TrainCargoPlatform)

1. Client: ブロック設置は `CommonBlockPlaceSystem` -> `PlaceSystemUtil.SendPlaceProtocol` -> `VanillaApiSendOnly.PlaceHotBarBlock` で送信されます。(`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs`)
2. Server: `PlaceBlockFromHotBarProtocol` が `VanillaTrainStationTemplate.New` / `VanillaTrainCargoTemplate.New` を呼び、`RailComponentUtility.Create2RailComponents` と `RegisterAndConnetStationBlocks` で駅内接続および隣接駅との自動接続を行います。(`moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaTrainStationTemplate.cs`, `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaTrainCargoTemplate.cs`, `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Utility/RailComponentUtility.cs`)
3. Broadcast: `PlaceBlockEventPacket` に加え、駅内接続/自動接続に応じて `RailNodeCreatedEventPacket` と `RailConnectionCreatedEventPacket` が配信されます。
4. Client apply: `WorldDataHandler` がブロックを作成し、`RailGraphCacheNetworkHandler` と `RailGraphConnectionNetworkHandler` がキャッシュを更新、`ClientStationReferenceRegistry` が駅参照を再適用します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraphConnectionNetworkHandler.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/ClientStationReferenceRegistry.cs`)

### レール接続 (手動)

1. Client: `TrainRailConnectSystem` が `RailGraphClientCache` から nodeId/guid を解決し、`VanillaApiSendOnly.ConnectRail` (va:railConnectionEdit) を送信します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRailConnect/TrainRailConnectSystem.cs`, `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`)
2. Server: `RailConnectionEditProtocol` が `RailConnectionCommandHandler.TryConnect` を実行し、`RailGraphDatastore.ConnectNode` が接続を確定します。(`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RailConnectionEditProtocol.cs`, `moorestech_server/Assets/Scripts/Game.Train/RailGraph/RailConnectionCommandHandler.cs`)
3. Broadcast: `RailConnectionCreatedEventPacket` (va:event:railConnectionCreated) が配信されます。
4. Client apply: `RailGraphConnectionNetworkHandler` が `RailGraphClientCache.UpsertConnection` を適用し、必要に応じて `TrainRailObjectManager` に転送します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraphConnectionNetworkHandler.cs`)

### レール削除 (ブロック削除)

1. Client: `DeleteObjectState` が `VanillaApiSendOnly.BlockRemove` (va:removeBlock) を送信します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`, `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`)
2. Server: `RemoveBlockProtocol` が `WorldBlockDatastore.RemoveBlock` を実行し、レールブロック内の `RailComponent.Destroy` -> `RailNode.Destroy` を通じて `RailGraphDatastore.RemoveNode` が走ります。(`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`, `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/RailComponent.cs`, `moorestech_server/Assets/Scripts/Game.Train/RailGraph/RailNode.cs`)
3. Broadcast: `RemoveBlockToSetEventPacket` (va:event:removeBlock) に加え、`RailNodeRemovedEventPacket` と `RailConnectionRemovedEventPacket` が配信されます。
4. Client apply: `WorldDataHandler` がブロックを除去し、`RailGraphCacheNetworkHandler` / `RailGraphConnectionNetworkHandler` がノードと接続を削除します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/World/WorldDataHandler.cs`)

### レール接続解除 (接続エッジ削除)

1. Client: `VanillaApiWithResponse.DisconnectRailAsync` が `RailConnectionEditProtocol` (Disconnect) を要求します。(`moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`)
2. Server: `RailConnectionEditProtocol` が `TrainRailPositionManager.CanRemoveEdge` を検証し、`RailConnectionCommandHandler.TryDisconnect` を実行します。(`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RailConnectionEditProtocol.cs`)
3. Broadcast: `RailConnectionRemovedEventPacket` (va:event:railConnectionRemoved) が配信されます。
4. Client apply: `RailGraphConnectionNetworkHandler` が `RailGraphClientCache.RemoveConnection` を適用します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraphConnectionNetworkHandler.cs`)

### TrainCar 設置

1. Client: `TrainCarPlaceSystem` が `TrainCarPlacementDetector` の `RailPositionSaveData` を `VanillaApiWithResponse.PlaceTrainOnRail` で送信します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/TrainCarPlaceSystem.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/TrainCarPlacementDetector.cs`)
2. Server: `PlaceTrainCarOnRailProtocol` がレールスナップショットとアイテムを検証し、`TrainUnit` を生成して `TrainUpdateService` に登録します。(`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceTrainCarOnRailProtocol.cs`, `moorestech_server/Assets/Scripts/Game.Train/Common/TrainUpdateService.cs`)
3. Broadcast: `TrainUnitCreatedEventPacket` (va:event:trainUnitCreated) が列車スナップショットとエンティティ情報を配信します。(`moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainUnitCreatedEventPacket.cs`)
4. Client apply: `TrainUnitCreatedEventNetworkHandler` が `TrainUnitClientCache.Upsert` と `EntityObjectDatastore.OnEntitiesUpdate` を実行します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/Train/TrainUnitCreatedEventNetworkHandler.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/EntityObjectDatastore.cs`)

### TrainCar 削除

Note: `TrainUnitSnapshotApplier` removes stale train entities via `EntityObjectDatastore.RemoveTrainEntitiesNotInSnapshot`.
1. Client: `DeleteObjectState` が `VanillaApiSendOnly.RemoveTrain` (va:removeTrainCar) を送信します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`, `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`)
2. Server: `RemoveTrainCarProtocol` が `TrainUnit.RemoveCar` を実行し、編成が空になった場合は `TrainUnit.OnDestroy` で `TrainUpdateService.UnregisterTrain` が呼ばれます。(`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveTrainCarProtocol.cs`, `moorestech_server/Assets/Scripts/Game.Train/Train/TrainUnit.cs`)
3. Broadcast: 直接の削除イベントはなく、`TrainUnitHashStateEventPacket` (va:event:trainUnitHashState) によるハッシュ検証で差分が検出されると `GetTrainUnitSnapshotsProtocol` による再同期が走ります。(`moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainUnitHashStateEventPacket.cs`, `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/TrainUnitHashVerifier.cs`)
4. Client apply: `TrainUnitSnapshotApplier` がキャッシュを上書きし、欠落した列車を削除します。(`moorestech_client/Assets/Scripts/Client.Game/InGame/Train/TrainUnitSnapshotApplier.cs`)

## Data Flow Highlights

- `ConnectionDestination` と `Guid` 検証により、古い nodeId の誤適用を防止します。(`moorestech_server/Assets/Scripts/Game.Train/RailGraph/RailConnectionCommandHandler.cs`)
- `RailGraphHashStateEventPacket` と `TrainUnitHashStateEventPacket` は 1 秒周期でハッシュを配信し、クライアントが差分を検知した場合のみスナップショット再取得を行います。
