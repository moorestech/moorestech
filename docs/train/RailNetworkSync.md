# Rail Network Communication

## Overview

Rail 系のサーバー／クライアント通信は以下の 3 経路で構成されています。ノード・接続の差分をイベントで伝達し、編集リクエストは nodeId + Guid で整合を取ります。

1. **RailNodeCreatedEvent (`va:event:railNodeCreated`)**  
   - **サーバー**: `RailGraphDatastore` でノード登録時に `RailNodeCreatedEventPacket` がブロードキャスト。`RailNodeCreatedMessagePack` には `nodeId`, `nodeGuid`, `ConnectionDestination`, `originPoint`, `front/back control point` が含まれる。  
   - **クライアント**: `RailGraphCacheNetworkHandler` が購読し、`RailGraphClientCache.UpsertNode` を通じて Guid/Tick/制御点をキャッシュ。

2. **RailConnectionCreatedEvent (`va:event:railConnectionCreated`)**  
   - **サーバー**: `ConnectNodeInternal` 実行時に `RailConnectionCreatedEventPacket` が `fromNodeId/guid`, `toNodeId/guid`, `distance` を配信。  
   - **クライアント**: `RailGraphConnectionNetworkHandler` が Guid 整合性をチェック後 `_cache.UpsertConnection` を実行。`TrainRailObjectManager` など接続イベント依存コンポーネントへ通知。

3. **RailConnectionEditProtocol (`va:railConnectionEdit`)**  
   - **クライアント**: `TrainRailConnectSystem` がレイキャスト結果の `ConnectionDestination` を `RailGraphClientCache` から nodeId/guid に変換し、`VanillaApiSendOnly.ConnectRail/DisconnectRail` を送信。  
   - **サーバー**: `RailConnectionEditProtocol` が `RailConnectionCommandHandler` に処理を委譲。nodeId/guid を照合し、距離算出と接続／切断 (`RailNode.ConnectNode/DisconnectNode`) を実行。成功すると上記イベント 2 が自然に配信される。

## Data Flow Highlights

- **ConnectionDestination**: サーバー・クライアントで同型の「レールコンポーネント座標＋インデックス＋Front/Back」情報。ノード生成時のみサーバー→クライアントで送られ、以後はクライアントがキャッシュし逆引きする。
- **Guid Validation**: イベント＆リクエストともに nodeId に加えて Guid を必ず確認することで、古い nodeId を参照した誤接続を防止。
- **差分前提**: ノード/接続ともにスナップショット同期は行わず、既存データはイベントの再送 or セーブ読み込み時のシリアライズで補完する設計。
