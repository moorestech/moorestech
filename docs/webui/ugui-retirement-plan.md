# uGUI 段階的廃止計画（uGUI Retirement Plan）

Web UI（CEF / moorestech_web/webui）への移行完了（docs/webui/MIGRATION.md）を受け、残置された uGUI を4段階で物理削除する計画。
MIGRATION.md 末尾の「物理削除は別判断」の続きにあたる新規フェーズ群。処遇分類の正は `Client.Tests/WebUi/Gate/WebUiGateClassification.cs`。

## スコープ外（uGUI のまま残すもの）— ADR 0052 の例外4種＋CutScene残部

削除の定義と例外は `docs/adr/0052-ugui-removal-scope-and-exceptions.md` が正。

1. **CEF 描画面** — `MainGameUI.prefab` の Canvas / CanvasScaler / GraphicRaycaster / `CefUnity`（RawImage）。外部パッケージ `jp.juha.cefunity` が uGUI Canvas 上で描画するため
2. **MainMenu シーンと GameInitialaizer のローディング表示** — `Client.MainMenu`、`InitializeScenePipeline` の loadingLog、`TextMeshProLocalize`。Web ホスト起動前に動くため。UI Toolkit 化は別 ADR
3. **mapObject の HP バー** — `MapObjectHpBar.prefab` / `MapObjectHpBarView`（ワールド空間 Canvas）
4. **デバッグ UI** — `DebugObjects.prefab` の DebugSheetController / `ItemSelectModal`、`Client.DebugSystem/ItemSlot/*`、`TrainUnitDebugOverlayPresenter`
5. **CutScene 残部** — `TimelinePlayer` / PlayableDirector / CutSceneCamera / playable（Canvas のみ削除済み）

ワールド空間 UI（`InGame/Mining`、`Tutorial/MapObjectPin.cs`、`Tutorial/VeinPin.cs`、`Tutorial/BlockPlacePreviewTutorialManager.cs`、`Tutorial/PlacementGuide`）は画面 UI でないため引き続き Unity 残置（分類 Excluded）。
`Client.Game/InGame/UI/UIState/` は UI 状態主権が Unity のままのため残置（分類 Infra）。ビュー実体は全削除済み。

## Phase 1: 実使用コードから外す（本コミットで実施）

uGUI を実行経路から恒久的に切り離し、未メンテであることをコード上に明示する。物理削除はしない。

- `WebUiScreenGate.IsWebUiMode` を恒久 `true` 化。Ctrl+I トグル（`WebUiCefToggle`）・DebugSheet の CEF スイッチ・ホスト起動失敗時の uGUI フォールバックを全て撤去
  - 副作用: Web ホスト起動失敗時は UI が表示されなくなる（uGUI は未メンテのため復活させない判断）
- 移行済み uGUI ビュー群のファイル先頭に「未メンテ・削除予定」ヘッダコメントを付与
  - Tier A（外部参照なし）: Phase 2 でそのまま削除予定
  - Tier B（Client.WebUiHost・状態機械等から参照中）: 削除前にロジック抽出／参照整理が必要
- ゲート参照（`!WebUiScreenGate.IsWebUiMode`）は 26 箇所そのまま残す — 恒久 false 評価となり全ビューが常時抑止される。`WebUiGateAuditTest` の安全網も維持

## Phase 2〜4: 完了（PR1 #1325 / PR2）

ADR 0052 の裁定により、Phase 2（コード削除）・Phase 3（prefab 削除）・Phase 4（未参照アセット削除）は 2 本の PR で実施した。

- **PR1 #1325（抽出）**: uGUI ビューが抱えていた論理状態を uGUI 非依存モデルへ抽出し、UIState の各 State・`Client.WebUiHost` の Topic/Action・テストを新型へ差し替えた（挙動不変）。`ProgressBarView.Instance` 型の静的所有は DI 登録へ置換
- **PR2（削除）**: 孤児になった .cs・テスト・prefab・シーン内オブジェクト・Addressable 登録・未参照アセットを一括削除し、`WebUiGateClassification` の分類を残置対象へ縮小した
  - `MainGameUI.prefab` の直下は CefUnity / TutorialUI / SkitUI / BacgkroundSkitUI / SaveAndQuitPresenter のみ。`CefUnity` は `WebUiCefToggle`（削除）の代わりに prefab 上で有効化
  - `MainGame.unity` から BuildMenuView / BlueprintNameInput / CrosshairView を削除。EventSystem はデバッグ UI のクリックに要るため残置
  - デバッグ UI が使う `ItemSlotView` / `CommonSlotView` は `Client.DebugSystem/ItemSlot/` へ移設
  - prefab 39件と未参照アセット 35件を削除。Addressable 再ビルドは 501 locations でエラー無し
  - `WebUiGateClassification` は「新規スクリーンスペース uGUI の追加禁止」の安全網として維持（ScanRoots/Rules を縮小）

`com.unity.ugui` と TMP は例外4種のため manifest に残る（ADR 0052）。
