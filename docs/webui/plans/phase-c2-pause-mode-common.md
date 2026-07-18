# Phase C2 実行計画: ポーズメニュー・モード系HUD・共通部品

親: `../MIGRATION.md` / 進捗: `../TODO.md`
旧台帳 FEAT-SYS-1 / MODE-1/2/4/5 / COM-1/4/6/7 相当 + クロスヘア。
モード系は「入力モード + 3D 連動」であり、**判定・3D 描画は Unity 残置、HUD 表示のみ Web**。

## タスク1: ポーズメニュー（FEAT-SYS-1）

- uGUI 参照: `UIState/State/PauseMenuState.cs` + `PauseMenu/PauseMenuStateService.cs` +
  `InGame/Presenter/PauseMenu/`（SaveButton / BackToMainMenu / NetworkDisconnectPresenter）。Esc/M
- Action: セーブ実行 / メインメニュー復帰 / （切断表示は Topic）。セーブ名入力があれば IME（A2）依存
- Web: `src/features/pauseMenu/`。`ui_state.current` の `PauseMenu` でルーティング

## タスク2: モード系 HUD

1. **設置モード（PlaceBlock）**: uGUI 参照 `PlaceBlockState.cs`。HUD 表示（選択中ブロック・高さ・
   キーガイド）+ ホットバー連動。選択状態・不可理由は Topic 配信、モード遷移は既存
   `ui_state` 経路。3D プレビュー・レール接続・列車車両配置の派生は Unity 残置
2. **削除モード（DeleteBar）**: uGUI 参照 `DeleteObjectState.cs` + `DragDelete/`。削除バー HUD +
   ホバー不可理由ツールチップ（判定は Unity、内容を Topic 配信）
3. **給電範囲**: uGUI 参照 `InGame/Electric/DisplayEnergizedRange.cs`。3D 描画は Unity 残置、
   表示 ON/OFF 連携のみ（設置モードと一体で扱う）
4. **直接採掘 HUD**: uGUI 参照 `InGame/Mining/MapObjectMiningController.cs`
   （Idle/Focus/Mining/Complete 状態機械）。フォーカス対象名 + 採掘進捗の HUD を Topic 配信で表示
   ※ワールド追従のピン表示は対象外（ワールド空間 UI は uGUI 維持）。画面固定 HUD のみ

## タスク3: 共通部品

0. **ツールチップ基盤**: uGUI 参照 `InGame/UI/Tooltip/MouseCursorTooltip.cs` + `UGuiTooltipTarget.cs`。
   Web にカーソル追従ツールチップ基盤を新設（クラフト不可理由等の文言表示に必要）。
   **3D オブジェクト由来の表示（旧 WORLD-1）もここで吸収**: ホバー判定（毎フレーム Raycast）は
   Unity 残置のまま、表示 key を Topic 連携して同じツールチップ基盤で表示する
1. **コンテキストメニュー**: uGUI 参照 `InGame/UI/ContextMenu/ContextMenuView.cs` +
   `UGuiContextMenuTarget.cs`。メニュー項目を Topic/Action 化し、Web 汎用 ContextMenu を新設
2. **キー操作ヒント**: uGUI 参照 `InGame/UI/KeyControl/KeyControlDescription.cs`。
   現在 state のキーヒントを Topic 配信（文言は i18n=Phase D と整合させる）
3. **クロスヘア**: uGUI 参照 `InGame/UI/Crosshair/CrosshairView.cs`。GameScreen 中央の常時表示
4. **全 UI 一括非表示（Ctrl+U）**: uGUI 参照 `UIState/UIRoot.cs`（CanvasGroup.alpha トグル）。
   C# 主権でキー検知 → 表示状態 Topic → CEF レイヤ全体を非表示（スクショ用途）
5. **カーソル追従オーバーレイ**: uGUI 参照 `InGame/Control/UICursorFollowControl.cs`。
   Web は grab 表示が既にカーソル追従のため、**残る用途を実コードで棚卸しし、
   Web の grab 表現へ統合できれば個別移植はしない**（要確認から入る）

## 実装順序

ポーズ → 設置/削除 HUD（+給電範囲）→ クロスヘア/キーヒント/全 UI 非表示 → 採掘 HUD →
コンテキストメニュー → カーソル追従（棚卸し）。各タスク完結ごとにコミット。

## 完了条件

- Esc/M でポーズが Web 表示され、セーブ・復帰・切断表示が動く
- 設置/削除/採掘モードで HUD が Web 表示され、3D 側挙動（プレビュー・判定）が従来通り
- Ctrl+U で Web UI 全体が消え、復帰する
- 各 uGUI ビューがゲート化されている

## 検証

vitest / e2e（HUD 表示・ポーズ操作）/ `uloop compile` / PlayMode スモーク（モード遷移一巡）
