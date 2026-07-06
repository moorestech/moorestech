---
name: key-files
description: moorestechの調査起点になる主要ファイルの場所（初期化パイプライン・デバッグオーバーレイ・起動統合テスト）
metadata: 
  node_type: memory
  type: reference
  originSessionId: 4af4fa5c-5b45-4d86-9cb7-4f7ba0604186
---

調査の起点になる主要ファイル:

- `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs` — ゲーム初期化パイプライン本体
- `moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugObjectsBootstrap.cs` — デバッグオーバーレイ（`RuntimeInitializeOnLoadMethod` で自動起動）
- `moorestech_client/Assets/Scripts/Client.Tests/StartGameTest.cs` — ゲーム起動統合テスト
- `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/` — PlayMode遷移型統合テスト置き場（ヘルパー: `Util/EditModeInPlayingTestUtil.cs`）

関連: [[addressables-sequential-preload]] [[editmode-test-domain-reload]]
