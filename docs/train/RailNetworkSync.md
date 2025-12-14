# Rail Network Communication

## Overview

Rail関連のサーバー／クライアント通信は現在次の4経路で構成されます。ノード・接続の初期状態はスナップショットで構築し、その後は差分イベントとリクエストで同期します。

1. **RailGraphSnapshot (`va:getRailGraphSnapshot`)**  
   - **サーバー**: `GetRailGraphSnapshotProtocol` が `RailGraphDatastore.CaptureSnapshot()` を呼び出し、すべてのノードと接続、接続ハッシュを `RailGraphSnapshotMessagePack` にまとめて返します。  
   - **クライアント**: ハンドシェイク直後に `RailGraphSnapshotInitializer` が `RailGraphClientCache.ApplySnapshot` を実行してキャッシュを一括構築します。

2. **RailNodeCreatedEvent (`va:event:railNodeCreated`)**  
   - **サーバー**: `RailGraphDatastore` にノードが追加されるたび `RailNodeCreatedEventPacket` が `RailNodeCreatedMessagePack` をブロードキャストします。  
   - **クライアント**: `RailGraphCacheNetworkHandler` が購読し、`RailGraphClientCache.UpsertNode` を通じて Guid/制御点/ConnectionDestination を差分適用します。

3. **RailConnectionCreatedEvent (`va:event:railConnectionCreated`)**  
   - **サーバー**: `ConnectNodeInternal` で接続が確定すると `RailConnectionCreatedEventPacket` が `fromNodeId/guid`, `toNodeId/guid`, `distance` を送出します。  
   - **クライアント**: `RailGraphConnectionNetworkHandler` が Guid 整合を確認して `_cache.UpsertConnection` を実行し、さらに `TrainRailObjectManager` などへイベントを転送します。

4. **RailConnectionEditProtocol (`va:railConnectionEdit`)**  
   - **クライアント**: `TrainRailConnectSystem` がレイキャストで取得した `ConnectionDestination` を `RailGraphClientCache` から nodeId/guid に引き当て、`VanillaApiSendOnly.ConnectRail/DisconnectRail` を送信します。  
   - **サーバー**: `RailConnectionEditProtocol` が `RailConnectionCommandHandler` に処理を委譲し、nodeId/guid を照合して接続／切断を実行。結果の差分はイベント(項目2,3)で配信されます。

## Data Flow Highlights

- **ConnectionDestination**: RailComponent座標＋インデックス＋Front/Back情報。ノード生成時にサーバーから伝達され、以後はクライアントキャッシュで逆引きします。
- **Guid Validation**: nodeId に加え Guid も必ず照合し、古い ID を参照した誤差分適用を防ぎます。
- **差分前提**: スナップショット適用後はイベントのみで同期し、ハッシュは今後 Tick と組み合わせて検証する想定です。
