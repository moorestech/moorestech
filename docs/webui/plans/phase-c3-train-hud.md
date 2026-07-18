# Phase C3 実行計画: 列車乗車HUD・列車インベントリ

親: `../MIGRATION.md` / 進捗: `../TODO.md`
旧台帳 FEAT-TRAIN-1 / FEAT-INV-5 相当。**依存: A2（乗車入力の入力排他）**、B2（PF）完了推奨。
着手時に writing-plans 形式の詳細計画を作成してから実装する。

## スコープと uGUI 側の正

- **乗車 HUD**: `UIState/State/TrainHUDScreenState.cs` + `TrainHUDScreen/` 配下
  （`TrainHudScreenUIStateController` = GameScreen/PauseMenu の**入れ子状態機械**、
  `TrainRidingInputSender` = W/S 前後・A/D 分岐選択、`TrainBranchRoutePreviewController` =
  分岐ルート 3D プレビュー、`TrainHudGameScreenSubState`）。乗車開始は `RideVehicleInputService`
  （E + 近接判定 → `UIStateEnum.TrainHUDScreen` 遷移要求）。`RidingStateEventPacket` で強制降車
- **列車インベントリ**: `InGame/UI/Inventory/Train/TrainInventoryView.cs`（SubInventory 派生。
  汎用貨車スロット + コンテナ不在等のエラー状態表示）
- **注意**: 現行 uGUI に速度表示・分岐候補リストの画面 HUD は**実在しない**
  （入力送信 + 3D プレビュー + 乗車中ポーズのみ）。速度計等を作るなら新規機能であり本 Phase では作らない

## 実装ステップ

1. **uGUI 実コード確認**: 入れ子状態機械の遷移（乗車中 GameScreen ⇔ 乗車中 Pause）・
   降車条件・分岐選択の入力仕様を確定
2. **Topic**: `train.riding`（乗車状態・強制降車イベント。`RidingStateEventPacket` 中継の3点セット）+
   分岐候補（選択 index 反映用の最小データ）
3. **Action**: 乗車中入力は**キー主権を C# に残す**（`TrainRidingInputSender` 経路を維持）。
   Web は乗車中 HUD の表示・乗車中ポーズ操作のみ担当。分岐選択を Web 操作にする場合は
   選択 index を Action で返し 3D プレビューを駆動（要 uGUI 仕様確認のうえ判断）
4. **ui_state 拡張**: `TrainHUDScreen` state を Web ルーティングに追加。乗車中の
   入れ子 Pause は `ui_state.current` のサブ状態として配信するか要設計
5. **列車インベントリ**: SubInventory 系の既存 Web 実装（blockInventory）に貨車スロット +
   エラー状態表示を追加。`TrainInventoryView` の表示条件を移植
6. **ゲート**: `TrainInventoryView` と TrainHUD 系ビューへ webモードゲート追加

## 完了条件

- E で乗車 → HUD が Web 表示、W/S/A/D 操作・分岐 3D プレビュー・乗車中ポーズ・強制降車が従来通り
- 貨車インベントリが Web で開閉・操作できる
- e2e（HUD 表示・インベントリ）+ PlayMode 実機（乗車一巡）green

## 検証

vitest / e2e / `uloop compile` / PlayMode 実機スモーク（乗車→分岐→降車→貨車開閉）
