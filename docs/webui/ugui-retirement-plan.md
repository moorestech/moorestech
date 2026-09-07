# uGUI 段階的廃止計画（uGUI Retirement Plan）

Web UI（CEF / moorestech_web/webui）への移行完了（docs/webui/MIGRATION.md）を受け、残置された uGUI を4段階で物理削除する計画。
MIGRATION.md 末尾の「物理削除は別判断」の続きにあたる新規フェーズ群。処遇分類の正は `Client.Tests/WebUi/Gate/WebUiGateClassification.cs`。

## スコープ外（削除しないもの）

- `Client.Game/InGame/UI/UIState/`（UIStateControl・状態機械・ゲート本体）— Web UI ブリッジ（Client.WebUiHost）が状態主権として7箇所で型依存。Web 側へ状態機械を移すまで維持
- ワールド空間 UI（`InGame/Mining`、`Tutorial/MapObjectPin.cs`、`Tutorial/BlockPlacePreviewTutorialManager.cs`、`Tutorial/TutorialBlock`、`MapObjectHpBarView` 等）— 画面 UI でないため Unity 残置確定（分類 Excluded）
- `Client.MainMenu` / `GameInitialaizer` シーンのローディング表示 — Web ホスト起動前に動くため当面 uGUI のまま
- `MainGameUI.prefab` の Canvas と `CefUnity` 子オブジェクト — CEF の描画面（RawImage）自体が uGUI Canvas 上に乗っているため、Canvas/RawImage インフラは最後まで残る
- `Client.CutScene` — 移行 Phase C4 待ち（分類 Pending）
- デバッグ UI・エディタツール

## Phase 1: 実使用コードから外す（本コミットで実施）

uGUI を実行経路から恒久的に切り離し、未メンテであることをコード上に明示する。物理削除はしない。

- `WebUiScreenGate.IsWebUiMode` を恒久 `true` 化。Ctrl+I トグル（`WebUiCefToggle`）・DebugSheet の CEF スイッチ・ホスト起動失敗時の uGUI フォールバックを全て撤去
  - 副作用: Web ホスト起動失敗時は UI が表示されなくなる（uGUI は未メンテのため復活させない判断）
- 移行済み uGUI ビュー群のファイル先頭に「未メンテ・削除予定」ヘッダコメントを付与
  - Tier A（外部参照なし）: Phase 2 でそのまま削除予定
  - Tier B（Client.WebUiHost・状態機械等から参照中）: 削除前にロジック抽出／参照整理が必要
- ゲート参照（`!WebUiScreenGate.IsWebUiMode`）は 26 箇所そのまま残す — 恒久 false 評価となり全ビューが常時抑止される。`WebUiGateAuditTest` の安全網も維持

## Phase 2: コードだけ消す

PR1（抽出）完了: Tier B の論理状態抽出・State/WebUiHost/テスト差し替え・Phase1 ヘッダ整理まで完了。以降は本フェーズの Tier A 削除から着手する。

- Tier A ファイルを削除。`WebUiGateClassification.cs` の Rules を同時更新（監査テストが整合を強制）
- Tier B は先に依存を外す: Client.WebUiHost の Topics/Actions が uGUI ビュー（HotBarView・BuildMenuView・ProgressBarView・ItemListView 等）をデータ源にしている箇所を、uGUI 非依存のモデル/サービスクラスへ抽出してから削除
- 最難関は `UIStateControl` の uGUI 非依存化（UIState ディレクトリを純ロジックアセンブリへ移す or Web 側へ状態主権を移譲）— これは Phase 2 の最後
- 削除順の目安（葉から）: `UI/Inventory/Block` → `UI/Inventory/RecipeViewer` → `UI/Inventory/Craft` → `UI/Challenge` → `UI/Modal` → ルートビュー群
- `MainGameStarter` の uGUI SerializeField（20個以上）と DI 登録を削除

## Phase 3: Prefab を消す

- コード削除で参照が切れた Prefab を削除（Unity Editor 経由 / uloop execute-dynamic-code）
  - `Assets/AddressableResources/UI/Block`（15個）、`Assets/Asset/UI/Prefab` 配下（Challenge/Inventory/Research 含む）、`AddressableResources/UI/Modal` 等
- `MainGameUI.prefab` は削除せず、uGUI 専用の子オブジェクト（PauseMenu・ChallengeParent 等）を除去して CefUnity ルート中心に縮小
- シーン（MainGame/GameInitialaizer）の Missing 参照を掃除。SmartAddresser のアドレス欠落に注意

## Phase 4: 未参照 uGUI アセットを消す

- 未参照になったスプライト・フォント・アトラス・アニメーション等を検出して削除（エディタの依存検索 or アドレス参照解析）
- `WebUiGateClassification.cs` の ScanRoots/Rules を縮小し、最終的に監査テスト自体の要否を判断
- docs/webui/MIGRATION.md・disposition.md をクローズ状態に更新
