# 0052. uGUIは移行済み画面UIの残骸を全削除し、例外4種はuGUIのまま残す

日付: 2026-09-05
状態: 採択

## Context

Web UI（CEF / moorestech_web/webui）への画面UI移行は完了し、`docs/webui/ugui-retirement-plan.md` のPhase 1（実行経路からの切り離し・86ファイルへの未メンテヘッダ付与）まで済んでいる。しかし残骸コード・prefab・MainGameStarterの配線は残り、Webブリッジ（Client.WebUiHost）の Topic/Action と UI状態機械（UIStateControl の各 State）は、恒久非表示の uGUI ビュー（ProgressBarView・BuildMenuView・PlayerInventoryViewController 等）を論理状態の置き場として読み書きしている。

「uGUI関連をすべて消す」の検討で、次の事実が制約になった:
- CEF の描画面は外部パッケージ `jp.juha.cefunity`（READ権限のみ）の `CefUnityBrowserSample` で、RawImage と Canvas 更新順序に依存する。asmdef が `UnityEngine.UI` を参照するため、`com.unity.ugui`（TMP同梱）をmanifestから外すとパッケージがコンパイルできない
- Web ホストと CEF は `InitializeScenePipeline`（GameInitialaizerシーン内）で起動するため、MainMenuとローディング表示はWeb描画できない
- 既存計画の Excluded/Pending 分類のうち、Skit `SelectionButton.prefab` は参照ゼロの孤児、`CutSceneManager.prefab` の Canvas は Timeline から一切バインドされておらず `TimelinePlayer.Play` の呼び出し元も無い（分類に根拠が無かった）

## Decision

- **撤去の定義は「本repoのコード・prefab・シーンから `UnityEngine.UI` / `TMPro` / `EventSystems` への参照をゼロにする」ではなく、「Web UIへ移行済みで描画停止中の画面uGUIを残骸ごと全削除する」。** `com.unity.ugui` はmanifestに残す。
  出所: ユーザー裁定 2026-09-05 原文「uGUI関連をすべて消したいから検討して」→ 選択「パッケージごと完全撤去」→ 原文「それだけは例外」（CEF描画面）→ 復唱確認「合っている」（[[2026-09-05-uGUIはパッケージごと完全撤去する]]、[[2026-09-05-uGUI撤去の唯一の例外はCEF描画面]]）
  棄却案: ①cef-unity上流にuGUI非依存描画経路を追加してPR ②moorestech側でCEF表示コンポーネントを自前実装 ③cef-unityをフォークしてベンダリング

- **uGUIのまま残す例外は次の4種。**
  1. CEF描画面: `MainGameUI.prefab` の Canvas / CanvasScaler / CefUnity子（RawImage）
  2. MainMenuシーンと GameInitialaizer のローディング表示（`Client.MainMenu`、`InitializeScenePipeline` の loadingLog、`TextMeshProLocalize`）。CEFの起動はゲーム内に限定し、ブート最初への前倒しはしない。UI Toolkit化は後日別途
     出所: ユーザー裁定 2026-09-05 原文「CEFの起動はゲーム内だけに限定したい。そもそも将来的にこのゲームは起動毎にpkillが必要になるゲームにする予定なので、ロード画面はさておきメインメニューまでCEF化するとややこしくなる。ロード画面も先にCEFの起動が立ち上がらないと画面を出せないので、ロード、メインメニューはCEF化の対象外とする。とりあえずuGUIの現状維持でOK,UI TK化はあとから考える」（[[2026-09-05-メインメニューとロード画面はuGUI現状維持]]）
     棄却案: ①Web UIへ移行（Webホスト起動を前倒し） ②UI Toolkitで作り直す ③メインメニュー廃止で起動直行
  3. mapObject の HPバー（`MapObjectHpBar.prefab` / `MapObjectHpBarView`、ワールド空間Canvas）
     出所: ユーザー裁定 2026-09-05 原文「現状維持」（[[2026-09-05-mapObjectのHPバーはuGUI現状維持]]）
     棄却案: ①Web UIの画面投影オーバーレイ（WorldPin同型） ②SpriteRenderer+TextMesh ③HPバー廃止
  4. デバッグUI（`DebugObjects.prefab` の DebugSheetController / ItemSelectModal、`TrainUnitDebugOverlayPresenter`）
     出所: ユーザー裁定 2026-09-05 選択「現状維持」（[[2026-09-05-デバッグUIはuGUI現状維持]]）
     棄却案: ①TrainUnitDebugOverlayだけIMGUI化 ②両方をWeb UIのdebug featureへ

- **CutScene は Canvas（BlackOut・InitialText・CanvasScaler・GraphicRaycaster）だけ除去し、`TimelinePlayer` / PlayableDirector / CutSceneCamera / playable / `GameStateController` の購読は残す。** Skit の `SelectionButton.prefab` と `BackgroundSkitUI.cs` は削除する。
  出所: ユーザー裁定 2026-09-05 選択「uGUI Canvas だけ消し、TimelinePlayer は残す」（ユーザーの問い「CutScene Skit SelectionButtonが残す枠になってるのはなぜ？」への調査回答を受けて）（[[2026-09-05-CutSceneはuGUI-Canvasだけ消しTimelinePlayerは残す]]）
  棄却案: ①Client.CutSceneごと全部消す ②現状維持

- **UI状態主権はUnity（UIStateControl）のまま。uGUIビューが抱える論理状態は Client.Game 内の uGUI 非依存モデルクラスへ抽出し、State と Web ブリッジの両方がそれを読み書きする。** 恒久非表示ビューへの `SetActive` は削除、`ProgressBarView.Instance` 型の静的所有は DI 登録へ置換、Web側の topic/action 契約は不変、テスト54ファイルは新モデル型へ移植。
  出所: ユーザー裁定 2026-09-05 選択「Unity側の純ロジッククラスへ抽出（状態主権はUnityのまま）」（[[2026-09-05-uGUIビューの論理状態はUnity側の純ロジッククラスへ抽出する]]）
  棄却案: 状態主権をWeb側へ移譲しUnityはUIStateEnumのミラーだけ持つ

- **PRは2本。** PR1: 論理状態の抽出と型差し替え（挙動不変）。PR2: 残骸コード・prefab・MainGameStarter配線・未参照アセット・監査テスト縮小・docs更新の一括削除。
  出所: ユーザー裁定 2026-09-05 選択「論理モデル抽出を先行PR、その後に削除を1本」（[[2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける]]）
  棄却案: ①1本のPRで全部 ②既存計画どおりPhase 2/3/4の3本

- agent前提（ユーザー裁定ではない）:
  1. `UiPointerHitTest` の `EventSystem.current.IsPointerOverGameObject()` 判定は残す。デバッグUI（uGUI）が残るため、ゲーム内の EventSystem と uGUI 上のポインタ判定は引き続き必要（根拠: 例外4の帰結）
  2. `WebUiGateClassification` / `WebUiGateAuditTest` は削除せず、ScanRoots/Rules を例外4種＋CutScene残部に縮小して「新規スクリーンスペース uGUI の追加禁止」の安全網として維持する（根拠: docs/webui/ugui-retirement-plan.md Phase 4 の既定）
  3. `ItemSelectModal`（デバッグUI）が依存する `CommonSlotView` は Client.DebugSystem 側へ移すか同等品を持たせ、Client.Game/InGame/UI/Inventory/Common は削除する（根拠: 例外4の帰結・AGENTS.md「デバッグ専用publicをプロダクションに残さない」）
  4. `docs/webui/ugui-retirement-plan.md` のスコープ外リストは本ADRの例外4種＋CutScene残部で上書きし、Phase 2〜4 の完了状態を PR2 で記録する
  5. DOTween の `DOTweenModuleUI.cs` と MagicaCloth2 の Example フォルダは外部アセット同梱物であり、パッケージが残る以上コンパイルは通るため触らない

## Consequences

- ゲーム内の画面UIは Web UI 一本になり、`Client.Game/InGame/UI` 配下は UIState 状態機械・論理モデル・ワールド空間表示・チュートリアル誘導だけになる
- Web ブリッジの Topic/Action が uGUI ビュー型を参照しなくなり、Client.Game→Client.WebUiHost の依存方向はそのまま
- `com.unity.ugui` と TMP は残るため、Editor/ビルドサイズ・パッケージ構成は変わらない。cef-unity は無改変
- 例外4種はそれぞれ「Web化」「UI Toolkit化」の余地を残したまま現状維持となる。MainMenu/ローディングの UI Toolkit 化は別ADRで扱う
- 削除対象 prefab / シーンの Missing 参照掃除は Unity Editor 経由（uloop execute-dynamic-code）でのみ行う
