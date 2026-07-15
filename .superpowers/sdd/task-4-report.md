# Task 4 DI登録と全体回帰 レポート

## 実装

- `MainGameStarter` の `PlayerViewModeController` は既に `AsSelf().As<IStartable>().As<ITickable>()` 登録済みであることを確認した。
- `fps-tps-view-toggle-via-ui.cs` に BuildMenu / PlayerInventory 表示中の V キー FPS/TPS 往復を追加した。
- 各 UI について、表示状態維持、FPS 距離への遷移、初期 TPS 距離への復元を assert した。
- 削除済み `AimPointProvider.CurrentMode` 参照を、DI 解決した `PlayerViewModeController.GetCurrentMode()` へ更新した。
- PlaceBlock のカーソル assert を、UI 側のカーソル解放要求を維持する現設計へ合わせた。
- PlaceBlock 遷移前に有効な地面位置へマウスを移動し、操作前提を明示した。

## テストコマンドと結果

- `uloop compile --project-path ./moorestech_client`: PASS、Error 0 / Warning 0。
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerViewMode|AimPointProvider|ThirdPersonCameraDistance|PlayerObjectModelVisibility|MapObjectMiningAim|UIStateControl"`: 17/17 PASS。
- `run-scenario.sh ... fps-tps-view-toggle-via-ui.cs /tmp/moorestech-master-task4/server_v8`: `Success: true`、44/44 assert PASS。
- 録画: `moorestech_client/PlaytestResults/20260714_193140/fps-tps-view-toggle-via-ui/recording.mp4`（非ゼロ、約 4.7 MB）。
- BuildMenu / PlayerInventory の PNG を目視し、UI と実プレイ画面が同時に表示されることを確認した。

## E2E assert

- GameScreen、BuildMenu、PlaceBlock、DeleteBar、PlayerInventory で視点状態を検証した。
- BuildMenu / PlayerInventory 表示中に FPS 距離へ切り替わり、UI state が変わらず、再度 V で初期 TPS 距離へ復元した。
- PlaceBlock の TPS マウス照準設置と FPS 中央照準設置が成功した。
- FPS のまま DeleteBar / GameScreen へ遷移し、視点状態が維持された。
- FPS 中のホットバー持ち替え後も全自機 Renderer が非表示だった。
- 最終 TPS 復帰時に Renderer とクロスヘア表示が復元された。

## 静的確認

- ViewMode 配下の `UIStateEnum|TextInputFocusProvider|SetUIState|IsViewState|IsMouseAimState`: 一致 0 件。
- UIState 配下の `PlayerViewModeController`: 一致 0 件。
- `git diff --check origin/master-fable-tmp...HEAD`: 手書きコードの問題なし。既存 Unity 生成 `Client.Tests/Mining.meta` / `Player.meta` の末尾空白のみ検出。
- シナリオは 200 行未満。partial、手動 `.meta`、Unity YAML の直接編集なし。

## 変更ファイル

- `.claude/skills/unity-playmode-recorded-playtest/scenarios/fps-tps-view-toggle-via-ui.cs`
- `.superpowers/sdd/task-4-report.md`

`MainGameStarter.cs` は Task 1-3 時点で要求登録が完成済みのため、Task 4 では追加差分なし。

## 自己レビュー

- UI state とカメラ距離を別々に assert し、単に Controller の mode だけを見るテストにはしていない。
- OS 入力を使わず、Playtest DSL の `QueueStateEvent` 経路だけで操作した。
- 初回 QA で動的コンパイル不能な旧 API 参照と、現設計に反するカーソル期待値を検出して修正した。
- `.moorestech-external-revisions.json` の既存未コミット差分は変更・ステージしていない。

## 懸念

- 最終シナリオ自体は `Success: true` だが、結果の `ErrorLogs` に既存 `SlopeBlockPlaceSystem` の「地面が見つかりませんでした」が 1 件残る。今回の視点/UI assert と設置は全件成功しており、Task 4 の変更起因とは判断していない。
- Unity CLI Loop が失敗後の PlayMode 停止時に接続設定を失うことがあり、Editor 再起動を要した。最終実行はクリーン起動後に成功した。
