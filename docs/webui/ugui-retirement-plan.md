# uGUI 物理削除計画 v2（uGUI Retirement Plan）

Web UI（CEF / moorestech_web/webui）への移行完了（docs/webui/MIGRATION.md）を受け、残置 uGUI を物理削除する計画。
v1 の Phase 1（ランタイム恒久抑止・削除予定ヘッダ付与）は実施済み。本 v2 は 2026-08-04 の4系統調査
（コード全量・prefab/シーン・実行時境界・パッケージ依存）の結果で Phase 2 以降を具体化した改訂版。
処遇分類の正は `Client.Tests/WebUi/Gate/WebUiGateClassification.cs`（`WebUiGateAuditTest` が整合を強制）。

## 方針とスコープ

- **削除対象**: uGUI のコード・Prefab・シーン直置き GameObject のすべて（下記「動かせない制約」を除く）
- **画像アセット（Sprite/Texture）は全て残す**（ユーザー指定）。無参照の孤児16枚も削除しない。
  v1 Phase 4 の「未参照スプライト削除」は本改訂で取り下げ

## 動かせない制約（削除不可なもの）

1. **`com.unity.ugui` パッケージは除去不可能**。Unity 6 の builtin パッケージであり、URP core
   （`com.unity.render-pipelines.core`）が硬依存、TMP ランタイムも同パッケージに同居。
   manifest.json から明示エントリを消しても再解決されるだけで意味がない。パッケージ側の作業はゼロ
2. **CEF 描画面**: `MainGameUI.prefab` の Canvas + CanvasScaler + GraphicRaycaster + 子 `CefUnity`
   （RawImage + CefUnityBrowserSample + WebUiCefNavigator）+ `WebUiCefToggle`。Web UI 自体が
   uGUI RawImage に焼かれ、マウス座標変換も RawImage の RectTransform 経由。最後まで残る
3. **MainMenu / GameInitialaizer シーン**: タイトル画面・ローディング表示は Web 未移行
   （webui に対応 feature 無し）で、Web ホスト起動前に動くため uGUI のまま。Phase 5（将来）で対応
4. **ワールド空間 UI**: `MapObjectHpBar.prefab`（Environment 14 prefab から参照）、MapObjectPin、
   採掘進捗、電線コストラベル（3D `TextMeshPro`）等は画面 UI でないため残置確定（分類 Excluded）
5. **TMP フォント資産**: `Dependencies/TextMesh Pro/`（Essentials）と NotoSans-Regular SDF は
   ワールド空間 TextMeshPro（Block 9 prefab + MapVein 4 prefab）が使用するため残す
6. **サードパーティ**: URP / InputSystem / Cinemachine / UniRx / CefUnity 等の uGUI バインドは対象外。
   StarterAssets Mobile・MagicaCloth2 Example の uGUI prefab も別リポ/デモ扱いで対象外

## Phase 2A: 結合点の切り離し（コード削除の前提工事）

削除順ではなく依存順。各項は独立して着手可能だが、2B の一括削除はこれら全ての完了が前提。

1. **`UiPointerHitTest.cs:15` から `EventSystem.current.IsPointerOverGameObject()` を除去**し
   `WebUiInputExclusivity.IsPointerOverWebUi` 単独に畳む。設置・採掘・電線・列車・BP 系 21 箇所が
   自動解決し、MainGame.unity の EventSystem を消せるようになる（CEF の RawImage は
   RaycastTarget=0 で EventSystem を使っていない）。最初の一手
2. **`BackgroundSkitTextCommand.cs:28` のゲート漏れ修正**（唯一 Web モード分岐なしで
   `skitUi.SetText()` を呼ぶスキットコマンド）
3. **Tier B 抽出**: Client.WebUiHost（`WebUiGameBinder.cs` ほか）がデータ源・実行体として掴む
   uGUI クラスを、uGUI 非依存のモデル/サービスへ抽出して参照を差し替える。対象:
   - `MouseCursorTooltip`（TooltipPresentation の ReactiveProperty）→ 純ツールチップサービス
   - `ProgressBarView` / `CrosshairView` / `UIRoot`（Ctrl+U 可視状態）/ `HotBarView`（選択状態）
   - `BuildMenuView` / `BlueprintNameInputView`
   - `SaveButton.Save()` / `BackToMainMenu.Back()` → 非 UI のセッションサービスへ。
     このとき終了時セーブ不動作疑い（moorestech-5rx）も同時に解消する
4. **ブロックインベントリの脱 prefab 化（最重要の隠れ結合）**: Web でブロック UI を開くたび
   `SubInventoryState.cs:116` がマスタの `blockUIAddressablesPath` が指す uGUI prefab を
   Addressables ロード→Instantiate し、`CommonBlockInventoryViewBase.SubInventory`
   （純データ List）を `BlockInventoryTopic.cs:145` が読んでいる。スロット構成情報を prefab でなく
   マスタ/コードから供給する SubInventory データモデルへ置換する。
   **この経路を潰すまで `AddressableResources/UI/Block` の prefab 15 個は削除禁止**
5. **`InitializeScenePipeline` のローディング表示**: `TMP_Text loadingLog` が
   ServerConnectionInitializer / ModAssetLoader / ModAssetIconLoader にコンストラクタで刺さっている。
   シーン自体は残すため必須ではないが、ログ sink 抽象に置換すれば Client.Starter が脱 uGUI 化できる（任意）

## Phase 2B: コード削除（Tier A + 追随整理）

- 完全死コードは即削除可: `Client.Skit/UI/SelectionButton.cs`、無参照 prefab 8 個
  （ChatlogEntry / MissionBar / StoryUI / Recipe viwer selected・unselected tab / HotBar /
  ChallengeListUIElement / SelectionButton）
- Tier A（外部参照なしの葉ビュー）を削除: `UI/Inventory/Block/*`、`UI/Inventory/Craft/*`、
  `UI/Inventory/RecipeViewer/*`、`UI/Challenge/*Element`、`UI/Modal/ModalObject/*`、
  `UI/Inventory/Common/{CommonSlotView,ProgressArrowView}`、`Tooltip/UGuiTooltipTarget`、
  `KeyControl/KeyControlDescription`、`SkitUI/SkitUITools`（UI Toolkit 側・Web スキット完成済み）、
  `Client.DebugSystem/ItemSelectModal.cs`、`Client.Localization/TextMeshProLocalize.cs`
  （+ `Localize.GetLegacy`）、`Client.Game/Common/UIRaycastTarget.cs`
- Tier B 本体（抽出完了後）: HotBarView / BuildMenuView / ProgressBarView / CrosshairView /
  MouseCursorTooltip / BlueprintNameInputView / TrainInventoryView / 各 BlockInventoryView /
  PauseMenu 系 Presenter
- **`MainGameStarter.cs:95-137` の uGUI SerializeField 20 個超と `:282-314` の DI 登録を削除**
  （ビュー削除とロックステップで行う単一の最大編集点）
- **`WebUiGateClassification.cs` の Rules をファイル削除と同一コミットで更新**
  （WebUiGateAuditTest が「存在しないパス参照」も「未分類残置」も失敗させる）
- テスト追随: EditModeInPlayingTest の uGUI prefab 直指定 5 本（MachineRecipeSelection /
  MachineRecipeSelectionGear / MachineModuleSlot / ElectricToGearModeSelect / ChallengeListUI）と
  ビュー型を組む EditMode テスト約 15 ファイルを削除または Web 経路のテストへ書き換え。
  `SkitLocalizationResolverBoundaryTest.cs:190` はリフレクションで TMP を掴むため
  コンパイルが通ってもランタイム NRE になる隠れ地雷

## Phase 2C: UIState 状態機械の脱 uGUI 化（最難関・Phase 2 の最後）

`UIStateControl` に Client.WebUiHost が 7 箇所で型依存（UiStateTopic / BlockInventoryTopic /
TrainRidingTopic / BuildMenuTopic / ResearchTopic / UiStateActions / BuildMenuActions）。
画面ルーティングの正が今も uGUI アセンブリ側にある。`Client.Game/InGame/UI` 125 ファイル中
78 ファイルは uGUI 非依存の純ロジック（UIState 状態機械・LocalPlayerInventory・Equipment 等）なので、
これらを純ロジックアセンブリへ切り出すか、Web 側へ状態主権を移譲する。方式は着手時に設計判断。

## Phase 3: Prefab・シーン GameObject の削除（Unity Editor 経由必須）

すべて `uloop execute-dynamic-code` で行う（YAML 直編集禁止）。SmartAddresser がパス規則で
自動採番しているため、AddressableResources 配下の prefab を消せばアドレスも自動で消える（手作業不要）。

1. 無参照 8 prefab（2B と同時でも可）
2. コード削除に追随: `AddressableResources/UI/Block` 15（※2A-4 完了後）、`AddressableResources/UI`
   の ItemSlotView / FluidSlotView / Modal 2 / Train 1、`Asset/UI/Prefab` 配下の Challenge 6 /
   Inventory 3 / Research 2 / ルート 10、`AddressableResources/Debug/DebugObjects`（要裁定②）
3. **`MainGameUI.prefab` を縮小**: Canvas + CefUnity + WebUiCefToggle（+ UIStateControl は 2C 次第）
   だけ残し、PauseMenu・ChallengeParent 等の uGUI 子ツリーを除去
4. **`MainGame.unity` のシーン編集**: シーン側で Canvas 配下に追加されている CrosshairView /
   BuildMenuView / BlueprintNameInput を除去、EventSystem を除去（2A-1 完了後）
5. **`GameSystem.prefab` を開いて再保存**し、未シリアライズフィールドバグ（moorestech-wkz）を同時解消
6. 非ビルドのテストシーン（Scenes/Other 3 本）の uGUI は必要に応じて掃除（優先度低）

## Phase 4: 仕上げ

- `WebUiGateClassification.cs` の ScanRoots/Rules を縮小し、監査テスト自体の要否を判断
- 任意の削減: DOTween の `DOTweenSettings.asset` uiEnabled を Utility Panel で 0 に、
  `EXCLUDE_UNITY_DEBUG_SHEET` シンボルで DebugSheet をコンパイル除外、
  uGUI 専用フォント `NotoSansJP-Medium SDF 1`（74MB の Dependencies/Font の一部）の削除検討
  （Skit の TMP 利用が残る間は全消し不可）
- docs/webui/MIGRATION.md・disposition.md をクローズ状態に更新
- **画像アセットは触らない**（本計画のスコープ外として明記して終了）

## Phase 5（将来・別判断）: 最終残置の撤去

Web にタイトル画面（サーバー接続 / ローカル起動 / 言語設定 / データリセット / 終了）を実装し、
`Client.MainMenu` 7 ファイル + MainMenu.unity + GameInitialaizer.unity のローディング uGUI を撤去。
`ConnectServer` / `StartLocal` が IP/port/playerId を `InitializeScenePipeline` へ渡す唯一の経路なので、
Web ホストをシーン起動前に立てる構造変更が要る。ここまでやって初めて「アプリ uGUI ゼロ」になる。

## 検証（各フェーズ共通)

- `uloop compile` → `WebUiGateAuditTest` を含む EditMode テスト → プレイテスト DSL
  （unity-playmode-recorded-playtest）で起動〜ブロック設置〜ブロックインベントリ開閉〜セーブを通す
- 特に 2A-4（ブロックインベントリ）と 2A-3（ポーズメニューのセーブ）は Web UI 実操作での回帰確認必須

## 要裁定（着手前にユーザー判断が要るもの)

1. **2C の方式**: UIState 純ロジックの別アセンブリ切り出し vs Web への状態主権移譲
2. **デバッグ UI**（DebugObjects: Graphy / IngameDebugConsole / RuntimeInspector / DebugSheet、全 uGUI）:
   disposition 上は「非出荷」だが実際は全ビルドで DontDestroyOnLoad 生成される。削除 or 残置 or
   コンパイル除外での温存
3. **`blockUIAddressablesPath`**（マスタスキーマのフィールド）: 2A-4 で prefab 参照をやめた後、
   スキーマから削除するか（全 JSON 一括更新が必要）
