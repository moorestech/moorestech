# Skit Spawn-Relative Origin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

## Requirements
- ADR 0029 / `.decisions/2026-08-22-スキット座標はスポーン基準の相対座標へ相対化する.md` / bd moorestech-gbkd
- スキットJSONの位置（cameraWarp / camerawork / characterTransform / controlSkitBackground）は `InitialHandshakeResponse.MapLayout.Spawn` を原点とする相対座標。4種とも常に原点加算。フラグ無し
- 100_start_game.json は現値から (500, 15.6462908, 500) を引いて相対値へ焼き直す（Remove の position [0,0,0] は触らない）
- 傾斜対策は範囲外。デバッグ用 SkitTester は原点ゼロ（agent前提）

## File Structure
- 新規 `Client.Skit/Context/SkitOrigin.cs` — 原点の保持と `ToWorld(Vector3 relative)`
- 変更 `Client.Skit/Context/StoryContextExtension.cs` — `GetSkitOrigin()`
- 変更 4コマンド（CameraWarp / Camerawork / CharacterTransform / ControlSkitBackground）— 原点加算
- 変更 `Client.Game/Skit/SkitManager.cs` — `[Inject] SkitOrigin` を StoryContext へ登録
- 変更 `Client.Starter/MainGameStarter.cs` — `new SkitOrigin(initialHandshakeResponse.MapLayout.Spawn)` を登録
- 変更 `Client.DebugSystem/Skit/SkitTester.cs` — 原点ゼロを登録
- 変更 `AddressableResources/Skit/skits/100_start_game.json` — 相対値へ焼き直し
- 新規 `Client.Tests/UnitTest/Skit/SkitOriginTest.cs` — ToWorld の加算テスト

## Tasks
- [x] Task 1: SkitOrigin 新設＋テスト＋4コマンドの原点加算（コンパイル・EditModeテスト）
- [x] Task 2: DI配線（SkitManager / MainGameStarter / SkitTester）（コンパイル・SkitWorldObjectRegistrationTest）
- [x] Task 3: JSON焼き直し（スクリプトで一括減算、差分を目視）
- [ ] Task 4: generatedモードで録画確認（船がスポーン脇に出る）、8/20裁定ファイルのsuperseded追記、PR
