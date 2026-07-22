# Task 4 実装レポート: 撤去側の記録フック

## ステータス
完了

## コミット
`f1f77ecb8` feat(client): 撤去バッチのUndo履歴記録

## 変更ファイル
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs`
  - `CommitDelete()` を `void` → `List<IDeleteTarget>` に変更。コミットした対象のスナップショットを取ってから削除処理し、そのリストを返す。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs`
  - `BuildOperationHistory` をctor注入するフィールドを追加。
  - `HandleRelease` 内の直接 `CommitDelete()` 呼び出しを `RecordAndCommitDelete()` ローカル関数経由に変更。
  - `RecordAndCommitDelete()` は `CommitDelete()` の戻り値から `BlockGameObjectChild` のみを抽出し `RemovedBlockInfo`（`OriginalPos`/`BlockId`/`BlockDirection`）のリストを構築、非空なら `RemoveOperationRecord` として `BuildOperationHistory.Push` する。
  - using追加: `Client.Game.InGame.Block`, `Client.Game.InGame.BlockSystem.PlaceSystem.Undo`, `System.Collections.Generic`。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`
  - `_deleteObjectService` フィールドをフィールド初期化子からctor内初期化に変更。
  - ctorに4つ目の引数 `BuildOperationHistory buildOperationHistory` を追加し、`new DeleteObjectService(buildOperationHistory)` として生成。
  - using追加: `Client.Game.InGame.BlockSystem.PlaceSystem.Undo`。
- `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateCameraInteractionTest.cs`
  - `new DeleteObjectState(deleteObject, null, applier)` → `new DeleteObjectState(deleteObject, null, applier, new BuildOperationHistory())`。using追加。
- `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateFocusRestorationTest.cs`
  - 同上の4引数化。using追加。

## brief照合
- Step 1〜4の指示コードをほぼ逐語で反映（brief記載のコードブロックと完全一致）。
- Produces欄の `DeleteObjectState(DeleteBarObject, RailGraphClientCache, IPlayerCameraInteractionApplier, BuildOperationHistory)` 4引数ctorを実装。`BuildUndoService` は本タスクでは追加していない（Task 5予定通り）。
- Task 3で `BuildOperationHistory` はVContainerに登録済みのため、`DeleteObjectState` ctorへのDI解決はコンテナが自動で行う想定（本タスクでは呼び出し側のコンテナ設定を変更していない＝brief通り変更対象外）。
- 既存のDIコンテナからの `DeleteObjectState` 生成箇所以外に `new DeleteObjectState(...)` の呼び出しは無いことを確認済み（テスト2ファイルのみ）。

## コンパイル・テスト結果
- `uloop compile --project-path ./moorestech_client` → `Success: true, ErrorCount: 0, WarningCount: 98`（既存warningのみ、本タスクの変更由来のwarningなし）
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DragDeleteSelection|UIStateCameraInteraction|UIStateFocusRestoration"` → `Success: true, TestCount: 22, PassedCount: 22, FailedCount: 0`

## 自己レビュー（規約チェック）
- **行数**: `DeleteObjectState.cs` 108行、`DragDeleteSelection.cs` 119行、`DeleteObjectService.cs` 185行。全て200行以下。
- **日英2行コメント**: 追加した `RecordAndCommitDelete` 内のコメント、`CommitDelete` の変更コメントはすべて日本語1行→英語1行のセットで記述、それぞれ1行に収まっている。
- **`#region Internal`規約**: `DeleteObjectService.Update()` メソッド内の既存 `#region Internal` ブロックに `RecordAndCommitDelete` ローカル関数を追加した形。クラス直下でのprivateメソッド群の囲みには使っていない。既存の他ローカル関数（`HandleDragStart`等）と同じ配置パターンを踏襲。
- **partial**: 使用なし。
- **デフォルト引数**: 使用なし。ctorへの引数追加は全呼び出し側（本体2ファイル・テスト2ファイル）を変更済み。
- **getter/setter**: 単純プロパティの新規追加なし（既存の `BlockGameObject`/`BlockPosInfo`/`BlockId` は既存コードで `{ get; private set; }` 形式、本タスクでの新規追加ではない）。
- **null チェック**: `BlockGameObject`（`BlockGameObjectChild.BlockGameObject`）はAwake相当の`Init`で設定保証済みのためnullチェック無しでアクセス。設計上問題なし。
- **命名**: `RecordAndCommitDelete`、`removedBlocks`、`blockGameObject` 等、既存の命名規約（意味の通る名前・abbreviation回避）に整合。
- **イベント発火**: 本タスクでは新規イベント発火なし（`BuildOperationHistory.Push` は同期呼び出し、UniRx対象外）。

## 懸念点
- `RemoveOperationRecord` は撤去失敗セルを楽観的に記録し、Undo側（未実装のTask以降）の「空き座標ガード」で無効化される設計。本タスクの範囲では撤去失敗時の記録除外は行っていない（brief通りの意図的な仕様）。
- `DeleteObjectState` への `BuildOperationHistory` 注入はVContainerの自動解決に依存しており、本タスク側でDI登録の確認・追加テストは行っていない（Task 3の責務）。
