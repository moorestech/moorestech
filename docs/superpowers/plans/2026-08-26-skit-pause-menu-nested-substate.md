# スキット中ポーズメニュー（入れ子サブステート） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** uGUIモードのスキット再生中にEscでポーズメニューを開閉できるようにする（ADR 0035）。

**Architecture:** `SkitState` に列車HUDと同型の入れ子ステートマシン（`SkitScreenUIStateController` / Skit・PauseMenu の2サブステート）を持たせ、既存の `PauseMenuStateService` を再利用してメニューを表示する。スキット会話UIの「非表示→Esc復帰」判定は `SkitUITools` の `Update` ポーリングから `SkitPlayingSubState` へ移し、Escの優先順位（UI復帰 → メニュー）を一箇所で決める。スキット本体の再生は一切触らない（メニュー中も背後で進行）。

**Tech Stack:** Unity C# / VContainer / 既存 `InputManager.UI.OpenMenu`・`CloseUI`（どちらもEsc）

## Requirements

- R1: uGUIモードでスキット再生中にEscを押すとポーズメニュー（`PauseMenuObject`）が表示される。受け入れ: PlayModeでスキット中にEsc→メニューが見える
- R2: メニュー表示中もスキット（自動送り・ボイス・Waitコマンド）は止まらない。受け入れ: `SkitCommandExecutor`・各Commandに変更なし
- R3: 会話UIが非表示（HiddenButton押下後）のときのEscは会話UIの復帰のみ行い、メニューは開かない。表示中のEscでメニューが開く。受け入れ: 非表示中に1回目Esc→UI復帰・メニュー無し、2回目Esc→メニュー
- R4: メニュー中のEscでメニューを閉じてスキット画面へ戻る（`UIStateEnum.Story` のまま）。受け入れ: `UIStateControl.CurrentState` が変わらない
- R5: メニュー表示中にスキットが終了したらメニューを閉じて `GameScreen` へ遷移する。受け入れ: `PauseMenuObject` が非アクティブになり `CurrentState == GameScreen`
- R6: webモードではスキット中にメニューを開かない（現状維持）。受け入れ: `WebUiScreenGate.IsWebUiMode` 時はEscを無視
- やらないこと: スキット一時停止機構、web UI側メニュー対応、`SkitPresentationStateStore` のブロック変更、`UIStateEnum` の追加

## Global Constraints

- AGENTS.md 全規約（1ファイル200行以下、1ディレクトリ10ファイル以下、partial禁止、Func禁止、デフォルト引数禁止、日英2行コメント、`#region Internal` はローカル関数のみ）
- 時間計測に実時間APIを使わない（本planは時間を扱わない）
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client`
- 作業はメインワークツリーではなく `moores-wt new feature/skit-pause-menu` で切ったworktreeで行う（CLAUDE.local.md）
- .metaは手動作成しない（Unity起動で生成されたものをコミット）
- `Client.Game` は `Client.Skit` を参照済み（`Client.Game.asmdef:55`）。逆方向参照は追加しない

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `ISkitScreenSubState` / `SkitScreenUIStateEnum` | Client.Game `InGame/UI/UIState/State/Skit/` | 入れ子ステート | `State/TrainHUDScreen/ITrainHudScreenSubState.cs` |
| 2 | `SkitScreenUIStateController` | 同上 | 入れ子ステートマシン（ステートから明示駆動） | `TrainHudScreenUIStateController.cs` |
| 3 | `SkitPlayingSubState` | 同上 | Escポーリング → UI復帰 or メニュー | `TrainHudGameScreenSubState.cs` |
| 4 | `SkitPauseMenuSubState` | 同上 | `PauseMenuStateService` 再利用 | `TrainHudPauseMenuSubState.cs`（ほぼ同一） |
| 5 | `SkitManager.IsSkitUiHidden` / `ShowHiddenSkitUi()` | Client.Game `Skit/SkitManager.cs` | 会話UIの非表示状態の窓口 | `SkitManager.GetSkitOrigin()`（シーン実体の公開） |
| 6 | `SkitUITools.IsUIHidden` / `ShowUI()`、`ManualUpdate` 削除 | Client.Skit `UI/SkitUITools.cs` | 直読み `Input.GetKeyDown(Escape)` の撤去 | TODOコメント「InputManagerに移す」を実行 |
| 7 | `SkitState` 変更 | 既存 | サブステートの駆動 | `TrainHUDScreenState.GetNextUpdate/OnExit` |

**データフロー:** Esc入力 →（`SkitPlayingSubState` が判定）→ `SkitManager.ShowHiddenSkitUi()` か `SkitScreenUIStateEnum.PauseMenu` 遷移。交差点なし（新規bool戻りや第2の書き込み経路は作らない）。

**機構選択:** 既存のEsc復帰ポーリング（`SkitUITools.ManualUpdate`）を残して並走させる案（受動的統合）は、同フレームで両者がEscを見て「復帰＋メニュー」が同時発火するフレーム順依存バグになるため採らない。判定を `SkitPlayingSubState` へ一本化する（AGENTS.md「同種の条件分岐は一箇所へ」）。出所: agent前提。

**機能パリティ（死活表）:**
| 操作 | 計画後 | 根拠 |
|---|---|---|
| スキット中 HiddenButton でUI非表示 | 生きる | `HideUI` は変更しない |
| 非表示中 Esc でUI復帰 | 生きる | `SkitPlayingSubState` から `ShowHiddenSkitUi()` を呼ぶ |
| Skip / Auto ボタン | 生きる | 変更なし |
| 列車HUD中のポーズメニュー | 生きる | `PauseMenuStateService` はステートレスな共有サービス、変更なし |
| スキット中 webモード | 現状維持 | R6 |

---

### Task 1: SkitUITools のEsc直読みを撤去し、非表示状態の窓口を公開する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/UI/SkitUITools.cs:9-62`
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/UI/SkitUI.cs:138-141`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs:42-47`

**Interfaces:**
- Produces: `SkitUITools.IsUIHidden : bool`（`{ get; private set; }`）、`SkitUITools.ShowUI()`、`SkitUI.IsUIHidden : bool`、`SkitUI.ShowHiddenUI()`、`SkitManager.IsSkitUiHidden : bool`、`SkitManager.ShowHiddenSkitUi()`
- Consumes: なし

- [x] **Step 1: SkitUITools を書き換える**

`_isUIHidden` フィールドを `{ get; private set; }` プロパティに畳み、`ManualUpdate` を `ShowUI` に置き換える:

```csharp
        public bool IsUIHidden { get; private set; }
        
        // ...ctor は変更なし（HideUI の購読を維持）...
        
        private void HideUI()
        {
            IsUIHidden = true;
            _skitUiDocument.rootVisualElement.style.display = DisplayStyle.None;
        }

        // 非表示にした会話UIを戻す。Esc判定はUIステート側（SkitPlayingSubState）が持つ
        // Restore the hidden dialogue UI. The Esc decision lives in the UI state (SkitPlayingSubState)
        public void ShowUI()
        {
            IsUIHidden = false;
            _skitUiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
        }
```

`ManualUpdate` メソッドと `using UnityEngine;`（`Input` 用。他に使用が無ければ）を削除する。

- [x] **Step 2: SkitUI の Update を撤去し、窓口を追加する**

`private void Update() { _skitUITools.ManualUpdate(); }` を削除し、以下を追加:

```csharp
        public bool IsUIHidden => _skitUITools.IsUIHidden;
        
        public void ShowHiddenUI()
        {
            _skitUITools.ShowUI();
        }
```

- [x] **Step 3: SkitManager に窓口を追加する**

`IsPlayingSkit` の直下に追加:

```csharp
        // 会話UIの非表示状態をUIステートへ公開する（Escの優先順位判定に使う）
        // Expose the hidden state of the dialogue UI to the UI state (used to prioritize Esc handling)
        public bool IsSkitUiHidden => skitUI.IsUIHidden;
        
        public void ShowHiddenSkitUi()
        {
            skitUI.ShowHiddenUI();
        }
```

- [x] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [x] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Skit/UI/SkitUITools.cs moorestech_client/Assets/Scripts/Client.Skit/UI/SkitUI.cs moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs
git commit -m "refactor(skit): 会話UIのEsc復帰判定をUIステート側へ移すため窓口を公開"
```

---

### Task 2: スキット用入れ子サブステート群を新設する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/Skit/ISkitScreenSubState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/Skit/SkitScreenUIStateEnum.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/Skit/SkitPlayingSubState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/Skit/SkitPauseMenuSubState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/Skit/SkitScreenUIStateController.cs`

**Interfaces:**
- Consumes: `SkitManager.IsSkitUiHidden` / `ShowHiddenSkitUi()`（Task 1）、`PauseMenuStateService.OnEnter/OnExit/IsClosePause()`（既存）
- Produces: `SkitScreenUIStateController(SkitManager, PauseMenuStateService)` に `StartSubState()`, `Update()`, `ShutdownSubState()`, `CurrentState`

- [x] **Step 1: インターフェースとenum**

`ISkitScreenSubState.cs`:
```csharp
namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // スキット画面専用のサブステートインターフェース。ITrainHudScreenSubStateと同型
    // Sub-state interface for the skit screen. Same shape as ITrainHudScreenSubState
    public interface ISkitScreenSubState
    {
        void OnEnter();

        // 別のサブステートへ遷移する場合は遷移先を返す。nullなら継続
        // Return the next sub-state to transit to, or null to stay in the current one
        SkitScreenUIStateEnum? GetNextUpdate();

        void OnExit();
    }
}
```

`SkitScreenUIStateEnum.cs`:
```csharp
namespace Client.Game.InGame.UI.UIState.State.Skit
{
    public enum SkitScreenUIStateEnum
    {
        Playing,
        PauseMenu,
    }
}
```

- [x] **Step 2: SkitPlayingSubState**

```csharp
using Client.Game.Skit;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // スキット再生中のサブステート。Escは会話UI復帰を優先し、表示中ならポーズメニューを開く
    // Sub-state while the skit plays. Esc restores the hidden dialogue UI first; when visible, it opens the pause menu
    public class SkitPlayingSubState : ISkitScreenSubState
    {
        private readonly SkitManager _skitManager;

        public SkitPlayingSubState(SkitManager skitManager)
        {
            _skitManager = skitManager;
        }

        public void OnEnter()
        {
            // スキット中はカーソルを表示してUIを操作できるようにする
            // Keep the cursor visible during the skit so the UI stays operable
            InputManager.MouseCursorVisible(true);
        }

        public SkitScreenUIStateEnum? GetNextUpdate()
        {
            if (!InputManager.UI.OpenMenu.GetKeyDown) return null;

            // webモードのポーズメニューはSkitPresentationStateStoreのブロック対象なので開かない（ADR 0035）
            // The web-mode pause menu is blocked by SkitPresentationStateStore, so do not open it (ADR 0035)
            if (WebUiScreenGate.IsWebUiMode) return null;

            // 会話UIが隠れているなら復帰のみ。メニューは次のEscで開く
            // If the dialogue UI is hidden, only restore it; the next Esc opens the menu
            if (_skitManager.IsSkitUiHidden)
            {
                _skitManager.ShowHiddenSkitUi();
                return null;
            }

            return SkitScreenUIStateEnum.PauseMenu;
        }

        public void OnExit()
        {
        }
    }
}
```

- [x] **Step 3: SkitPauseMenuSubState**

```csharp
using Client.Game.InGame.UI.UIState.State.PauseMenu;

namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // ポーズメニュー表示中のサブステート。Escで閉じてスキット再生へ戻る。背後のスキットは止めない
    // Sub-state while the pause menu shows. Esc closes it and returns to playing. The skit keeps running behind it
    public class SkitPauseMenuSubState : ISkitScreenSubState
    {
        private readonly PauseMenuStateService _pauseMenuStateService;

        public SkitPauseMenuSubState(PauseMenuStateService pauseMenuStateService)
        {
            _pauseMenuStateService = pauseMenuStateService;
        }

        public void OnEnter()
        {
            _pauseMenuStateService.OnEnter();
        }

        public SkitScreenUIStateEnum? GetNextUpdate()
        {
            return _pauseMenuStateService.IsClosePause() ? SkitScreenUIStateEnum.Playing : null;
        }

        public void OnExit()
        {
            _pauseMenuStateService.OnExit();
        }
    }
}
```

- [x] **Step 4: SkitScreenUIStateController**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Game.Skit;

namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // スキット画面専用の入れ子ステートマシン。TrainHudScreenUIStateControllerと同型
    // Nested state machine dedicated to the skit screen. Same shape as TrainHudScreenUIStateController
    public class SkitScreenUIStateController
    {
        private readonly Dictionary<SkitScreenUIStateEnum, ISkitScreenSubState> _states;

        public SkitScreenUIStateEnum CurrentState { get; private set; }

        public SkitScreenUIStateController(SkitManager skitManager, PauseMenuStateService pauseMenuStateService)
        {
            _states = new Dictionary<SkitScreenUIStateEnum, ISkitScreenSubState>
            {
                { SkitScreenUIStateEnum.Playing, new SkitPlayingSubState(skitManager) },
                { SkitScreenUIStateEnum.PauseMenu, new SkitPauseMenuSubState(pauseMenuStateService) },
            };
        }

        public void StartSubState()
        {
            CurrentState = SkitScreenUIStateEnum.Playing;
            _states[CurrentState].OnEnter();
        }

        public void Update()
        {
            var next = _states[CurrentState].GetNextUpdate();
            if (next == null) return;

            _states[CurrentState].OnExit();
            CurrentState = next.Value;
            _states[CurrentState].OnEnter();
        }

        // スキット終了時に呼ぶ。メニューが開いていれば閉じる（ADR 0035: 終了時はGameScreenへ）
        // Called when the skit ends. Closes the pause menu if open (ADR 0035: return to GameScreen on end)
        public void ShutdownSubState()
        {
            _states[CurrentState].OnExit();
        }
    }
}
```

- [x] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0（未使用クラスのwarningは可）

- [x] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/Skit
git commit -m "feat(ui): スキット画面用の入れ子サブステート（再生/ポーズメニュー）を追加"
```

---

### Task 3: SkitState にサブステートを組み込み、DI登録する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SkitState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs:105-118`

**Interfaces:**
- Consumes: `SkitScreenUIStateController`（Task 2）

- [x] **Step 1: SkitState を書き換える**

```csharp
using Client.Game.Common;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.Skit;
using Client.Game.Skit;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State
{
    public class SkitState : IUIState
    {
        private readonly SkitManager _skitManager;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        private readonly SkitScreenUIStateController _subStateController;
        
        public SkitState(SkitManager skitManager, PlayerInventoryViewController playerInventoryViewController, SkitScreenUIStateController subStateController)
        {
            _skitManager = skitManager;
            _playerInventoryViewController = playerInventoryViewController;
            _subStateController = subStateController;
        }
        
        public void OnEnter(UITransitContext context)
        {
            // インベントリが開いている場合は閉じる
            if (context.LastStateEnum == UIStateEnum.PlayerInventory || context.LastStateEnum == UIStateEnum.SubInventory)
            {
                _playerInventoryViewController.SetActive(false);
            }

            // スキット状態へ遷移
            GameStateController.ChangeState(GameStateType.Skit);

            KeyControlDescription.Instance.SetText("");

            // 再生サブステートから開始（カーソル表示はサブステート側が担う）
            // Start from the playing sub-state (the sub-state owns cursor visibility)
            _subStateController.StartSubState();
        }

        public UITransitContext GetNextUpdate()
        {
            // スキット終了はメニュー表示中でも優先し、GameScreenへ戻す（ADR 0035）
            // Skit end takes priority even while the menu shows, returning to GameScreen (ADR 0035)
            if (!_skitManager.IsPlayingSkit) return new UITransitContext(UIStateEnum.GameScreen);

            _subStateController.Update();
            return null;
        }
        
        public void OnExit()
        {
            // 入れ子サブステートを終了（開いていればポーズメニューを閉じる）
            // Tear down the nested sub-state (closes the pause menu if open)
            _subStateController.ShutdownSubState();

            // スキット終了時はカーソルを非表示に戻す
            InputManager.MouseCursorVisible(false);
            
            // ゲーム状態をInGameに戻す
            GameStateController.ChangeState(GameStateType.InGame);
        }
    }
}
```

- [x] **Step 2: DI登録**

`MainGameInteractionRegistration.cs` の `builder.Register<PauseMenuStateService>(Lifetime.Singleton);` の直後に追加:

```csharp
            builder.Register<SkitScreenUIStateController>(Lifetime.Singleton);
```

（`TrainHudScreenUIStateController` の登録行があれば同じ並びに置く。`using Client.Game.InGame.UI.UIState.State.Skit;` を追加）

- [x] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [x] **Step 4: 既存テスト（Web UIゲート分類）を回す**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "WebUiGate"`
Expected: PASS。`SkitPlayingSubState.cs` が `WebUiScreenGate` を参照するため分類ルールが未登録で失敗する場合は、`Client.Tests/WebUi/Gate/WebUiGateClassification.cs` の `PauseMenuStateService` 行（80行目付近）の並びに以下を追加する:

```csharp
            new Rule("Client.Game/InGame/UI/UIState/State/Skit/SkitPlayingSubState.cs", Category.GatedRoot, "スキット中ポーズメニュー (C2)"),
```

- [x] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "feat(ui): スキット中にEscでポーズメニューを開閉できるようにする (ADR 0035)"
```

---

### Task 4: PlayMode での動作確認（unityプレイ録画テスト）

**Files:**
- なし（検証のみ。unity-playmode-recorded-playtest スキル参照）

- [ ] **Step 1: スキット中のEsc挙動を確認する**

unity-playmode-recorded-playtest のDSLで新規ゲーム開始→開幕スキット中に以下を実行し録画する:
1. Esc → `PauseMenuObject` がアクティブ（R1）、背後で会話が進む（R2）
2. Esc → メニューが閉じ `UIStateControl.CurrentState == Story`（R4）
3. HiddenButton クリック → Esc → 会話UIが戻りメニューは出ない（R3）→ Esc → メニュー
4. メニューを開いたまま Skip → メニューが消え `CurrentState == GameScreen`（R5）

Expected: 4項目すべて録画で確認。失敗した項目はbdへ `bd create --deps=discovered-from:<本タスクid>` で積む。

- [ ] **Step 2: ログにErrorが無いことを確認**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 本変更由来のエラー 0

---

### Task 5: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘の機械的修正を適用し、設計判断はAskUserQuestionで仰ぐ。

- [ ] **Step 2: pr-create スキルでPRを作成し、bd close する**

```bash
bd close <本タスクid> --reason="PR #<番号> 作成"
```

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0035-skit-pause-menu-nested-substate.md`／裁定: `.decisions/2026-08-26-スキット中のポーズメニューは入れ子ステートで背後再生継続.md`
- Esc復帰判定を `SkitUITools.ManualUpdate` から `SkitPlayingSubState` へ移す — 出所: agent前提（同フレーム二重発火の回避・「同種の条件分岐は一箇所へ」規約・`SkitUITools` のTODO「InputManagerに移す」）
- `UIStateEnum` を増やさず入れ子サブステートにする — 出所: agent前提（`TrainHudScreenUIStateController` 前例。ADR 0035 で採択）
- webモード判定を `SkitPlayingSubState` 内で行う — 出所: ユーザー裁定 2026-08-26「uGUIモードのみ」
- 単体テストは追加しない — 出所: agent前提（`InputManager` 静的ポーリング依存で列車HUDサブステートにも前例テスト無し。検証はTask 4のプレイ録画テスト）
