# Task 1 実装レポート

## Status

Task 1「UI非依存な視点モードController」の実装と対象テスト更新を完了した。後続タスクが変更する `PlayerViewApplier` と `InGameCameraController` が旧契約を参照しているため、共有ブランチ全体のコンパイルおよびGREEN確認は現時点では未完了。

## 実装内容

- `IPlayerViewApplier` を `SetViewMode(PlayerViewMode mode)` の単一契約へ縮小した。
- `PlayerViewModeController` を `IStartable`, `ITickable` 実装へ変更した。
- 初期モードを三人称として `Start()` でApplierへ同期するようにした。
- `Tick()` でUI状態に依存せずVキー入力を受け、完全な視点モードをApplierへ渡すようにした。
- UIState、テキスト入力フォーカス、右ドラッグ、AimPoint、視点適用ポリシー、三人称距離の責務をControllerから除去した。
- Fakeを完全な `PlayerViewMode` の記録へ変更し、Controller/Inputテストを新契約へ置換した。
- 旧テキストフォーカス仕様テストを削除した。対応 `.meta` はUnity起動後にUnityが削除した。

## TDD証拠

### RED

テストとFakeを先に変更してUnityを起動した。Unityコンパイルログで以下を確認した。

```text
FakePlayerViewApplier.cs(6,42): error CS0535: 'FakePlayerViewApplier' does not implement interface member 'IPlayerViewApplier.SetFirstPersonCamera(bool)'
FakePlayerViewApplier.cs(6,42): error CS0535: 'FakePlayerViewApplier' does not implement interface member 'IPlayerViewApplier.SetCursorVisible(bool)'
FakePlayerViewApplier.cs(6,42): error CS0535: 'FakePlayerViewApplier' does not implement interface member 'IPlayerViewApplier.SetCrosshairVisible(bool)'
FakePlayerViewApplier.cs(6,42): error CS0535: 'FakePlayerViewApplier' does not implement interface member 'IPlayerViewApplier.SetCameraRotatable(bool)'
```

旧インターフェースが新しいFakeと不整合になる、意図したREDだった。

### GREEN確認

Task 1の本体実装後に強制コンパイルを実行した。Task 1で変更したController/Fake/テスト自身のエラーは解消したが、後続タスク対象の旧契約参照によりAssembly全体は未GREEN。

```text
InGameCameraController.cs(38,17): error CS0246: The type or namespace name 'ThirdPersonCameraDistance' could not be found
PlayerViewApplier.cs(11,38): error CS0535: 'PlayerViewApplier' does not implement interface member 'IPlayerViewApplier.SetViewMode(PlayerViewMode)'
```

## 実行コマンドと結果

- `pwd` — `/Users/katsumi/moorestech-worktrees/tree3` を確認。
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerViewModeControllerTest|PlayerViewModeInputTest"` — Unity未起動のため初回は接続失敗。
- `uloop launch ./moorestech_client` — Unity 6000.3.8f1を起動。
- 同対象テスト — REDコンパイル失敗をUnityログで確認。
- 同対象テスト（実装後） — コンパイル不成立のため0件。
- `uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true` — 強制コンパイル完了、結果はログ参照。
- `uloop get-logs --project-path ./moorestech_client --log-type Error` — 後続タスク対象2箇所のエラーを確認。

## 変更ファイル

- `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/IPlayerViewApplier.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/PlayerViewModeController.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/FakePlayerViewApplier.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/PlayerViewModeControllerTest.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/PlayerViewModeInputTest.cs`
- 削除: `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/PlayerViewTextInputFocusTest.cs`
- Unityによる削除: `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/PlayerViewTextInputFocusTest.cs.meta`

## 自己レビュー

- 全変更C#ファイルは200行未満、`partial`・`try-catch`・デフォルト引数を使用していない。
- Controllerの主要処理コメントは日本語・英語の2行セットにした。
- ControllerはUI名前空間を参照せず、VContainerのライフサイクルとモード遷移だけを担当する。
- Fakeは個別副作用ではなく完全なモード値のみを観測する。
- 指定外ファイルは編集していない。

## 懸念

- `PlayerViewApplier` の新契約実装と `ThirdPersonCameraDistance` の移設/置換が入るまで、プロジェクト全体のコンパイルと対象テストGREENは確認できない。
- 共有worktreeで後続タスクが追随変更を完了した時点で、同じ対象テストと全体コンパイルを再実行する必要がある。

## Integration Fix

### 修正内容

- `PlayerViewApplier`を新しい完全モード契約へ追随させ、カメラ距離・自機表示・クロスヘア・照準方式だけを適用するようにした。
- `ThirdPersonCameraDistance`を専用ファイルへ移し、本番コードと既存テストから参照できる状態へ戻した。
- `UIStateControl`から視点Controllerとテキストフォーカス依存を除去し、旧結合テスト2件と`TextInputFocusProvider`を削除した。
- UI遷移単体テストを追加し、視点Controllerなしで遷移できることを検証した。
- GameScreen、BuildMenu、PlaceBlock、DeleteObjectへカーソル・カメラ回転責務を戻した。設置・削除では右ドラッグ中だけ回転する。
- `MainGameStarter`で`PlayerViewModeController`を`IStartable`兼`ITickable`として登録した。

### 実行コマンドと結果

```text
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UIStateControlTest|PlayerViewModeControllerTest|PlayerViewModeInputTest|AimPointProviderTest|ThirdPersonCameraDistanceTest|PlayerObjectModelVisibilityTest|MapObjectMiningAimTest"
Success: true, TestCount: 17, PassedCount: 17, FailedCount: 0, SkippedCount: 0

uloop compile --project-path ./moorestech_client
Success: true, ErrorCount: 0, WarningCount: 0

rg -n "UIStateEnum|TextInputFocusProvider|SetUIState|IsViewState|IsMouseAimState" moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode
該当なし

rg -n "PlayerViewModeController" moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState
該当なし

git diff --check
出力なし
```

### Save/永続化再チェック

Save、Master、ID/GUID、永続化形式には変更を加えていないため、永続化観点への影響はない。
