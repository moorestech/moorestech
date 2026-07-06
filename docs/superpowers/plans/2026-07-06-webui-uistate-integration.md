# Web UI 実UI統合（INFRA-6 最小版: UiStateTopic + 遷移Action + 画面ルーティング） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** デモ状態のWeb UI（常時表示インベントリ）を、C#のUIState遷移（Tab=インベントリ開閉、ブロックインタラクト=SubInventory）と同期する「実際のプレイUI」に統合する。

**Architecture:** Ctrl+I の「全面排他トグル」を「webモード」に再定義し、CEF の表示自体を UIState 駆動にする（GameScreen=CEF非表示で通常プレイ、PlayerInventory/SubInventory=CEF表示でWeb画面）。C#は `ui_state.current` topic で現在stateを配信し、Webは `ui_state.request` action で遷移を要求できる。Web側は topic を購読して画面ルーティング（パネル出し分け）する。

**Tech Stack:** Unity C#（Client.Game / Client.WebUiHost、UniTask、Newtonsoft.Json）、React + TypeScript + Mantine v8 + zustand、vitest、Playwright + mock-host（Node ws）。

## Global Constraints

- partial 禁止。1ファイル200行以下。1ディレクトリ10ファイルまで
- コメントは「// 日本語 → // English」の2行セット（各1行厳守）。自明コメント禁止
- 単純getter/setterプロパティ禁止（Setは `SetHoge` メソッド。ただし既存コードの形式は維持）
- `.meta` ファイルの手動作成は絶対禁止（Unity自動生成に任せる。fixture JSON追加後の .meta はUnity起動時に生成されたものをコミット）
- try-catch 基本禁止。null前提コード（外部データ・非同期ロードのみnullチェック）
- デフォルト引数禁止
- C# 変更後は必ず `uloop compile --project-path ./moorestech_client` を実行（ErrorCount 0 を確認）
- 「Unity is reloading (Domain Reload in progress)」エラー時は45秒待機してリトライ
- 各タスク完了時に必ず `git commit`（作業消失防止）
- 作業ディレクトリ: `/Users/katsumi/moorestech-worktrees/tree2`（各タスク冒頭で `pwd` 確認）

## 配置と前例（spec-architecture-review 済み）

| 項目 | 配置先 | 前例 |
|---|---|---|
| `WebUiScreenGate` 拡張（webモード+現在state） | `Client.Game/InGame/UI/UIState/`（既存ファイル書換） | 既存の一方通行 static gate 自体が前例。UIStateEnum と同asmdef |
| `UIStateControl.RequestTransition` | 既存 `UIStateControl.cs` へ追加 | UIState機構の所有者に外部入力口を置く（`WebUiModalService` の Respond と同型） |
| `UiStateTopic` | `Client.WebUiHost/Game/Topics/UiStateTopic.cs` | `ModalTopic.cs`（event購読 + PostLateUpdateデバウンス + snapshot）と同型 |
| `RequestUiStateActionHandler` | `Client.WebUiHost/Game/Actions/UiStateActions.cs` | `ModalActions.cs`（JObject検証 + ActionResult）と同型 |
| Topic/Action 登録 | `WebUiGameBinder.Bind()` へ追加 | 既存7topic/9actionの登録がすべてここ |
| ワイヤ契約 | `WireFixtures/ui_state.json` + `WireContractTest.cs` + `wireContract.test.ts` | 既存 fixture 群（modal/progress/block_inventory）と同運用 |
| Web topic/action 型 | `bridge/protocol.ts` / `payloadTypes.ts` / `validators.ts` | 単一ソース規約（protocol.ts に集約） |
| 画面ルーティング純関数 | `app/uiScreenRouting.ts` + `.test.ts` | `app/activeLayer.ts` + `.test.ts`（app層のロジック+テスト） |
| イベント機構 | C# `event Action`（UniRxでなく） | `UIStateControl.OnStateChanged` が既に event Action で、WebUiHost topic 群は全て event 購読前提 |

**新規パターン（ユーザーレビュー注目点）:**
1. webモード中、Web未実装state（PauseMenu/DeleteBar/PlaceBlock/ChallengeList/ResearchTree/Debug/Story/TrainHUDScreen）への遷移は**抑止**する（キー無反応）。不可視UIへの閉じ込め防止。ポーズメニュー（セーブ）が webモード中は使えない制限が生じる（Ctrl+I で uGUI モードに戻せば使える）
2. webモードの GameScreen では uGUI HUD も CEF も非表示（ホットバーHUD無し）。HUDパリティは INFRA-3（透過オーバーレイ）以降のフォローアップ

**既知の検証限界:** 実機CEFは INFRA-1（バイナリ破損）ブロックのため、本計画の動作保証は uloop compile + NUnit + vitest + Playwright e2e（mock-host）まで。PlayMode実機確認はフォローアップ。

## File Structure

```
C#:
  Client.Game/InGame/UI/UIState/WebUiScreenGate.cs     [書換] webモード+state+IsCefVisible
  Client.Game/InGame/UI/UIState/UIStateControl.cs      [変更] 凍結廃止・web要求消費・遷移抑止・gate公開
  Client.Game/InGame/UI/UIState/WebUiCefToggle.cs      [変更] CEF表示をIsCefVisible駆動へ
  Client.Game/InGame/Control/InGameCameraController.cs [変更] gate参照名変更（1行）
  Client.WebUiHost/Game/Topics/UiStateTopic.cs         [新規] ui_state.current topic
  Client.WebUiHost/Game/Actions/UiStateActions.cs      [新規] ui_state.request action
  Client.WebUiHost/Game/WebUiGameBinder.cs             [変更] 登録2行
  Client.Tests/WebUi/WireFixtures/ui_state.json        [新規] 正準フィクスチャ
  Client.Tests/WebUi/WireContractTest.cs               [変更] テスト1本追加

TS:
  src/bridge/payloadTypes.ts                           [変更] UiStateData
  src/bridge/protocol.ts                               [変更] Topics.uiState + ActionPayloads
  src/bridge/validators.ts                             [変更] validUiState
  src/bridge/wireContract.test.ts                      [変更] fixture テスト追加
  src/app/uiScreenRouting.ts / .test.ts                [新規] state→画面 純関数
  src/app/App.tsx                                      [変更] 画面ルーティング
  src/features/blockInventory/BlockInventoryPanel.tsx  [変更] ✕ボタン（ui_state.request送信）
  e2e/mock-host/fixtures.ts / server.ts                [変更] ui_state topic + /__uistate + action
  e2e/tests/uiState.spec.ts                            [新規] 画面遷移 e2e
```

---

### Task 1: C# — WebUiScreenGate 再設計 + UIState 協調駆動

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/WebUiScreenGate.cs`（全置換）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateControl.cs`（全置換）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/WebUiCefToggle.cs`（部分）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraController.cs:47`

**Interfaces:**
- Produces: `WebUiScreenGate.IsWebUiMode: bool` / `IsCefVisible: bool` / `SetWebUiMode(bool)` / `SetCurrentUiState(UIStateEnum)` / `IsWebSupportedState(UIStateEnum): bool`、`UIStateControl.RequestTransition(UIStateEnum)`（Task 2 が使用）
- 旧 `WebUiScreenGate.IsCefActive` / `SetCefActive` は削除（参照2箇所を本タスクで更新）

- [ ] **Step 1: WebUiScreenGate.cs を全置換**

```csharp
namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// Web UI の表示状態を一方通行で共有する静的ゲート。
    /// IsWebUiMode は WebUiCefToggle のみ、CurrentUiState は UIStateControl のみが書き込む。
    /// One-way static gate sharing the Web UI display status.
    /// Only WebUiCefToggle writes IsWebUiMode; only UIStateControl writes CurrentUiState.
    /// </summary>
    public static class WebUiScreenGate
    {
        // Ctrl+I マスタスイッチ（webモード）
        // Ctrl+I master switch (web mode)
        public static bool IsWebUiMode { get; private set; }

        // UIStateControl が公開する現在のUIState
        // Current UI state published by UIStateControl
        public static UIStateEnum CurrentUiState { get; private set; } = UIStateEnum.GameScreen;

        // CEF を表示すべきか（webモード かつ Web実装済み画面ステート）
        // Whether CEF should be shown (web mode AND a web-implemented screen state)
        public static bool IsCefVisible => IsWebUiMode && IsWebScreenState(CurrentUiState);

        public static void SetWebUiMode(bool active)
        {
            IsWebUiMode = active;
        }

        public static void SetCurrentUiState(UIStateEnum state)
        {
            CurrentUiState = state;
        }

        // webモード中に遷移を許可するstate（GameScreen + Web実装済み画面）
        // States reachable while in web mode (GameScreen + web-implemented screens)
        public static bool IsWebSupportedState(UIStateEnum state)
        {
            return state == UIStateEnum.GameScreen || IsWebScreenState(state);
        }

        // CEF に描画を任せる画面ステート
        // Screen states whose rendering is delegated to CEF
        private static bool IsWebScreenState(UIStateEnum state)
        {
            return state == UIStateEnum.PlayerInventory || state == UIStateEnum.SubInventory;
        }
    }
}
```

- [ ] **Step 2: UIStateControl.cs を全置換**（凍結ロジック廃止 → 協調駆動）

```csharp
using System;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.UIState
{
    public class UIStateControl : MonoBehaviour
    {
        [Inject] private UIStateDictionary _uiStateDictionary;

        public event Action<UIStateEnum> OnStateChanged;
        public UIStateEnum CurrentState { get; private set; }

        private UIStateEnum? _webTransitionRequest;
        private bool _lastWebUiMode;

        public void Initialize(UIStateEnum initialState, UITransitContext initialContext)
        {
            CurrentState = initialState;
            _uiStateDictionary.GetState(CurrentState).OnEnter(initialContext);
            WebUiScreenGate.SetCurrentUiState(CurrentState);
        }

        // Web UI からの遷移要求を受け付ける（次のUpdateで最優先消費）
        // Accept a transition request from the Web UI (consumed first in the next Update)
        public void RequestTransition(UIStateEnum nextState)
        {
            _webTransitionRequest = nextState;
        }

        // UI state
        private void Update()
        {
            // webモード終了の立ち下がりでGameScreenへ正規化しカーソル・カメラを復元する
            // On the web-mode falling edge, normalize to GameScreen to restore cursor/camera
            var webUiMode = WebUiScreenGate.IsWebUiMode;
            if (_lastWebUiMode && !webUiMode)
            {
                _lastWebUiMode = webUiMode;
                _webTransitionRequest = null;
                ForceReturnToGameScreen();
                return;
            }
            _lastWebUiMode = webUiMode;

            // Web要求を最優先で消費し、無ければ現stateの入力判定を使う
            // Consume the web request first; otherwise poll the current state's input
            var nextContext = ConsumeWebRequest() ?? _uiStateDictionary.GetState(CurrentState).GetNextUpdate();
            if (nextContext == null) return;

            // webモード中はWeb未実装stateへの遷移を抑止する（不可視UIへの閉じ込め防止）
            // While in web mode, suppress transitions to web-unimplemented states (avoid invisible-UI traps)
            if (webUiMode && !WebUiScreenGate.IsWebSupportedState(nextContext.NextStateEnum)) return;

            var lastState = CurrentState;
            nextContext.SetLastState(lastState);
            CurrentState = nextContext.NextStateEnum;

            //現在のUIステートを終了し、次のステートを呼び出す
            // Exit current UI state and call next state
            _uiStateDictionary.GetState(lastState).OnExit();
            _uiStateDictionary.GetState(CurrentState).OnEnter(nextContext);

            WebUiScreenGate.SetCurrentUiState(CurrentState);
            OnStateChanged?.Invoke(CurrentState);

            #region Internal

            UITransitContext ConsumeWebRequest()
            {
                if (_webTransitionRequest == null) return null;
                var requested = _webTransitionRequest.Value;
                _webTransitionRequest = null;

                // 同一stateへの要求は遷移不要
                // A request for the current state needs no transition
                if (requested == CurrentState) return null;
                return new UITransitContext(requested);
            }

            #endregion
        }

        private void ForceReturnToGameScreen()
        {
            // GameScreen以外なら終了処理を呼んでパネル等を閉じる
            // If not GameScreen, run its exit to close panels etc.
            var lastState = CurrentState;
            if (lastState != UIStateEnum.GameScreen) _uiStateDictionary.GetState(lastState).OnExit();

            // GameScreenへ再入場しカーソル・カメラ・操作説明を確定させる（同一状態でもカーソル復元のため実行）
            // Re-enter GameScreen to settle cursor/camera/key description (run even for the same state to restore cursor)
            CurrentState = UIStateEnum.GameScreen;
            _uiStateDictionary.GetState(CurrentState).OnEnter(new UITransitContext(UIStateEnum.GameScreen));

            WebUiScreenGate.SetCurrentUiState(CurrentState);
            if (lastState != CurrentState) OnStateChanged?.Invoke(CurrentState);
        }
    }
}
```

- [ ] **Step 3: WebUiCefToggle.cs を変更**（CEF表示をIsCefVisible駆動へ）

以下の3点を Edit で変更する:

(a) `Update()` 内、Ctrl+I ブロックの直後（`return;` の後、デバッグポーリング間引きの前）に挿入:

```csharp
            // CEF表示はUIState駆動（webモード かつ Web実装済み画面のみ表示）
            // CEF visibility is UIState-driven (shown only in web mode AND a web-implemented screen state)
            SyncCefRootVisibility();
```

(b) `Update()` 末尾のカーソル再表明と `ApplyState()` 内の同処理を、条件 `_isCefActive` → `WebUiScreenGate.IsCefVisible` に変更:

```csharp
            // CEF表示中はカーソル解放を再表明する（起動時の初期化順序競合への保険）
            // Re-assert cursor release while CEF is shown (guards against boot init-order races)
            if (WebUiScreenGate.IsCefVisible) InputManager.MouseCursorVisible(true);
```

(c) `ApplyState()` の先頭 `cefUnityRoot.SetActive(_isCefActive);` を削除し、ゲート更新行 `WebUiScreenGate.SetCefActive(_isCefActive);` を以下に置換（順序: SetWebUiMode → Sync）:

```csharp
            // 入力・カーソル調停ゲートを更新する（一方通行: 書き込みはここのみ）
            // Update the input/cursor arbitration gate (one-way: written only here)
            WebUiScreenGate.SetWebUiMode(_isCefActive);
            SyncCefRootVisibility();
            if (WebUiScreenGate.IsCefVisible) InputManager.MouseCursorVisible(true);
```

(d) `OnDestroy()` の `WebUiScreenGate.SetCefActive(false);` → `WebUiScreenGate.SetWebUiMode(false);`

(e) クラスに private メソッドを追加（`ApplyState` の下）:

```csharp
        private void SyncCefRootVisibility()
        {
            // 変化時のみSetActive（毎フレームの無駄なヒエラルキー操作を避ける）
            // SetActive only on change (avoids needless hierarchy churn every frame)
            var visible = WebUiScreenGate.IsCefVisible;
            if (cefUnityRoot.activeSelf != visible) cefUnityRoot.SetActive(visible);
        }
```

- [ ] **Step 4: InGameCameraController.cs:47 を変更**

```csharp
            // CEF表示中はカメラ操作を停止する（静的ゲートを毎フレームポーリング）
            // Stop camera control while the CEF is shown (poll the static gate each frame)
            if (WebUiScreenGate.IsCefVisible) return;
```

- [ ] **Step 5: 旧API参照が残っていないことを確認**

Run: `grep -rn "WebUiScreenGate.IsCefActive\|SetCefActive" moorestech_client/Assets/Scripts --include="*.cs"`
Expected: ヒット 0 件（※ WebUiCefToggle のローカルフィールド `_isCefActive` は残って良い）

- [ ] **Step 6: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

- [ ] **Step 7: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game
git commit -m "feat(webui): CEF表示をUIState駆動に変更しUIState凍結を協調駆動へ置換"
```

---

### Task 2: C# — UiStateTopic + ui_state.request Action + ワイヤ契約

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/UiStateTopic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/UiStateActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/ui_state.json`（.metaは作らない）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs`

**Interfaces:**
- Consumes: `UIStateControl.OnStateChanged: event Action<UIStateEnum>` / `CurrentState` / `RequestTransition(UIStateEnum)`（Task 1）
- Produces: topic `"ui_state.current"` payload `{"state":"<UIStateEnum名>"}`、action `"ui_state.request"` payload `{"state":"GameScreen"|"PlayerInventory"}`（エラー: `invalid_payload` / `invalid_state` / `unsupported_state`）

- [ ] **Step 1: 既存fixtureの書式を確認**

Run: `cat moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/progress_no_label.json`
（インデント・改行の書式を確認し、Step 2 で同じ書式にする）

- [ ] **Step 2: ui_state.json fixture を作成**（書式は Step 1 に合わせる。内容は以下と等価）

```json
{"state":"PlayerInventory"}
```

- [ ] **Step 3: WireContractTest.cs にテスト追加**（既存テスト群の末尾、`AssertMatchesFixture` ヘルパの前）

```csharp
        // ui_state: 列挙名文字列1フィールドの最小契約（INFRA-6）
        // ui_state: the minimal one-field enum-name contract (INFRA-6)
        [Test]
        public void UiStateMatchesFixture()
        {
            AssertMatchesFixture(new UiStateDto { State = "PlayerInventory" }, "ui_state.json");
        }
```

- [ ] **Step 4: コンパイルしてテストが失敗することを確認（UiStateDto未定義）**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `UiStateDto` 未定義のコンパイルエラー

- [ ] **Step 5: UiStateTopic.cs を作成**

```csharp
using System;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// ui_state.current トピック: 現在のUIStateを push（Web側画面ルーティングの正）
    /// ui_state.current topic: pushes the current UI state (source of truth for web-side routing)
    /// </summary>
    public class UiStateTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui_state.current";

        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private bool _publishScheduled;
        private bool _disposed;

        public UiStateTopic(WebSocketHub hub, UIStateControl uiStateControl)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;

            // state遷移を購読して push する
            // Subscribe to state transitions and push them
            _uiStateControl.OnStateChanged += OnStateChanged;
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _uiStateControl.OnStateChanged -= OnStateChanged;
        }

        private void OnStateChanged(UIStateEnum state)
        {
            SchedulePublish();
        }

        // INFRA-7 デバウンス規約: 同フレーム多段遷移でもフレーム末の最終stateだけ配信する
        // INFRA-7 debounce rule: publish only the final state at frame end even on multi-hop transitions
        private void SchedulePublish()
        {
            if (_publishScheduled) return;
            _publishScheduled = true;
            PublishAtEndOfFrame().Forget();
        }

        private async UniTaskVoid PublishAtEndOfFrame()
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            _publishScheduled = false;
            if (_disposed) return;
            _hub.Publish(TopicName, BuildJson());
        }

        private string BuildJson()
        {
            return WebUiJson.Serialize(new UiStateDto { State = _uiStateControl.CurrentState.ToString() });
        }
    }

    /// <summary>
    /// ui_state.current の配信 DTO
    /// Payload DTO for ui_state.current
    /// </summary>
    public class UiStateDto
    {
        public string State;
    }
}
```

- [ ] **Step 6: UiStateActions.cs を作成**

```csharp
using System;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// ui_state.request: Web からのUIState遷移要求を UIStateControl に渡す
    /// ui_state.request: forwards a UI-state transition request from the web to UIStateControl
    /// </summary>
    public class RequestUiStateActionHandler : IActionHandler
    {
        public string ActionType => "ui_state.request";

        private readonly UIStateControl _uiStateControl;

        public RequestUiStateActionHandler(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["state"] is not JValue { Type: JTokenType.String } stateValue) return UniTask.FromResult(ActionResult.Fail("invalid_state"));

            // Webから要求できるのは GameScreen / PlayerInventory のみ（SubInventoryは対象ブロックが必要）
            // The web may request only GameScreen / PlayerInventory (SubInventory needs a target block)
            var stateName = (string)stateValue;
            if (stateName != nameof(UIStateEnum.GameScreen) && stateName != nameof(UIStateEnum.PlayerInventory)) return UniTask.FromResult(ActionResult.Fail("unsupported_state"));

            _uiStateControl.RequestTransition(Enum.Parse<UIStateEnum>(stateName));
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
```

- [ ] **Step 7: WebUiGameBinder.cs に登録追加**

ブロックインベントリトピック登録の直後に:

```csharp
            // UIステートトピックを登録（Web側画面ルーティングの正）
            // Register the UI-state topic (source of truth for web-side routing)
            var uiStateTopic = new UiStateTopic(hub, uiStateControl);
            hub.RegisterTopic(UiStateTopic.TopicName, uiStateTopic);
```

action登録ブロックの末尾（`BlockMoveItemActionHandler` の次行）に:

```csharp
            hub.RegisterAction(new RequestUiStateActionHandler(uiStateControl));
```

- [ ] **Step 8: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

- [ ] **Step 9: NUnitテスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractTest"`
Expected: 全 PASS（UiStateMatchesFixture 含む）

- [ ] **Step 10: Commit**（Unityが生成した ui_state.json.meta があれば一緒に）

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/WebUi
git commit -m "feat(webui): ui_state.current topic と ui_state.request action を追加"
```

---

### Task 3: TS — ワイヤ契約 + 画面ルーティング純関数

**Files:**
- Modify: `moorestech_web/webui/src/bridge/payloadTypes.ts`
- Modify: `moorestech_web/webui/src/bridge/protocol.ts`
- Modify: `moorestech_web/webui/src/bridge/validators.ts`
- Modify: `moorestech_web/webui/src/bridge/wireContract.test.ts`
- Create: `moorestech_web/webui/src/app/uiScreenRouting.ts`
- Create: `moorestech_web/webui/src/app/uiScreenRouting.test.ts`

**Interfaces:**
- Consumes: Task 2 の fixture `WireFixtures/ui_state.json`
- Produces: `Topics.uiState`（= `"ui_state.current"`）、`UiStateData = { state: string }`、`ActionPayloads["ui_state.request"] = { state: "GameScreen" | "PlayerInventory" }`、`screenForUiState(state: string | null): UiScreen`（`UiScreen = "none" | "playerInventory" | "subInventory"`）

- [ ] **Step 1: uiScreenRouting.test.ts を作成（失敗するテスト）**

```typescript
import { describe, it, expect } from "vitest";
import { screenForUiState } from "./uiScreenRouting";

describe("screenForUiState", () => {
  it("PlayerInventory はインベントリ画面", () => {
    expect(screenForUiState("PlayerInventory")).toBe("playerInventory");
  });
  it("SubInventory はブロック画面", () => {
    expect(screenForUiState("SubInventory")).toBe("subInventory");
  });
  it("GameScreen・未受信・未知state はパネル無し", () => {
    expect(screenForUiState("GameScreen")).toBe("none");
    expect(screenForUiState(null)).toBe("none");
    expect(screenForUiState("PauseMenu")).toBe("none");
  });
});
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `cd moorestech_web/webui && npx vitest run src/app/uiScreenRouting.test.ts`
Expected: FAIL（モジュール不存在）

- [ ] **Step 3: uiScreenRouting.ts を実装**

```typescript
// ui_state.current の state 名 → Web が描画する画面。App.tsx ルーティングの単一の正
// Maps ui_state.current's state name to the web screen; single source for App.tsx routing
export type UiScreen = "none" | "playerInventory" | "subInventory";

export function screenForUiState(state: string | null): UiScreen {
  if (state === "PlayerInventory") return "playerInventory";
  if (state === "SubInventory") return "subInventory";
  // GameScreen・未対応state・未受信はパネル無し（前方互換: 未知state名も安全側に倒す)
  // GameScreen, unsupported states and pre-snapshot are panel-less (forward-compat: unknown names fail safe)
  return "none";
}
```

- [ ] **Step 4: payloadTypes.ts に追加**（`ProgressData` の直後）

```typescript
// INFRA-6 最小版: C# UIStateEnum の現在値。未知のstate名も受理し画面ルータが安全側に倒す
// Minimal INFRA-6: current C# UIStateEnum value; unknown names are accepted and the router fails safe
export type UiStateData = { state: string };
```

- [ ] **Step 5: protocol.ts に追加**（3箇所）

import 型リストに `UiStateData` を追加。`Topics` に:

```typescript
  uiState: "ui_state.current",
```

`TopicPayloads` に:

```typescript
  [Topics.uiState]: UiStateData;
```

`ActionPayloads` に:

```typescript
  "ui_state.request": { state: "GameScreen" | "PlayerInventory" };
```

- [ ] **Step 6: validators.ts に追加**

```typescript
// state は列挙名文字列。値の解釈（既知/未知）はルータ側の責務
// state is an enum-name string; interpreting known/unknown values is the router's job
function validUiState(d: unknown): boolean {
  return isObject(d) && isString(d.state);
}
```

validators レコードに `[Topics.uiState]: validUiState,` を追加。

- [ ] **Step 7: wireContract.test.ts にテスト追加**（import 型リストに `UiStateData` を追加）

```typescript
  it("ui_state が受理され型消費できる", () => {
    const data = loadFixture("ui_state.json");
    expect(validateTopicPayload(Topics.uiState, data)).toBe(true);
    expect((data as UiStateData).state).toBe("PlayerInventory");
  });
```

- [ ] **Step 8: 全 vitest 実行**

Run: `cd moorestech_web/webui && npm test`
Expected: 全 PASS

- [ ] **Step 9: Commit**

```bash
git add moorestech_web/webui/src
git commit -m "feat(webui): ui_state ワイヤ契約と画面ルーティング純関数を追加"
```

---

### Task 4: TS — App.tsx 画面ルーティング + ブロックパネル閉じるボタン

**Files:**
- Modify: `moorestech_web/webui/src/app/App.tsx`
- Modify: `moorestech_web/webui/src/features/blockInventory/BlockInventoryPanel.tsx`

**Interfaces:**
- Consumes: `screenForUiState` / `Topics.uiState` / `dispatchAction`（`@/bridge` から export 済み）
- Produces: `data-testid="block-inventory-close"`（Task 5 の e2e が使用）。画面表示規則: `none`=パネル無し、`playerInventory`=インベントリ+レシピ+アイテムリスト、`subInventory`=インベントリのみ（ブロックパネルは blockInventory topic の open が正）

- [ ] **Step 1: App.tsx のルーティング化**

import に追加: `useTopicSelector, Topics` を `@/bridge` から、`screenForUiState` を `./uiScreenRouting` から。

コンポーネント冒頭の `disconnected` の直後に:

```typescript
  // ui_state.current による画面ルーティング（C# UIStateControl が正。セレクタはプリミティブを返す）
  // Screen routing by ui_state.current (C# UIStateControl is authoritative; the selector returns a primitive)
  const screen = useTopicSelector(Topics.uiState, (d) => screenForUiState(d?.state ?? null));
```

JSX を以下の出し分けに変更（ヘッダ `Group`、`InventoryPanel`、`RecipeViewer`、`ItemListPanel` の4要素。BlockInventoryPanel/ModalHost/ProgressBar/ToastHost/再接続オーバーレイは無条件のまま）:

```tsx
      {screen !== "none" && (
        <Group gap="md" style={{ gridArea: "header" }}>
          <Title order={1} size="h3">moorestech Web UI</Title>
          {DebugActionButton ? (
            <Suspense fallback={null}>
              <DebugActionButton />
            </Suspense>
          ) : null}
        </Group>
      )}
      {screen !== "none" && <InventoryPanel />}
      {screen === "playerInventory" && <RecipeViewer />}
      {screen === "playerInventory" && <ItemListPanel />}
```

- [ ] **Step 2: BlockInventoryPanel.tsx に閉じるボタン追加**

import を変更: `import { CloseButton, Group, Paper, Title } from "@mantine/core";`、`dispatchAction` を `@/bridge` の import に追加。

`<Title order={2} size="h4" mb="sm">{data.blockName}</Title>` を以下に置換:

```tsx
      <Group justify="space-between" mb="sm">
        <Title order={2} size="h4">{data.blockName}</Title>
        {/* uGUIのEsc/Tab相当のマウス閉じ操作。GameScreenへの遷移をhostへ要求する */}
        {/* Mouse-driven close, like uGUI Esc/Tab; asks the host to transit to GameScreen */}
        <CloseButton
          data-testid="block-inventory-close"
          aria-label="close"
          onClick={() => {
            void dispatchAction("ui_state.request", { state: "GameScreen" });
          }}
        />
      </Group>
```

- [ ] **Step 3: 型チェック + 全 vitest**

Run: `cd moorestech_web/webui && npx tsc -b && npm test`
Expected: エラー 0、全 PASS

- [ ] **Step 4: Commit**

```bash
git add moorestech_web/webui/src
git commit -m "feat(webui): App を ui_state 駆動の画面ルーティングに変更しブロックパネルに閉じる操作を追加"
```

---

### Task 5: mock-host 拡張 + 画面遷移 e2e

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/server.ts`
- Create: `moorestech_web/webui/e2e/tests/uiState.spec.ts`

**Interfaces:**
- Consumes: `Topics.uiState` / `UiStateData` / `ActionPayloads["ui_state.request"]`（Task 3）、`data-testid="block-inventory-close"`（Task 4）
- Produces: mock-host の `/__uistate?state=<名前>` テスト用エンドポイント（既定 state は `PlayerInventory` — 既存 e2e の表示前提を維持）

- [ ] **Step 1: fixtures.ts に追加**（import 型リストに `UiStateData` を追加）

```typescript
// INFRA-6: 既定はインベントリ画面（既存 e2e が前提とする表示状態を保つ）
// INFRA-6: default to the inventory screen (keeps the visibility existing e2e tests assume)
export const uiState = { state: "PlayerInventory" } satisfies UiStateData;
```

- [ ] **Step 2: server.ts に ui_state 対応を追加**（5箇所）

(a) import 型リストに `UiStateData` を追加。

(b) グローバル状態（`currentModal` 宣言の直後）:

```typescript
// ui_state は既定でインベントリ画面。/__uistate?state=X で切替（既存 e2e の表示前提を守る既定値）
// ui_state defaults to the inventory screen; switch via /__uistate?state=X (default keeps existing e2e assumptions)
let currentUiState: UiStateData = clone(fx.uiState);
const uiStateSubscribers = new Set<WebSocket>();
```

(c) HTTPエンドポイント（`/__modal` ブロックの直後）:

```typescript
  // テスト用: ui_state を差し替えて購読者へ event push
  // Test-only: swap the served ui_state and push an event to subscribers
  if (url.startsWith("/__uistate")) {
    const state = new URL(url, "http://x").searchParams.get("state") ?? "PlayerInventory";
    currentUiState = { state };
    for (const ws of uiStateSubscribers) send(ws, { op: "event", topic: Topics.uiState, data: currentUiState });
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify({ ok: true }));
    return;
  }
```

(d) `KNOWN_ACTIONS` に `"ui_state.request",` を追加。

(e) `topicData` に `if (topic === Topics.uiState) return currentUiState;` を追加。subscribe ハンドラに `if (topic === Topics.uiState) uiStateSubscribers.add(ws);`、unsubscribe に `if (topic === Topics.uiState) uiStateSubscribers.delete(ws);`、`ws.on("close")` に `uiStateSubscribers.delete(ws);` を追加。

(f) action 分岐（`block_inventory.move_item` の else-if の直後）:

```typescript
      } else if (msg.type === "ui_state.request") {
        // 実 host の許可制を再現: GameScreen/PlayerInventory のみ受理し、GameScreen 遷移では block も閉じる
        // Mirror the real host's allowlist: accept only GameScreen/PlayerInventory; GameScreen also closes the block
        const state = (msg.payload as ActionPayloads["ui_state.request"]).state;
        if (state !== "GameScreen" && state !== "PlayerInventory") {
          error = "unsupported_state";
        } else {
          currentUiState = { state };
          if (state === "GameScreen") currentBlock = clone(fx.blockClosed);
          setTimeout(() => {
            for (const sub of uiStateSubscribers) send(sub, { op: "event", topic: Topics.uiState, data: currentUiState });
            if (state === "GameScreen") {
              for (const sub of blockSubscribers) send(sub, { op: "event", topic: Topics.blockInventory, data: currentBlock });
            }
          }, 30);
        }
      }
```

- [ ] **Step 3: uiState.spec.ts を作成**

※ RecipeViewer 非表示の検証セレクタは、`e2e/tests/recipe.spec.ts` を Read して既存の RecipeViewer 表示検証と同じセレクタを流用すること（見出しテキスト or testid）。以下の `RECIPE_LOCATOR` コメント箇所を差し替える。

```typescript
import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

// 各テスト後に既定状態へ戻し、他 spec へ画面状態を漏らさない
// Reset to defaults after each test so screen state never leaks into other specs
test.afterEach(async ({ page }) => {
  await page.request.get("/__uistate?state=PlayerInventory");
  await page.request.get("/__block?type=closed");
});

test("既定(PlayerInventory)でインベントリ画面が表示される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
});

test("GameScreen でパネルが消え、PlayerInventory への event で再表示される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();

  await page.request.get("/__uistate?state=GameScreen");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeHidden();

  await page.request.get("/__uistate?state=PlayerInventory");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
});

test("SubInventory でインベントリ+ブロックパネルが出てレシピビューアは消える", async ({ page }) => {
  await page.request.get("/__block?type=chest");
  await page.request.get("/__uistate?state=SubInventory");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  // RECIPE_LOCATOR: recipe.spec.ts と同一セレクタで RecipeViewer の非表示を検証する
  // RECIPE_LOCATOR: assert the RecipeViewer is hidden using the same selector as recipe.spec.ts
});

test("ブロックパネルの✕で ui_state.request(GameScreen) を送り画面が閉じる", async ({ page }) => {
  await page.request.get("/__block?type=chest");
  await page.request.get("/__uistate?state=SubInventory");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();

  await page.getByTestId("block-inventory-close").click();

  // action 送信契約を /__actions で検証する
  // Verify the send contract via /__actions
  await expect
    .poll(async () => {
      const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
      return actions.some((a) => a.type === "ui_state.request" && (a.payload as { state?: string }).state === "GameScreen");
    })
    .toBe(true);

  // mock が ui_state/block event を返し、インベントリ画面とブロックパネルが閉じる
  // The mock pushes ui_state/block events back, closing the inventory screen and block panel
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeHidden();
  await expect(page.getByTestId("block-inventory")).toBeHidden();
});
```

- [ ] **Step 4: e2e 全実行（既存 spec の回帰確認込み）**

Run: `cd moorestech_web/webui && npm run test:e2e`
Expected: 全 PASS（既存 7 spec + uiState.spec）

- [ ] **Step 5: Commit**

```bash
git add moorestech_web/webui/e2e
git commit -m "test(webui): mock-host に ui_state を追加し画面遷移 e2e を新設"
```

---

### Task 6: ドキュメント更新 + 最終QA

**Files:**
- Modify: `docs/webui/TODO.md`

- [ ] **Step 1: TODO.md 更新**

「現状スナップショット」の基盤節に以下を追記し、「残タスク §1」の INFRA-6 行を「🟡最小版済」に更新する:

```markdown
- **INFRA-6 最小版（2026-07-06）**: `ui_state.current` topic + `ui_state.request` action で UIState⇔Web を橋渡し。CEF表示はUIState駆動（GameScreen=非表示で通常プレイ、PlayerInventory/SubInventory=CEF表示）。App.tsx は state で画面をルーティング。**webモード中の未対応state遷移は抑止**（PauseMenu等はCtrl+IでuGUIモードへ）。GameStateType（第2状態機械）のTopic化は未着手
```

「§3 検証」に追記:

```markdown
- [ ] webモードの実機遷移確認（Tab開閉・ブロックインタラクト・✕ボタン。INFRA-1 解消後 or PlayMode録画）
```

- [ ] **Step 2: 最終QA（全系統）**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractTest"
cd moorestech_web/webui && npx tsc -b && npm test && npm run test:e2e
```

Expected: すべて成功（ErrorCount 0 / NUnit 全PASS / vitest 全PASS / e2e 全PASS）

- [ ] **Step 3: Commit**

```bash
git add docs/webui/TODO.md
git commit -m "docs(webui): INFRA-6 最小版の到達点と検証残を TODO に反映"
```
