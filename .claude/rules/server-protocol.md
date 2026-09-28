---
paths:
  - "moorestech_server/Assets/Scripts/Server.Protocol/**/*"
  - "moorestech_server/Assets/Scripts/Server.Event/**/*"
  - "moorestech_client/Assets/Scripts/Client.Network/**/*"
---

必ず「creating-server-protocol」SKILLを読み込んでください（可変状態同期の3点セット・Applier禁止はSKILLが正本）。

PacketResponse直下にはIPacketResponse実装以外を置かない。
