# Rail Network Communication

## Overview

Rail関連のサーバー／クライアント通信は現在次の6経路で構成されます。ノード・接続の初期状態はスナップショットで構築し、その後は差分イベントとリクエストで同期します。

1. **RailGraphSnapshot (`va:getRailGraphSnapshot`)**  
   - **サーバー**: `GetRailGraphSnapshotProtocol` が `RailGraphDatastore.CaptureSnapshot()` を呼び出し、すべてのノードと接続、接続ハッシュを `RailGraphSnapshotMessagePack` にまとめて返します。  
   - **クライアント**: ハンドシェイク直後に `RailGraphSnapshotInitializer` が `RailGraphClientCache.ApplySnapshot` を実行してキャッシュを一括構築します。

2. **RailNodeCreatedEvent (`va:event:railNodeCreated`)**  
   - **サーバー**: `RailGraphDatastore` にノードが追加されるたび `RailNodeCreatedEventPacket` が `RailNodeCreatedMessagePack` をブロードキャストします。  
   - **クライアント**: `RailGraphCacheNetworkHandler` が購読し、`RailGraphClientCache.UpsertNode` を通じて Guid/制御点/ConnectionDestination を差分適用します。

3. **RailNodeRemovedEvent (`va:event:railNodeRemoved`)**  
   - **サーバー**: `RemoveNodeInternal` がノードを破棄する際に `RailNodeRemovedEventPacket` が `RailNodeRemovedMessagePack` を送信し、nodeId/guid を通知します。  
   - **クライアント**: `RailGraphCacheNetworkHandler` が Guid を照合し、`RailGraphClientCache.RemoveNode` を実行して接続ごとノードを掃除します。

4. **RailConnectionCreatedEvent (`va:event:railConnectionCreated`)**  
   - **サーバー**: `ConnectNodeInternal` で接続が確定すると `RailConnectionCreatedEventPacket` が `fromNodeId/guid`, `toNodeId/guid`, `distance` を送出します。  
   - **クライアント**: `RailGraphConnectionNetworkHandler` が Guid 整合を確認して `_cache.UpsertConnection` を実行し、さらに `TrainRailObjectManager` などへイベントを転送します。

5. **RailConnectionRemovedEvent (`va:event:railConnectionRemoved`)**  
   - **サーバー**: `DisconnectNodeInternal` がエッジ削除を処理するたび `RailConnectionRemovedEventPacket` が `RailConnectionRemovedMessagePack` を送信します。  
   - **クライアント**: `RailGraphConnectionNetworkHandler` が両端の nodeId/guid を検証し、`RailGraphClientCache.RemoveConnection` を適用します。

6. **RailConnectionEditProtocol (`va:railConnectionEdit`)**  
   - **クライアント**: `TrainRailConnectSystem` がレイキャストで取得した `ConnectionDestination` を `RailGraphClientCache` から nodeId/guid に引き当て、`VanillaApiSendOnly.ConnectRail/DisconnectRail` を送信します。  
   - **サーバー**: `RailConnectionEditProtocol` が `RailConnectionCommandHandler` に処理を委譲し、nodeId/guid を照合して接続／切断を実行。結果の差分はイベント(項目2〜5)で配信されます。
7. **RailGraphHashProtocol (`va:railGraphHash`)**  
   - **クライアント**: `RailGraphHashVerifier` が `RailGraphClientCache` から算出したハッシュ値を送信し、整合性チェックを要求します。  
   - **サーバー**: 現在のハッシュとグラフTickを返却し、不一致なら警告を出しつつ `RailGraphSnapshotMessagePack` を同レスポンスに添付してフル同期を促します。

## Data Flow Highlights

- **ConnectionDestination**: RailComponent座標＋インデックス＋Front/Back情報。ノード生成時にサーバーから伝達され、以後はクライアントキャッシュで逆引きします。
- **Guid Validation**: nodeId に加え Guid も必ず照合し、古い ID を参照した誤差分適用を防ぎます。
- **削除イベント**: ノード・接続の削除も専用イベントで通知し、Guid が一致した場合のみローカルキャッシュから除去します。
- **差分前提**: スナップショット適用後はイベントのみで同期し、定期的に `RailGraphHashProtocol` でハッシュ/Tick を照合して破損を早期検知します。
