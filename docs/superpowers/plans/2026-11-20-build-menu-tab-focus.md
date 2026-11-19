# ビルドメニューの Tab が検索欄へ吸われる不具合の修正 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ビルドメニューの一覧を開いた状態で Tab を押すと、検索欄へフォーカスが移らずにインベントリが開くようにする。

**Architecture:** GameUi の入力規則（`GameUiInputRules`）は UIDocument の根に TrickleDown で付いている。フォーカスが無いとき `PanelEventHandler` は Tab 由来の `NavigationMoveEvent` をパネルの根（`panel.visualTree`。UIDocument の根の親）宛てに送るので、規則を通らず、UITK の既定のフォーカス移動が検索欄へフォーカスを移す。パネルの根に「宛先がパネルの根自身のナビゲーションだけを打ち消す」ガードを1つ足す。要素宛てのナビゲーションは今までどおり UIDocument の根の規則が打ち消す。

**Tech Stack:** Unity 6000.3.8f1 / UI Toolkit（C# で組む。UXML なし）/ com.unity.ugui 2.0.0 の `PanelEventHandler` / com.unity.inputsystem 1.18.0 / NUnit（`UiTestHost` の L1、EditModeInPlayingTest）

## Requirements

- R1: ビルドメニュー表示中・EventSystem が GameUi のパネルを選択済み（UI を1回クリックした後）・パネル内にフォーカス無しの状態で Tab を押すと、UIState が `PlayerInventory` になり、検索欄（`build-menu-search`）へフォーカスが移らない。受け入れ基準: Task 2 の EditModeInPlayingTest が、修正前のコードで失敗し修正後に通る。
- R2: フォーカスが無いときにパネルの根へ届いた `NavigationMoveEvent`（Tab・矢印由来）・`NavigationSubmitEvent`・`NavigationCancelEvent` は、フォーカスを動かさない。受け入れ基準: Task 1 の L1 テストが通る。
- R3: 要素にフォーカスがあるときの挙動は変えない。既存の `GameUiInputRulesTest`・`GameUiInputTest`（パネルの根で「規則より前」の件数を数えている）・`BuildMenuSearchInputGuardTest`・`BuildMenuStateTextInputGuardTest` が通り続ける。
- やらないこと:
  - 検索欄に入力している間の Tab（今も TextField 自身の処理で1回でインベントリが開く）は変えない。
  - 置く場所を決めている間（`PlaceBlock`）の Tab（今はビルドメニューを開き直す）は変えない。
  - 実際のキーボードでの確認は、この PR ではしない。PR の後に作者がプレイで確かめる（原因は Editor 上でキー入力をプログラムから送って確かめた）。PR の報告にこの残りを書く。
  - 同じ依頼のほかの6件（発射台・クリーンルーム・分岐器）は別の PR。

## Global Constraints

- 1ファイル200行以下。1ディレクトリの .cs は10ファイルまで（`Client.Tests/EditModeInPlayingTest/Ui/` は既に10ファイルなので、新しいテストはサブディレクトリ `BuildMenu/` に置く）。
- コメントは日本語→英語の2行セット。各1行。
- `.meta` は手で作らない（Unity が生成したものはコミットしてよい）。Prefab・シーンは触らない。
- try-catch・partial・`Func<>`・デフォルト引数は使わない。
- .cs を変えたら必ず `uloop compile --project-path ./moorestech_client`。
- 作業場所: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/build-menu-tab-focus`（ブランチ `fix/build-menu-tab-focus`、origin/master 28c483852a 起点）。
- Unity Editor は Mac mini では同時に1本まで。起動前に `moores-wt status` で他の Editor が無いかを確かめ、他のセッションの Editor が動いていれば落とさずに `~/bin/winunity`（Windows 機。push 済みブランチを渡す）で compile / test を回す。
- PR はマージしない（moorestech は PR 作成まで）。PR 本文の先頭行は報告ページの URL（`CLAUDE.local.macmini.md`「PR の成果物の載せ先」）。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## File Structure

| ファイル | 役割 |
|---|---|
| Create `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiPanelRootNavigationGuard.cs` | パネルの根宛てのナビゲーションを打ち消すガード |
| Modify `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs:30` | 起動時にガードをパネルの根へ付ける |
| Create `moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootNavigationGuardTest.cs` | L1: フォーカス無しでパネルの根へ届いたナビゲーションがフォーカスを動かさない |
| Create `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/BuildMenuTabOpensInventoryTest.cs` | 実ゲーム: ビルドメニューでフォーカス無しの Tab がインベントリを開く |

## 配置と前例

- ガードの置き場 `Client.Ui/Foundation/Input/` は、GameUi 全体の入力の決まり（`GameUiInputRules`・`UiKeyFocusKeepMark`）が並ぶ場所。ガードはドメインの語（ビルドメニュー・検索欄）を持たず、「パネルの根宛てのナビゲーションを止める」だけを行う。
- 付ける側は既存の `GameUiInputInstaller.Initialize`（`GameUiInputRules.Attach(root)` を呼んでいる所）。新しい DI 登録は足さない。
- 打ち消しの書き方（`StopImmediatePropagation`＋`focusController.IgnoreEvent`）は `GameUiInputRules.CancelNavigation`（`GameUiInputRules.cs:146-151`）と同じ。
- 規則一式をパネルの根へ付け替える案は採らない。`GameUiInputTest` の `NavigationEventCounter` がパネルの根の TrickleDown で「規則より前」の件数を数えていて、規則を同じ要素へ先に付けると数えられなくなる。ガードは宛先がパネルの根自身のイベントだけを止めるので、要素宛てのイベントは今までどおりカウンタと規則を順に通る。
- 実ゲームのテストの形は `Client.Tests/EditModeInPlayingTest/Ui/GameUiInputTest.cs`（`EnterPlayModeUtil`→`EnterPlayMode`→`LoadMainGame`→`EditorBackgroundInputScope`→`SemanticInput`）に合わせる。

---

### Task 1: パネルの根のナビゲーションガードと L1 テスト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiPanelRootNavigationGuard.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs:30`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootNavigationGuardTest.cs`

**Interfaces:**
- Consumes: `GameUiRoot.GetRootElement()`（UIDocument の根。`root.panel.visualTree` がパネルの根）
- Produces: `public static void GameUiPanelRootNavigationGuard.Attach(VisualElement panelRoot)`（Task 2 は起動済みのゲームで間接的に使う）

- [ ] **Step 1: L1 テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootNavigationGuardTest.cs`:

```csharp
using Client.Input;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// フォーカスが無い間にパネルの根へ届くナビゲーションが、入力欄へフォーカスを移さないことを実パネルで確かめる（L1）
    /// Verifies on a real panel that navigation reaching the panel root with nothing focused never moves focus into a text field (L1)
    /// </summary>
    /// <remarks>
    /// ガードはパネルの根に付き、テストの間で外れない。パネルはテストクラスごとに作り直されるので、このクラスの中だけに効く
    /// The guard sits on the panel root and stays across tests; the panel is recreated per test class, so it only affects this class
    /// </remarks>
    public class GameUiPanelRootNavigationGuardTest : UiTestHost
    {
        private TextField _textField;

        [SetUp]
        public void AttachRulesGuardAndTextField()
        {
            // 本番と同じ組み合わせ: 規則は UIDocument の根に当たる器へ、ガードはパネルの根へ付ける
            // Same pairing as production: the rules on a container standing in for the UIDocument root, the guard on the panel root
            var rulesRoot = new VisualElement { name = "guard-rules-root", pickingMode = PickingMode.Ignore };
            rulesRoot.StretchToParentSize();
            rootVisualElement.Add(rulesRoot);
            GameUiInputRules.Attach(rulesRoot);
            GameUiPanelRootNavigationGuard.Attach(rootVisualElement.panel.visualTree);

            _textField = new TextField { name = "guard-text" };
            _textField.style.width = 300;
            rulesRoot.Add(_textField);
            simulate.FrameUpdate();
        }

        [Test]
        public void フォーカスが無いときのTab由来の移動は入力欄へフォーカスを移さない()
        {
            Assert.IsNull(panel.focusController.focusedElement);

            // PanelEventHandler.SendTabEvent と同じく、宛先をパネルの根にして送る
            // Aim the event at the panel root, exactly as PanelEventHandler.SendTabEvent does
            var panelRoot = rootVisualElement.panel.visualTree;
            using (var tab = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next))
            {
                tab.target = panelRoot;
                panelRoot.SendEvent(tab);
            }
            simulate.FrameUpdate();

            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void フォーカスが無いときの決定と取消もフォーカスを動かさない()
        {
            var panelRoot = rootVisualElement.panel.visualTree;
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = panelRoot;
                panelRoot.SendEvent(submit);
            }
            using (var cancel = NavigationCancelEvent.GetPooled())
            {
                cancel.target = panelRoot;
                panelRoot.SendEvent(cancel);
            }
            simulate.FrameUpdate();

            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void 入力欄にフォーカスがあるときは規則が今までどおり移動を止める()
        {
            _textField.Focus();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);

            // 宛先を決めずに送ると、フォーカス中の入力欄宛てになる。ガードは通し、UIDocument の根の規則が打ち消すのでフォーカスは入力欄に残る
            // Sent without a target it goes to the focused field; the guard lets it pass and the root rules cancel it, so focus stays on the field
            using (var tab = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next))
            {
                rootVisualElement.panel.visualTree.SendEvent(tab);
            }
            simulate.FrameUpdate();

            var focused = panel.focusController.focusedElement as VisualElement;
            Assert.IsTrue(focused != null && (focused == _textField || _textField.Contains(focused)));
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }
    }
}
```

- [ ] **Step 2: ガードを書く**

`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiPanelRootNavigationGuard.cs`:

```csharp
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// フォーカスが無い間にパネルの根へ届くナビゲーションを打ち消す
    /// Cancels navigation that reaches the panel root while nothing is focused
    /// </summary>
    /// <remarks>
    /// GameUiInputRules は UIDocument の根に付くので、その親であるパネルの根宛てのイベントは通らない。
    /// フォーカスが無いとき PanelEventHandler は Tab・矢印・Enter・ESC 由来のナビゲーションをパネルの根へ送り、止めないと UITK の既定のフォーカス移動が入力欄（ビルドメニューの検索欄など）へフォーカスを移す。
    /// GameUiInputRules sits on the UIDocument root, so events aimed at its parent, the panel root, never pass through it.
    /// With nothing focused PanelEventHandler aims navigation from Tab, arrows, Enter and ESC at the panel root, and unless stopped UITK's default focus move puts focus into a text field such as the build menu search.
    /// </remarks>
    public static class GameUiPanelRootNavigationGuard
    {
        public static void Attach(VisualElement panelRoot)
        {
            // 既定のフォーカス移動より先に走らせるため TrickleDown で付ける
            // Register as TrickleDown so it runs before the default focus move
            panelRoot.RegisterCallback<NavigationMoveEvent>(CancelWhenAimedAtPanelRoot, TrickleDown.TrickleDown);
            panelRoot.RegisterCallback<NavigationSubmitEvent>(CancelWhenAimedAtPanelRoot, TrickleDown.TrickleDown);
            panelRoot.RegisterCallback<NavigationCancelEvent>(CancelWhenAimedAtPanelRoot, TrickleDown.TrickleDown);
        }

        private static void CancelWhenAimedAtPanelRoot(EventBase evt)
        {
            // silent-ok: 要素宛てのナビゲーションは UIDocument の根の GameUiInputRules が打ち消すのでここは通す / 実ゲームの GameUiInputTest が規則を越えた件数 0 を確かめる
            // silent-ok: navigation aimed at an element is cancelled by GameUiInputRules on the UIDocument root, so let it pass / the in-game GameUiInputTest asserts nothing gets past the rules
            if (evt.target != evt.currentTarget) return;

            evt.StopImmediatePropagation();
            ((VisualElement)evt.currentTarget).focusController.IgnoreEvent(evt);
        }
    }
}
```

- [ ] **Step 3: 起動時にガードを付ける**

`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs` の `Initialize` を次の形にする（`GameUiInputRules.Attach(root);` の次に2行足す）:

```csharp
        public void Initialize()
        {
            var root = _gameUiRoot.GetRootElement();

            // パネル未接続では Pick も入力も届かないので起動の段階で止める
            // Without an attached panel neither Pick nor input works, so stop at startup
            if (root.panel == null) throw new InvalidOperationException("[GameUiInput] GameUi root is not attached to a panel");
            GameUiInputRules.Attach(root);

            // フォーカスが無い間のキー由来のナビゲーションはパネルの根へ届くので、根にもガードを付ける
            // Key-driven navigation with nothing focused reaches the panel root, so guard the root as well
            GameUiPanelRootNavigationGuard.Attach(root.panel.visualTree);
            UiInputExclusivity.SetGameUiPointerProbe(_pointerProbe);
        }
```

クラスの summary も合わせて直す:

```csharp
    /// <summary>
    /// GameUi のルートへ入力規則を、パネルの根へナビゲーションのガードを付け、Pick の probe を入力排他へ登録する
    /// Attaches the input rules to the GameUi root and the navigation guard to the panel root, and registers the Pick probe with the input exclusivity
    /// </summary>
```

- [ ] **Step 4: コンパイルし、L1 テストが通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.Input.GameUiPanelRootNavigationGuardTest"`
Expected: ErrorCount 0 / 3 件 PASS

- [ ] **Step 5: テストがこの不具合を検出することを確かめる（赤の確認）**

`GameUiPanelRootNavigationGuardTest.AttachRulesGuardAndTextField` の `GameUiPanelRootNavigationGuard.Attach(rootVisualElement.panel.visualTree);` を一時的にコメントアウトし、コンパイルして同じコマンドでこのクラスを実行する（テスト用のパネルはテストクラスの実行ごとに作り直されるので、前の実行のガードは残らない）。
Expected: `フォーカスが無いときのTab由来の移動は入力欄へフォーカスを移さない` が FAIL（`focusedElement` が `guard-text` の内側の要素になる）。
失敗を確認したらコメントアウトを戻し、もう一度 3 件 PASS を確認する。FAIL しなかった場合は、テストが不具合を検出できていないので、止めて報告する（赤にならない理由をテストパネルで調べる。合格にして進めない）。

- [ ] **Step 6: 既存の入力テストが通り続けることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.Input\.|BuildMenuSearchInputGuardTest|BuildMenuStateTextInputGuardTest"`
Expected: 全件 PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiPanelRootNavigationGuard.cs \
        moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiPanelRootNavigationGuard.cs.meta \
        moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootNavigationGuardTest.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootNavigationGuardTest.cs.meta
git commit -m "fix(ui): フォーカスが無い間にパネルの根へ届くナビゲーションを打ち消し、Tab が検索欄へ吸われないようにする"
```

---

### Task 2: 実ゲームで、ビルドメニューのフォーカス無しの Tab がインベントリを開くことを確かめる

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/BuildMenuTabOpensInventoryTest.cs`

**Interfaces:**
- Consumes: Task 1 の修正（起動時に `GameUiInputInstaller` がガードを付ける）。既存の `EditModeInPlayingTestUtil.EnterPlayModeUtil()`・`LoadMainGame()`、`UiStateTransitionWait.TransitionTo(UIStateControl, UIStateEnum)` と `EditorBackgroundInputScope.Open()`（どちらも名前空間 `Client.Tests.EditModeInPlayingTest.Ui`。新しいテストはその子の名前空間なので using は要らない）、`SemanticInput.TapKey(Key)`（`Client.Playtest.Input`）、`GameUiRoot.Instance.GetRootElement()`、`PanelEventHandler.panel`（`UnityEngine.UIElements`。com.unity.ugui）
- Produces: なし

- [ ] **Step 1: テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/BuildMenuTabOpensInventoryTest.cs`:

```csharp
using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest.Input;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.BuildMenu
{
    /// <summary>
    /// 実ゲームで、ビルドメニュー表示中・パネル選択済み・フォーカス無しの Tab がインベントリを開くことを確かめる
    /// Verifies in the real game that Tab opens the inventory while the build menu is shown, the panel is selected and nothing is focused
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class BuildMenuTabOpensInventoryTest
    {
        private const int InventoryWaitFrameLimit = 30;

        [UnityTest]
        public IEnumerator ビルドメニューでフォーカスが無いときのTabは検索欄へ吸われずインベントリを開く()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();
                await UniTask.DelayFrame(5);
                var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
                var panel = GameUiRoot.Instance.GetRootElement().panel;

                // 背面の Editor でも注入が届く設定にしてから、ビルドメニューを開く
                // Let injection reach a background Editor, then open the build menu
                using var backgroundInput = EditorBackgroundInputScope.Open();
                await UiStateTransitionWait.TransitionTo(uiStateControl, UIStateEnum.BuildMenu);
                await UniTask.DelayFrame(3);

                // UI を1回クリックした後と同じ状態にする: EventSystem が GameUi のパネルを選び、パネル内にフォーカスは無い
                // Reproduce the state after one UI click: the EventSystem selects the GameUi panel and nothing inside it is focused
                SelectGameUiPanel(panel);
                await UniTask.DelayFrame(2);
                Assert.IsNull(panel.focusController.focusedElement, "前提: パネル内にフォーカスが無い");
                Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "前提: 入力中の旗が立っていない");

                // 実キー相当の Tab: UITK へ届く IMGUI のキーと、ゲームが読む InputSystem のキーを両方入れる
                // A real-key-like Tab: both the IMGUI key that reaches UITK and the InputSystem key the game reads
                EditorGUIUtility.QueueGameViewInputEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab });
                EditorGUIUtility.QueueGameViewInputEvent(new Event { type = EventType.KeyUp, keyCode = KeyCode.Tab });
                await SemanticInput.TapKey(Key.Tab);
                for (var i = 0; i < InventoryWaitFrameLimit && uiStateControl.CurrentState != UIStateEnum.PlayerInventory; i++) await UniTask.Yield();

                var focused = panel.focusController.focusedElement as VisualElement;
                Assert.AreEqual(UIStateEnum.PlayerInventory, uiStateControl.CurrentState, $"Tab でインベントリが開いていない（フォーカス: {focused?.GetType().Name} '{focused?.name}'）");
                Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "Tab のあと入力中の旗が立っている（検索欄へ吸われた）");
            }

            #endregion
        }

        // 本番ではパネル上のクリックで PanelEventHandler.OnPointerDown がこの選択を行う
        // In production a click on the panel makes PanelEventHandler.OnPointerDown perform this selection
        private static void SelectGameUiPanel(IPanel gameUiPanel)
        {
            foreach (var handler in Object.FindObjectsByType<PanelEventHandler>(FindObjectsSortMode.None))
            {
                if (handler.panel != gameUiPanel) continue;
                EventSystem.current.SetSelectedGameObject(handler.gameObject);
                return;
            }
            Assert.Fail("GameUi のパネルの PanelEventHandler が見つからない");
        }
    }
}
```

- [ ] **Step 2: コンパイルし、テストが通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.EditModeInPlayingTest.Ui.BuildMenu.BuildMenuTabOpensInventoryTest" --timeout-seconds 900`
Expected: ErrorCount 0 / 1 件 PASS。
「Unity is reloading (Domain Reload in progress)」が出たら 45 秒待って再実行する。

- [ ] **Step 3: 修正前のコードでこのテストが失敗することを確かめる（赤の確認）**

`GameUiInputInstaller.Initialize` の `GameUiPanelRootNavigationGuard.Attach(root.panel.visualTree);` を一時的にコメントアウトし、コンパイルして同じテストを実行する。
Expected: FAIL（`Tab でインベントリが開いていない（フォーカス: … 'build-menu-search' …）` か、入力中の旗の検査で落ちる）。
失敗を確認したらコメントアウトを戻し、コンパイルして Step 2 の PASS をもう一度確認する。
FAIL しなかった場合は、このテストは不具合の状態を作れていない。前提の検査（`SelectGameUiPanel` の後に `EventSystem.current.currentSelectedGameObject` が PanelEventHandler の GameObject であること、IMGUI の Tab が届いていること）を `Debug.Log` で確かめて原因を報告する。合格として進めない。再現の実測値は `../moorestech_logs/harness/ui-clarity-kgfsp/tab-focus-repro-report.md` の 5 章 C と F にある。

- [ ] **Step 4: 既存の実ゲーム入力テストが通り続けることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.EditModeInPlayingTest.Ui.GameUiInputTest" --timeout-seconds 900`
Expected: 2 件 PASS（パネルの根で数える「規則より前」の件数が変わらない）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu.meta \
        moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/
git commit -m "test(ui): ビルドメニューでフォーカスが無いときの Tab がインベントリを開くことを実ゲームで確かめる"
```

---

### Task 3: 全ブランチレビュー

- [ ] **Step 1:** 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映がガードの判定（`evt.target != evt.currentTarget`）・付ける先・付ける時点に触れたら、Task 2 の EditModeInPlayingTest を反映後のコードで再実行してから完了とする。

### Task 4: セッション終了可能状態にする

- [ ] **Step 1:** pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する。PR 本文の先頭行に報告ページの URL を張る。PR の報告には次を書く: 原因（`../moorestech_logs/harness/ui-clarity-kgfsp/tab-focus-repro-report.md` の結論）、赤の確認の結果（Task 1 Step 5・Task 2 Step 3）、残り（実際のキーボードでの確認は作者がプレイで行う。確かめる手順: ビルドメニューを開く → 一覧のどこかを1回クリック → Tab → インベントリが開くこと）。
- [ ] **Step 2:** 全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認する。PR を出したら `moores-wt rm build-menu-tab-focus` で作業場所と Editor を畳む。マージはしない。

---

## 判断記録（ADR）

- 設計の出所: この不具合の直しには専用の ADR を作らない（戻しやすい1か所の修正で、ADR の3条件を満たさない）。判断はここに書く。
- **原因: 実測（証拠: `../moorestech_logs/harness/ui-clarity-kgfsp/tab-focus-repro-report.md`。Editor PlayMode で InputSystem と IMGUI の両方へ Tab を注入。条件3つがそろうと 7 回中 7 回 `uiState=BuildMenu / focused=TextField 'build-menu-search' / flag=True`。パネルの根で NavigationMoveEvent を打ち消すと 2 回中 2 回 `uiState=PlayerInventory`、外すと再発）。** 測った経路は注入したキーで、実際のキーボード・ビルドでは測っていない（同報告 6 章）。
- Tab の直しはほかの6件より先に小さな PR で出す。出所: ユーザー裁定 2026-11-20 原文「ビルドメニューでtabキーを押したらインベントリが開くようにしてほしい」、場面の問いへの赤「検索窓に入力が吸われることがある」→ 問い「Tab の直しを、ほかの6件より先に小さな PR で出しますか」→ 選択「A. Tab だけ先に出す (PR は2本)」（`.decisions/2026-11-20-Tabの直しはほかの6件より先に小さなPRで出す.md`。ブランチ `feat/ui-clarity-rocket-cleanroom-dicing` にある）
- 置く場所を決めている間の Tab は変えない。出所: agent前提（原文は「ビルドメニューで」。2026-11-20 の仕分けシートで「そのまま通す項目」として提示し、異論なし）
- 検索欄に入力している間の Tab は変えない（今も1回でインベントリが開く）。出所: agent前提（実測で今の挙動を確認。2026-11-20 の2枚目の仕分けシートに「そのまま通す項目」として載せた。回答は未着）
- ガードはパネルの根宛てのイベントだけを止める（規則一式をパネルの根へ付け替えない）。出所: agent前提（`GameUiInputTest` の `NavigationEventCounter` がパネルの根で規則より前の件数を数える前例。機構比較は「配置と前例」節）
- 打ち消すのは NavigationMove に加えて Submit・Cancel も。出所: agent前提（`GameUiInputRules.Attach` が3種をそろえて打ち消している。GameUi はナビゲーションの決定・取消を使わない）
- ランタイム挙動に触れるので EditModeInPlayingTest（Task 2）を入れる。unityプレイ録画テストの代わり。CI の batchmode で IMGUI のキーが届かない場合、Task 2 は不具合の状態を作れずに通る（Tab がそのままインベントリを開く）ので、CI では落ちない。仕組みの検出は Task 1 の L1 が受け持つ。出所: agent前提
