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
- [x] Task 4: 実走検証（プレイテストDSL）、8/20裁定ファイルのsuperseded追記、PR #1231

## Task 4 検証結果（2026-08-23）
- シナリオ `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/skit-opening-spawn-relative-origin.cs`（4回連続 Success・assert 9/9 PASS）
- サーバー実スポーン `(186.00, 15.70, -37.40)` に対し、スキットカメラ 23.61m・船内カット(Interior) 24.53m。焼き直し時の基準点 `(500, 15.65, 500)` にも旧絶対座標 `(496, 14.8, 475.8)` にも張り付いておらず、原点加算が実行時に効いている
- 未達: WebUiHost(CEF)のWS接続が確立せず画面全体が白飛びし、録画・スクショでの目視確認は取れていない（コード側ではなく環境要因。EditModeテストとassertは全緑）

## 判断台帳（ADR）
- Requirements: 原点はスポーン地点そのものXYZ（ユーザー裁定 AskUserQuestion 2026-08-22「スポーン地点そのもの XYZ（推奨）」）
- Requirements: 4種コマンドすべて常に原点加算・フラグ無し（ユーザー裁定 AskUserQuestion 2026-08-22「4種すべて常に原点加算（推奨）」）
- Requirements: 傾斜対策は範囲外・起動して見てから別タスク（ユーザー裁定 AskUserQuestion 2026-08-22「今回は相対化のみ（推奨）」）
- Requirements: JSONは(500, 15.6462908, 500)減算で焼き直し（ADR 0029・.decisions/2026-08-22）
