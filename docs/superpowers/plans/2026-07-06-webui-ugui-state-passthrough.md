# Web UI: uGUIステートマシン活用への転換（抑止撤廃・CEF常時表示・ビュー差し替え型） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 前計画（2026-07-06-webui-uistate-integration.md、実装済み）の「独自機構」3点を撤廃し、既存uGUIステートマシンを唯一の状態源としてそのまま動かす。CEFはwebモード中**常時表示の透明オーバーレイ**とし、Webが置換済みのuGUIビュー（インベントリ画面）だけを非表示ゲートする。

**Architecture:** UIStateControl は無傷でフル稼働（B/G/T/R/Esc/F3等すべて従来通り）。`ui_state.current` topic（実装済み）が遷移をWebへ中継し、Webは PlayerInventory/SubInventory のみ描画、他stateは透明（uGUIがそのまま見える）。ホットバー等のuGUI HUDは隠さない。二重表示は `PlayerInventoryViewController.SetActive` / `RecipeViewerView.SetActive` のwebモードゲートで防ぐ。

**Tech Stack:** 前計画と同じ + uloop execute-dynamic-code（prefab変更の正規ルート）。

## Global Constraints

- partial 禁止 / try-catch 基本禁止 / .meta 手動作成禁止 / デフォルト引数禁止
- コメントは「// 日本語 → // English」2行セット（各1行）
- Prefab/シーンのテキスト直接編集禁止。変更は uloop execute-dynamic-code 経由のみ
- C# 変更後は必ず `uloop compile --project-path ./moorestech_client`（ErrorCount 0）
- Domain Reload エラー時は45秒待機してリトライ
- 各タスク完了時に必ず git commit
- 作業ディレクトリ: /Users/katsumi/moorestech-worktrees/tree2（タスク冒頭で pwd 確認）

## 配置と前例

| 項目 | 配置 | 前例/根拠 |
|---|---|---|
| ビューのwebモードゲート | `PlayerInventoryViewController.SetActive` / `RecipeViewerView.SetActive` 内（各1行） | 両stateが必ず通るチョークポイント（PlayerInventoryState.cs:52-53 / SubInventoryState.cs:137,169 / SkitState.cs:28）。false側は素通しなので閉じ漏れなし |
| WebUiScreenGate 縮小 | IsWebUiMode のみ残す | CurrentUiState/IsCefVisible/IsWebSupportedState の用途（CEF出し分け・遷移抑止・カメラゲート）が全て消えるため |
| バックドロップ | App.tsx 内 fixed 全面 div | 旧 index.css の body 0.6 dim の移設（GameScreen で dim しないため画面ルート単位へ） |

**維持するもの（前計画の成果）:** `UiStateTopic` / `RequestUiStateActionHandler` / `UIStateControl.RequestTransition` / Web側ルーティング（protocol/validators/uiScreenRouting/App出し分け/✕ボタン）/ mock-host / e2e。

**既知の制限（フォローアップ=INFRA-2）:** raycastTarget=0 化により、Web UIパネル上のクリックがuGUI/3Dワールドにも届き得る（現状の実害: Webインベントリ画面のホットバー行とuGUIホットバーの重なり領域での二重選択程度。世界クリック処理は GameScreenState のみなのでインベントリ画面中の誤採掘等は起きない）。

---

### Task 1: C# — 独自機構の撤廃とビューゲート

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/WebUiScreenGate.cs`（全置換）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateControl.cs`（全置換）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/WebUiCefToggle.cs`（部分）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraController.cs`（ゲート行削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/PlayerInventoryViewController.cs:379-382`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeViewerView.cs:92-95`

**Interfaces:**
- Produces: `WebUiScreenGate.IsWebUiMode` / `SetWebUiMode(bool)`（これのみ。CurrentUiState/IsCefVisible/IsWebSupportedState/SetCurrentUiState は削除）
- 維持: `UIStateControl.RequestTransition(UIStateEnum)` / `OnStateChanged` / `CurrentState`（UiStateTopic/Action が使用中）

- [ ] **Step 1: WebUiScreenGate.cs を全置換**

```csharp
namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// Web UI（CEF）モードかどうかを一方通行で共有する静的ゲート。書き込みは WebUiCefToggle のみ。
    /// One-way static gate telling whether Web UI (CEF) mode is on; written only by WebUiCefToggle.
    /// 状態遷移は uGUI の UIStateControl が唯一の正で、本ゲートは置換済みビューの表示抑止にだけ使う。
    /// The uGUI UIStateControl remains the sole state authority; this gate only suppresses replaced views.
    /// </summary>
    public static class WebUiScreenGate
    {
        public static bool IsWebUiMode { get; private set; }

        public static void SetWebUiMode(bool active)
        {
            IsWebUiMode = active;
        }
    }
}
```

- [ ] **Step 2: UIStateControl.cs を全置換**（抑止撤廃・両エッジ正規化）

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
            // webモード切替の両エッジでGameScreenへ正規化する（uGUI/Webビューの表示不整合を防ぐ）
            // Normalize to GameScreen on both web-mode edges (prevents uGUI/web view visibility mismatch)
            var webUiMode = WebUiScreenGate.IsWebUiMode;
            if (webUiMode != _lastWebUiMode)
            {
                _lastWebUiMode = webUiMode;
                _webTransitionRequest = null;
                ForceReturnToGameScreen();
                return;
            }

            // Web要求を最優先で消費し、無ければ現stateの入力判定を使う
            // Consume the web request first; otherwise poll the current state's input
            var nextContext = ConsumeWebRequest() ?? _uiStateDictionary.GetState(CurrentState).GetNextUpdate();
            if (nextContext == null) return;

            var lastState = CurrentState;
            nextContext.SetLastState(lastState);
            CurrentState = nextContext.NextStateEnum;

            //現在のUIステートを終了し、次のステートを呼び出す
            // Exit current UI state and call next state
            _uiStateDictionary.GetState(lastState).OnExit();
            _uiStateDictionary.GetState(CurrentState).OnEnter(nextContext);

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

            if (lastState != CurrentState) OnStateChanged?.Invoke(CurrentState);
        }
    }
}
```

- [ ] **Step 3: WebUiCefToggle.cs を変更**（CEF常時表示・uGUI隠し撤廃）

(a) `Update()` から `SyncCefRootVisibility();` の呼び出し行（コメント2行含む）と、末尾の「CEF表示中はカーソル解放を再表明する」ブロック（コメント2行 + if 行）を削除。

(b) `ApplyState()` を以下に全置換（uGUIスナップショット機構を撤廃）:

```csharp
        private void ApplyState()
        {
            // webモード中はCEFルートを常時表示する（透明オーバーレイ。uGUIは隠さず共存）
            // While in web mode the CEF root stays always visible (transparent overlay; uGUI coexists unhidden)
            cefUnityRoot.SetActive(_isCefActive);

            // 入力・カーソル調停ゲートを更新する（一方通行: 書き込みはここのみ）
            // Update the input/cursor arbitration gate (one-way: written only here)
            WebUiScreenGate.SetWebUiMode(_isCefActive);

            _appliedCefActive = _isCefActive;
        }
```

(c) 不要になったフィールド `_uguiRoots` / `_uguiRootActiveSnapshot` / `_hasAppliedOnce` と、`Awake()` の子収集ループ、`SyncCefRootVisibility()` メソッドを削除。`Awake()` が空になる場合はメソッドごと削除。

(d) `OnDestroy()` はそのまま（`if (_appliedCefActive) WebUiScreenGate.SetWebUiMode(false);`）。

- [ ] **Step 4: InGameCameraController.cs のゲート行を削除**

`Update()` 冒頭の以下4行（コメント2行 + if 行 + 空行）を削除し、従来動作（`SetControllable` のみでの制御）に戻す:

```csharp
            // CEF表示中はカメラ操作を停止する（静的ゲートを毎フレームポーリング）
            // Stop camera control while the CEF is shown (poll the static gate each frame)
            if (WebUiScreenGate.IsCefVisible) return;
```

あわせて不要になった `using Client.Game.InGame.UI.UIState;` があれば削除（他で未使用の場合のみ）。

- [ ] **Step 5: PlayerInventoryViewController.SetActive をゲート**（`using Client.Game.InGame.UI.UIState;` を追加）

```csharp
        public void SetActive(bool isActive)
        {
            // webモード中はWeb側が同画面を描画するためuGUIビューは表示しない（falseは常に通す）
            // In web mode the web renders this screen, so never show the uGUI view (false always passes)
            var visible = isActive && !WebUiScreenGate.IsWebUiMode;
            mainInventoryObject.SetActive(visible);
            subInventoryParent.gameObject.SetActive(visible);
        }
```

- [ ] **Step 6: RecipeViewerView.SetActive をゲート**（`using Client.Game.InGame.UI.UIState;` を追加）

```csharp
        public void SetActive(bool isActive)
        {
            // webモード中はWeb側が同画面を描画するためuGUIビューは表示しない（falseは常に通す）
            // In web mode the web renders this screen, so never show the uGUI view (false always passes)
            gameObject.SetActive(isActive && !WebUiScreenGate.IsWebUiMode);
        }
```

- [ ] **Step 7: 旧API参照の残存確認**

Run: `grep -rn "IsCefVisible\|IsWebSupportedState\|SetCurrentUiState\|CurrentUiState" moorestech_client/Assets/Scripts --include="*.cs"`
Expected: ヒット 0 件

- [ ] **Step 8: コンパイル + WireContract回帰**

Run: `uloop compile --project-path ./moorestech_client`（ErrorCount 0）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractTest"`（全PASS）

- [ ] **Step 9: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game
git commit -m "refactor(webui): uGUIステートマシンをフル稼働に戻しCEFを常時透明オーバーレイ化"
```

---

### Task 2: TS — 背景透過 + 画面バックドロップ

**Files:**
- Modify: `moorestech_web/webui/src/app/index.css`
- Modify: `moorestech_web/webui/src/app/App.module.css`
- Modify: `moorestech_web/webui/src/app/App.tsx`

**Interfaces:**
- Consumes: `screen`（App.tsx 内の既存ルーティング値）
- Produces: `data-testid="screen-backdrop"`（screen !== "none" 時のみ存在）

- [ ] **Step 1: index.css の body 背景を完全透過に変更**

`background-color: rgba(17, 17, 17, 0.6);` を `background-color: transparent;` に変更し、コメントを更新:

```css
/* CEF は常時表示の透明オーバーレイのため body は完全透過（dim は App の screen バックドロップが担う） */
/* The body is fully transparent since CEF is an always-on overlay; dimming is done by App's screen backdrop */
```

- [ ] **Step 2: App.module.css にバックドロップを追加**（ファイル末尾）

```css
/* 画面表示中のみの全面 dim。クリックは透過し、後続siblingのパネルが上に描画される */
/* Full-screen dim only while a screen is open; clicks pass through and later siblings paint above */
.backdrop {
  position: fixed;
  inset: 0;
  background: rgba(17, 17, 17, 0.6);
  pointer-events: none;
}
```

- [ ] **Step 3: App.tsx の layout 直下・先頭にバックドロップを追加**

```tsx
      {screen !== "none" && <div className={styles.backdrop} data-testid="screen-backdrop" />}
```

- [ ] **Step 4: 検証**

Run: `cd moorestech_web/webui && npx tsc -b && npm test && npm run test:e2e`
Expected: すべて全PASS（e2e 24本の回帰確認込み）

- [ ] **Step 5: Commit**

```bash
git add moorestech_web/webui/src
git commit -m "feat(webui): 常時オーバーレイ用に背景を透過化し画面バックドロップを追加"
```

---

### Task 3: Unity — CEF RawImage の raycastTarget 無効化（prefab変更）

**Files:**
- Modify（uloop経由のみ）: `moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab` 内 CefUnity 配下の RawImage

**背景**: raycastTarget=1 のままだと CEF 表示中 `EventSystem.current.IsPointerOverGameObject()` が常に true になり、GameScreen のブロッククリックインタラクト（`GameScreenSubInventoryInteractService.cs:30`）が死ぬ。CEF への入力転送は EventSystem 非依存（`Input.mousePosition` 直読み）のため raycastTarget=0 でも Web UI クリックは動く。

- [ ] **Step 1: uloop execute-dynamic-code で prefab を変更**

以下の処理を行う C# コードを `uloop execute-dynamic-code` で実行:
1. `AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Asset/UI/Prefab/MainGameUI.prefab")` はインスタンス変更に使えないため、`PrefabUtility.LoadPrefabContents` でロード
2. ルート配下から名前 "CefUnity" の子を探し、その配下（自身含む）の全 `RawImage` の `raycastTarget = false` に設定
3. `PrefabUtility.SaveAsPrefabAsset` で保存 → `PrefabUtility.UnloadPrefabContents`
4. 変更した RawImage の数をログ出力（0件なら失敗として報告）

- [ ] **Step 2: 変更確認**

Run: `git diff moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab | grep -A1 -B1 "m_RaycastTarget"`
Expected: CefUnity 配下の RawImage の `m_RaycastTarget: 1` → `0` の差分のみ

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab
git commit -m "fix(webui): CEF RawImageのraycastTargetを無効化しゲーム側クリックを透過"
```

---

### Task 4: PlayMode 遷移マトリクス検証

**前提知識**: 必ず `.claude/skills/unity-playmode-recorded-playtest/SKILL.md` を先に読むこと（PlayMode起動・入力注入の制約・落とし穴）。ゲームコードは legacy `UnityEngine.Input` を多用するため**キー注入では駆動できない**。遷移は `uloop execute-dynamic-code` で `UIStateControl` を直接駆動する:
- `ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>()` で取得
- Web要求経路は `RequestTransition(UIStateEnum.PlayerInventory)`
- 任意state遷移の直接検証は各stateの `GetNextUpdate` を待たず、`RequestTransition` が GameScreen/PlayerInventory 限定Actionの内側APIなので、**検証はこの内側APIで全stateに対して行ってよい**（`RequestTransition(UIStateEnum.PlaceBlock)` 等。ただし SubInventory はコンテキスト必須のため後述の実ブロック経由）

**検証マトリクス**（各行: 遷移操作 → 期待。スクショ（uloop-screenshot GameView）で目視確認しレポートに貼る）:

| # | 操作（execute-dynamic-code / 実操作） | 期待 |
|---|---|---|
| 1 | PlayMode 起動しゲーム内到達（webモード既定ON） | uGUI HUD（ホットバー・チャレンジHUD）表示、CEF透明で3D見える、カメラ可動 |
| 2 | `RequestTransition(PlayerInventory)` | Webインベントリ画面表示（dim + 3Dが透ける）、uGUIインベントリ**非表示**、カーソル表示 |
| 3 | `RequestTransition(GameScreen)` | Web画面消滅、ホットバー健在、カメラ復帰 |
| 4 | `RequestTransition(PlaceBlock)` | uGUI設置HUD + 3Dプレビュー表示（B키相当）。Web透明のまま |
| 5 | `RequestTransition(GameScreen)` → `RequestTransition(DeleteBar)` | uGUI削除バー表示 |
| 6 | GameScreen → `RequestTransition(PauseMenu)` | uGUIポーズメニュー表示 |
| 7 | GameScreen → `RequestTransition(ChallengeList)` / `ResearchTree` | 各uGUI画面表示 |
| 8 | GameScreen で実ブロックをSubInventory化: シーン上の openable ブロックを `BlockGameObjectDataStore` から取得し `UITransitContextContainer.Create<ISubInventorySource>(new BlockSubInventorySource(...))` 付きで遷移（execute-dynamic-code で `UIStateControl` の private は触らず、`GameScreenSubInventoryInteractService` 相当のコンテキストを作って `RequestTransition` では渡せないため、`_uiStateDictionary` 経由は不可。**代替: マウスクリックが通ることの検証を優先**し、カーソル位置注入が不可能なら「raycastTarget=0 により `IsPointerOverGameObject()` が false になること」を execute-dynamic-code のログで確認） | Webブロックインベントリ表示 or 少なくともクリック経路が開通していることのログ確認 |
| 9 | Web ✕ボタン相当: `RequestTransition(GameScreen)`（実クリックは手動確認に委ねる） | GameScreen復帰 |
| 10 | webモードOFF→ON（DebugParameters `WebUiCefActiveKey` を false→true に書き換え） | 両エッジで GameScreen 正規化、uGUIモードでは従来のuGUIインベントリが出る（Tab相当は `RequestTransition(PlayerInventory)`） |

- [ ] **Step 1**: skill読解 → PlayMode起動 → ゲーム内到達
- [ ] **Step 2**: マトリクス1〜10を順に実行、各行スクショ + 判定を記録
- [ ] **Step 3**: 発見した問題を列挙（修正はしない。レポートに BLOCKED/ISSUE として記載）
- [ ] **Step 4**: PlayMode停止、レポート `.superpowers/sdd/task-4-verification-report.md` 作成

---

### Task 5: ドキュメント + 最終QA

- [ ] **Step 1**: `docs/webui/TODO.md` の INFRA-6 記述を更新（「抑止」記述を撤回し、uGUIステートマシン・パススルー型 + CEF常時透明オーバーレイ + 置換ビューゲート方式へ。既知の制限: 入力の二重配送は INFRA-2）
- [ ] **Step 2**: 最終QA: `uloop compile` / WireContractTest / `npx tsc -b && npm test && npm run test:e2e` すべて緑
- [ ] **Step 3**: Commit
