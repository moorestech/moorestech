---
paths:
  - "moorestech_server/Assets/Scripts/Server.Protocol/**/*"
  - "moorestech_server/Assets/Scripts/Server.Event/**/*"
  - "moorestech_client/Assets/Scripts/Client.Network/**/*"
---

必ず「creating-server-protocol」SKILLを読み込んでください。

サーバー可変状態のクライアント同期は3点セット（①Server.Event/EventReceiveのイベントパケット＋DI登録 ②初期データ：InitialHandshake同梱かva:get*全量 ③クライアントのSubscribeEventResponseハンドラ）が標準。他プロトコル応答から状態を推測合成するApplierは禁止。PacketResponse直下にはIPacketResponse実装以外を置かない。
