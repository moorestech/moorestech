# ビルドメニューの Tab が検索欄へ吸われる不具合の修正 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ビルドメニューの一覧を開いた状態で Tab を押すと、検索欄へフォーカスが移らずにインベントリが開くようにする。

**Architecture:** GameUi の入力規則（`GameUiInputRules.Attach`）は UIDocument の根に TrickleDown で付いている。フォーカスが無いとき `PanelEventHandler` は Tab 由来の `NavigationMoveEvent` をパネルの根（`panel.visualTree`。UIDocument の根の親）宛てに送るので、規則を通らず、UITK の既定のフォーカス移動が検索欄へフォーカスを移す。`GameUiInputRules` に「パネルの根宛ての選択移動を、印の付いた画面が出ている間だけ打ち消す」入口を1つ足して起動時にパネルの根へ付け、印（新しい `UiTabFocusEntryBlockMark`）はビルドメニューの画面にだけ付ける。ほかの画面の Tab の動きは変えない。

**Tech Stack:** Unity 6000.3.8f1 / UI Toolkit（C# で組む。UXML なし）/ com.unity.ugui 2.0.0 の `PanelEventHandler` / com.unity.inputsystem 1.18.0 / NUnit（`UiTestHost` の L1、EditModeInPlayingTest）

## Requirements

- R1: ビルドメニュー表示中・パネル内にフォーカス無しの状態で、Tab 由来の選択移動がパネルの根へ届いても、検索欄（`build-menu-search`）へフォーカスが移らず、Tab で UIState が `PlayerInventory` になる。受け入れ基準: Task 2 の L1 テスト1本目と EditModeInPlayingTest が、印を外したコードで失敗し、印のあるコードで通る。
- R2: ビルドメニューが出ていない間は、今までと同じ（パネルの根宛ての選択移動を打ち消さない）。受け入れ基準: Task 1 の L1 テスト2本目と Task 2 の L1 テスト2本目（隠した後は別の入力欄へフォーカスが移る）が通る。
- R3: 要素にフォーカスがあるときの挙動は変えない（要素宛ての選択移動はパネルの根を通り抜け、今までどおり UIDocument の根の規則が打ち消す）。受け入れ基準: Task 1 の L1 テスト3本目と、既存の `Client.Tests.Ui.` 配下・`GameUiInputTest`（パネルの根で「規則より前」の件数を数えている）が通り続ける。
- やらないこと:
  - ビルドメニュー以外の画面（不具合報告フォーム・列車の駅名・モーダルの入力欄・クラフトのボタン）の Tab の動きは変えない。
  - 検索欄に入力している間の Tab（今も1回でインベントリが開く）は変えない。
  - 置く場所を決めている間（`PlaceBlock`）の Tab（今はビルドメニューを開き直す）は変えない。
  - パネルの根宛ての決定・取消（`NavigationSubmitEvent`・`NavigationCancelEvent`）は打ち消さない（フォーカスが無ければ何も起こさず、故障の場面が無い）。
  - 実際のキーボードでの確認は、この PR ではしない。PR の後に作者がプレイで確かめる（原因は Editor 上でキー入力をプログラムから送って確かめた）。PR の報告にこの残りを書く。
  - 同じ依頼のほかの6件（発射台・クリーンルーム・分岐器）は別の PR。

## Global Constraints

- 1ファイル200行以下（`GameUiInputRules.cs` は今 171 行。この plan の追加で 190 行前後になる。200 行を超えたら止めて報告する）。1ディレクトリの .cs は10ファイルまで（`Client.Ui/Foundation/Input/` は 8→9、`Client.Tests/Ui/Input/` は 7→8、`Client.Tests/Ui/Screens/BuildMenu/` は 7→8。`Client.Tests/EditModeInPlayingTest/Ui/` は既に10ファイルなので、新しいテストはサブディレクトリ `BuildMenu/` に置く）。
- コメントは日本語→英語の2行セット。各1行。
- `.meta` は手で作らない。Prefab・シーンは触らない。
- try-catch・partial・`Func<>`・デフォルト引数は使わない。
- 作業場所: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/build-menu-tab-focus`（ブランチ `fix/build-menu-tab-focus`、origin/master 28c483852a 起点）。
- PR はマージしない（moorestech は PR 作成まで）。PR 本文の先頭行は報告ページの URL（`CLAUDE.local.macmini.md`「PR の成果物の載せ先」）。
- 再現の実測は `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/ui-clarity-kgfsp/tab-focus-repro-report.md`（worktree からは `../moorestech_logs/harness/ui-clarity-kgfsp/tab-focus-repro-report.md` でも開ける）。

### 実行環境（Unity の回し方）

Mac mini の Unity Editor は同時に1本までで、今は別のセッション（worktree `dicing-quality-saturation`）が使っている。**この plan は Mac で Editor を起動せず、コンパイルとテストを全部 Windows 機の `~/bin/winunity` で回す。** 他のセッションの Editor は落とさない。

- コンパイル: `git push` してから `~/bin/winunity compile fix/build-menu-tab-focus`
- テスト: `git push` してから `~/bin/winunity test fix/build-menu-tab-focus --class "<クラスの完全名>"` か `--regex "<正規表現>"`（EditModeInPlayingTest も同じ口。長いものは `--timeout 1500`）
- 結果は `~/.cache/winunity/runs/<runId>/`（`summary.json`・`results.xml`・`log-excerpt.txt`）。失敗はこの3つで読む
- ブランチは毎回 push 済みであること（Windows が GitHub から取る）。Windows 機が寝ていれば winunity が起こす。枠が埋まっていればエラーにならず空くまで待つ
- **.meta の持ち帰り**: 新しい .cs とフォルダの .meta は Windows 側の Unity が作る。winunity は .meta を持ち帰らないので、新規ファイルを足したコミットを push → `winunity compile` を流した直後、**次の winunity の実行より前に** scp で取る（次の実行は準備で `git clean -fdq` を流し、未追跡の .meta を消す）。Windows 側の作業場所は `C:/dev/worktrees/moorestech/fix-build-menu-tab-focus`（ブランチ名の `/` を `-` にした名前）。取り忘れて消えたら、もう一度 `winunity compile` を流して取り直す

  ```bash
  scp "winbuild:C:/dev/worktrees/moorestech/fix-build-menu-tab-focus/<repo 相対パス>.meta" "<同じ repo 相対パス>.meta"
  ```
- **赤の確認（修正を外すとテストが落ちることの確認）**: winunity は push 済みのブランチしか回せないので、使い捨てのブランチで行う。手順は Task 2 Step 6 に書いてある。使い捨てブランチは確認の後に消す

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## File Structure

| ファイル | 役割 |
|---|---|
| Create `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/UiTabFocusEntryBlockMark.cs` | 「出ている間、フォーカスが無いときの Tab でフォーカスを入れさせない画面」の印 |
| Modify `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs` | パネルの根宛ての選択移動を、印の付いた画面が出ている間だけ打ち消す入口 `AttachPanelRootGuard` |
| Modify `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs` | 起動時にパネルの根へ付ける |
| Modify `moorestech_client/Assets/Scripts/Client.Ui/Screens/BuildMenu/BuildMenuView.cs` | ビルドメニューの画面に印を付ける |
| Create `moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootGuardTest.cs` | L1: 印の付いた画面が出ている間だけ打ち消す／要素宛ては通す |
| Create `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuTabFocusEntryTest.cs` | L1: ビルドメニューを開いている間は検索欄へ入らない／閉じた後は打ち消さない |
| Create `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/BuildMenuTabOpensInventoryTest.cs` | 実ゲーム: ビルドメニューでフォーカス無しの Tab がインベントリを開く |

## データの流れ

```
Tab キー
 ├─(A) InputSystem → InputManager.UI.OpenInventory → BuildMenuState.GetNextUpdate → PlayerInventory へ遷移（BuildMenuState.cs:52）
 └─(B) IMGUI のイベント → PanelEventHandler.Update（EventSystem がこのパネルを選んでいるときだけ）
        → 宛先 = フォーカス中の要素。無ければ panel.visualTree（PanelEventHandler.cs:308）
        → Tab 由来の NavigationMoveEvent(target = 宛先)（PanelEventHandler.cs:360-375）
        → TrickleDown: パネルの根【新: 宛先がパネルの根で、印の付いた画面が出ていれば打ち消す】→ UIDocument の根【既存: GameUiInputRules.CancelNavigation】→ 宛先
        → 打ち消されなければ UITK の既定のフォーカス移動 → 検索欄にフォーカス
        → GameUiInputRules.OnFocusIn → UiInputExclusivity.SetGameUiTextInputFocused(true) → (A) が止まる（これが不具合）
```

新しい登録の立ち位置は「打ち消し役 `GameUiInputRules` の、届く範囲の穴ふさぎ」。打ち消し役は `GameUiInputRules` の1クラスのままで、打ち消しの本体 `CancelNavigation` も1つのまま使う。どの画面で効かせるかは画面側が印で申告し、基盤は画面の名前を知らない。

## 配置と前例

- 打ち消しの置き場は既存の `GameUiInputRules`。同クラスの remarks が「全画面に共通するキー・フォーカスの規則はここに登録を足す」と決めている（`GameUiInputRules.cs:12-13`）。
- 付ける側は既存の `GameUiInputInstaller.Initialize`。installer の `Initialize` で `root.panel.visualTree` に部品を付ける前例は `Client.Ui/Foundation/Tooltip/GameUiHoverTooltipInstaller.cs:25-31`。新しい DI 登録は足さない。
- 画面が基盤へ申告する印の前例は、同じディレクトリの `UiKeyFocusKeepMark`（`CraftRecipeEntryView.cs:51` が付け、`GameUiInputRules.MayKeepFocus` が読む）と `WheelPassthroughMark`（`HotbarHudView.cs:46` が付ける）。新しい印も「画面が付け、`GameUiInputRules` が読む」同じ形。
- 画面が出ているかは、印の付いた要素のインラインの表示（`UiDisplay.IsShownInline`。`Client.Ui/Foundation/Layout/UiDisplay.cs:20-23`）で読む。`BuildMenuView.SetShown` は `UiDisplay.SetShown(this, …)` で出し入れしている（`BuildMenuView.cs:60, 94, 103`）ので、表示の状態を別に持たない。

### 直し方の比較

| 案 | 中身 | 判定 |
|---|---|---|
| **A（採用）: パネルの根で、宛先がパネルの根の選択移動を、印の付いた画面が出ている間だけ打ち消す** | `GameUiInputRules.AttachPanelRootGuard` ＋ `UiTabFocusEntryBlockMark`。印は `BuildMenuView` だけ | 再現の計測で、パネルの根で NavigationMoveEvent を打ち消すと直ることを確かめてある（2回中2回。外すと再発）。ほかの画面は変わらない |
| A2: 印を使わず、どの画面でも打ち消す | A から印の判定を除いた形 | 今回は採らない。依頼に無い画面の動き（フォーカス無しの Tab で入力欄やクラフトのボタンにフォーカスが入る。コード上は今できる）が変わる |
| B: `BuildMenuView.SetShown` で、出すたびにパネルの根へ登録し、隠すたびに外す | 印を作らない | 採らない。登録するコールバックは static で、同じものの登録は1つにまとまる。2つ目の画面が同じ形を使うと、片方が隠れたときの取り外しで両方の分が外れる。表示の状態を「表示そのもの」と「登録の有無」の2か所で持つことにもなる |
| C: 発生源で止める（シーンの `EventSystem.sendNavigationEvents` を偽にし、Tab 由来の NavigationMoveEvent を作らせない） | 前例: UI 専用の入力アクション資産で Navigate・Submit・Cancel を発生源から外している | 採らない。`sendNavigationEvents` はシーンの EventSystem の設定で、uGUI と全画面に効く（依頼に無い画面の動きが変わる）。入力欄にフォーカスがある間の Tab がこの設定でどう変わるかも測っていない |
| D: 既定のフォーカス移動に手を付けず、ビルドメニューの中の要素を Tab の順番から外す（`tabIndex` を負にする） | 既存の機構を止めない | 採らない。ビルドメニュー表示中にフォーカスを取れる要素は検索欄の3個（`BuildMenuPanelSkeleton.cs:45` の `GameTextField` とその内側）のほかに、2つのスクロール領域の UITK 内部の部品が6個ある（再現報告 3 章）。全部を1つずつ外す形になり、外し漏れた要素・後から足した要素へ Tab でフォーカスが入ると、入力欄なら同じ不具合が戻り、入力欄でなければ `GameUiInputRules.OnFocusIn` が警告を出して外す（`GameUiInputRules.cs:57-67`）。A は中身が増えても届いた所の1か所で止まる |

### 同じ仕組みにぶら下がる操作の死活表

| 操作 | plan の後 | 根拠 |
|---|---|---|
| ビルドメニュー・フォーカス無しの Tab → インベントリが開く | 直る | 印の付いた画面が出ているので、宛先がパネルの根の選択移動を打ち消す |
| ビルドメニュー・フォーカス無しの Tab／Shift+Tab で検索欄にフォーカスが入る | 止まる（これが不具合そのもの） | 同上。検索欄はクリックで使う |
| 検索欄に入力中の Tab → インベントリが開く | 変わらない | 宛先は検索欄の内側の要素で、パネルの根は通す |
| `PlaceBlock` 中の Tab → ビルドメニューを開き直す | 変わらない | InputSystem の経路。ビルドメニューは隠れていて、印の付いた画面が出ていない |
| 入力欄にフォーカス中の ESC、フォーカス無しの ESC・B・右短押し | 変わらない | KeyDown と InputSystem の経路。打ち消すのは NavigationMoveEvent だけ |
| ほかの画面で、フォーカス無しの Tab で入力欄・クラフトのボタンにフォーカスが入る（不具合報告フォーム・列車の駅名・モーダルの入力欄・クラフト画面） | 変わらない | 印の付いた画面が出ていないので、パネルの根は通す（今までどおり） |
| 入力欄のクリック、ツールチップ、モーダルの入力欄の自動フォーカス、録画 DSL のキー注入 | 変わらない | ポインタの経路・`Focus()` の直接呼び出し・フォーカス中の要素宛て |

死ぬ操作は無い。

---

### Task 1: 印と、パネルの根宛ての選択移動の打ち消し

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/UiTabFocusEntryBlockMark.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs:8-11,23-32`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootGuardTest.cs`

**Interfaces:**
- Consumes: `GameUiRoot.GetRootElement()`（UIDocument の根。`root.panel.visualTree` がパネルの根）、既存の `private static void GameUiInputRules.CancelNavigation(EventBase evt)`、`public static bool UiDisplay.IsShownInline(VisualElement element)`（`Client.Ui.Foundation.Layout`）
- Produces:
  - `public static void UiTabFocusEntryBlockMark.Mark(VisualElement screenRoot)`（Task 2 が `BuildMenuView` で使う）
  - `public static bool UiTabFocusEntryBlockMark.IsAnyMarkedScreenShown(VisualElement panelRoot)`
  - `public static void GameUiInputRules.AttachPanelRootGuard(VisualElement panelRoot)`（Task 2 のテストが使う）

- [ ] **Step 1: L1 テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootGuardTest.cs`:

```csharp
using Client.Input;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layout;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// パネルの根へ届く Tab 由来の選択移動を、印の付いた画面が出ている間だけ打ち消し、要素宛ては通すことを実パネルで確かめる（L1）
    /// Verifies on a real panel that a Tab-driven navigation move reaching the panel root is cancelled only while a marked screen is shown, and element-aimed moves pass through (L1)
    /// </summary>
    public class GameUiPanelRootGuardTest : UiTestHost
    {
        private VisualElement _markedScreen;
        private TextField _fieldInMarkedScreen;
        private TextField _fieldInOtherScreen;
        private int _movesBelowPanelRoot;

        [SetUp]
        public void AttachRulesGuardAndTwoScreens()
        {
            // パネルの根のすぐ下に、打ち消しを通り抜けた選択移動を数える器を置く
            // Right under the panel root, place a container that counts navigation moves that got past the guard
            _movesBelowPanelRoot = 0;
            var belowPanelRoot = new VisualElement { name = "guard-below-panel-root", pickingMode = PickingMode.Ignore };
            belowPanelRoot.StretchToParentSize();
            belowPanelRoot.RegisterCallback<NavigationMoveEvent>(_ => _movesBelowPanelRoot++, TrickleDown.TrickleDown);
            rootVisualElement.Add(belowPanelRoot);

            // 本番と同じ組み合わせ: 規則は UIDocument の根に当たる器へ、打ち消しはパネルの根へ付ける
            // Same pairing as production: the rules on a container standing in for the UIDocument root, the guard on the panel root
            var rulesRoot = new VisualElement { name = "guard-rules-root", pickingMode = PickingMode.Ignore };
            rulesRoot.StretchToParentSize();
            belowPanelRoot.Add(rulesRoot);
            GameUiInputRules.Attach(rulesRoot);
            GameUiInputRules.AttachPanelRootGuard(rootVisualElement.panel.visualTree);

            // 印の付いた画面と、印の無い別の画面に、入力欄を1つずつ置く
            // Put one text field in a marked screen and one in another, unmarked screen
            _markedScreen = new VisualElement { name = "guard-marked-screen" };
            UiTabFocusEntryBlockMark.Mark(_markedScreen);
            _fieldInMarkedScreen = AddTextField(_markedScreen, "guard-marked-text");
            rulesRoot.Add(_markedScreen);
            var otherScreen = new VisualElement { name = "guard-other-screen" };
            _fieldInOtherScreen = AddTextField(otherScreen, "guard-other-text");
            rulesRoot.Add(otherScreen);
            simulate.FrameUpdate();
        }

        [Test]
        public void 印の付いた画面が出ている間はフォーカスが無いときのTab由来の移動でフォーカスが入らない()
        {
            Assert.IsNull(panel.focusController.focusedElement);

            SendTabMoveAimedAt(rootVisualElement.panel.visualTree);

            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void 印の付いた画面が隠れている間は今までどおり入力欄へフォーカスが移る()
        {
            UiDisplay.SetShown(_markedScreen, false);
            simulate.FrameUpdate();

            SendTabMoveAimedAt(rootVisualElement.panel.visualTree);

            // 打ち消さないので、出ている方の入力欄へ既定のフォーカス移動が起きる（上のテストが空振りでない証拠でもある）
            // Not cancelled, so the default focus move lands in the shown field (also proving the test above is not vacuous)
            var focused = panel.focusController.focusedElement as VisualElement;
            Assert.IsTrue(focused != null && (focused == _fieldInOtherScreen || _fieldInOtherScreen.Contains(focused)), $"focused: {focused?.name}");
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void 入力欄にフォーカスがあるときの移動はパネルの根を通り抜けて規則が止める()
        {
            _fieldInMarkedScreen.Focus();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            var focusedBefore = panel.focusController.focusedElement;

            // PanelEventHandler と同じく、宛先をフォーカス中の要素にして送る
            // Aim the event at the focused element, as PanelEventHandler does
            SendTabMoveAimedAt(focusedBefore);

            // パネルの根は通し（下の器まで1件届く）、UIDocument の根の規則が打ち消すのでフォーカスは動かない
            // The panel root lets it through (one reaches the container below) and the root rules cancel it, so focus stays put
            Assert.AreEqual(1, _movesBelowPanelRoot);
            Assert.AreSame(focusedBefore, panel.focusController.focusedElement);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }

        // PanelEventHandler.SendTabEvent と同じく、宛先を決めてからパネルへ送る
        // Set the target first and send to the panel, exactly as PanelEventHandler.SendTabEvent does
        private void SendTabMoveAimedAt(Focusable target)
        {
            using (var tab = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next))
            {
                tab.target = target;
                rootVisualElement.panel.visualTree.SendEvent(tab);
            }
            simulate.FrameUpdate();
        }

        private static TextField AddTextField(VisualElement parent, string name)
        {
            var textField = new TextField { name = name };
            textField.style.width = 300;
            parent.Add(textField);
            return textField;
        }
    }
}
```

- [ ] **Step 2: 印を書く**

`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/UiTabFocusEntryBlockMark.cs`:

```csharp
using Client.Ui.Foundation.Layout;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// 出ている間、フォーカスが無いときの Tab でパネル内の要素へフォーカスを入れさせない画面の印（GameUiInputRules のパネルの根の打ち消しが読む）
    /// Mark for a screen that, while shown, must not let Tab move focus into the panel when nothing is focused (read by the GameUiInputRules panel-root guard)
    /// </summary>
    /// <remarks>
    /// 拡張点: Tab をゲームのキーとして使い、入力欄も持つ画面の根に 1 つ付ける。画面の出し入れは UiDisplay.SetShown で行うこと
    /// Extension: put one on the root of a screen that uses Tab as a game key and also has a text field; show and hide that screen through UiDisplay.SetShown
    /// </remarks>
    public static class UiTabFocusEntryBlockMark
    {
        private const string ClassName = "ui-tab-focus-entry-block";

        public static void Mark(VisualElement screenRoot)
        {
            screenRoot.AddToClassList(ClassName);
        }

        public static bool IsAnyMarkedScreenShown(VisualElement panelRoot)
        {
            // 画面は UiDisplay のインライン none で隠すので、印の付いた要素自身の出し入れだけを読む
            // Screens are hidden with UiDisplay's inline none, so only the marked element's own shown state is read
            foreach (var screenRoot in panelRoot.Query(className: ClassName).ToList())
            {
                if (UiDisplay.IsShownInline(screenRoot)) return true;
            }
            return false;
        }
    }
}
```

- [ ] **Step 3: `GameUiInputRules` に入口を足す**

`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs` を3か所変える。

(1) クラスの remarks（12〜13 行目）を次に置き換える:

```csharp
    /// <remarks>
    /// 拡張点: 全画面に共通するキー・フォーカスの規則はここの Attach に登録を足す（パネルの根宛てに届くものは AttachPanelRootGuard に足す）。画面固有のキー処理（←→・Enter 等）は各画面の Presenter が持つ
    /// Extension: add panel-wide key/focus rules as registrations in Attach (those aimed at the panel root go in AttachPanelRootGuard); screen-specific keys (arrows, Enter, ...) belong to each screen's presenter
    /// </remarks>
```

(2) `Attach` メソッドの閉じ括弧（34 行目）のすぐ下に、次のメソッドを足す:

```csharp

        // フォーカスが無い間、Tab 由来の選択移動は Attach 先の根ではなくパネルの根宛てに届き、上の規則を通らないので、パネルの根でも受ける
        // With nothing focused, a Tab-driven navigation move is aimed at the panel root, not the Attach root, and bypasses the rules above, so also listen on the panel root
        public static void AttachPanelRootGuard(VisualElement panelRoot)
        {
            panelRoot.RegisterCallback<NavigationMoveEvent>(CancelNavigationAimedAtPanelRoot, TrickleDown.TrickleDown);
        }
```

(3) `CancelNavigation` メソッド（146〜151 行目）のすぐ上に、次のメソッドを足す:

```csharp
        private static void CancelNavigationAimedAtPanelRoot(NavigationMoveEvent evt)
        {
            // silent-ok: 要素宛ての選択移動は Attach 先の根の CancelNavigation が打ち消すので、ここは通す / 規則を越えた件数 0 を実ゲームの GameUiInputTest が確かめる
            // silent-ok: a move aimed at an element is cancelled by CancelNavigation on the Attach root, so let it pass here / the in-game GameUiInputTest asserts none gets past the rules
            if (evt.target != evt.currentTarget) return;

            // silent-ok: 印の付いた画面が出ていない間は今までどおり既定のフォーカス移動に任せる / 移った先は OnFocusIn が入力中の旗か警告で知らせる
            // silent-ok: with no marked screen shown the default focus move runs as before / OnFocusIn reports where it landed through the text-input flag or a warning
            if (!UiTabFocusEntryBlockMark.IsAnyMarkedScreenShown((VisualElement)evt.currentTarget)) return;
            CancelNavigation(evt);
        }

```

変更後に `wc -l` で 200 行以下であることを確かめる。

- [ ] **Step 4: 起動時にパネルの根へ付ける**

`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs` の `Initialize` を次の形にする（`GameUiInputRules.Attach(root);` の次に3行足す）:

```csharp
        public void Initialize()
        {
            var root = _gameUiRoot.GetRootElement();

            // パネル未接続では Pick も入力も届かないので起動の段階で止める
            // Without an attached panel neither Pick nor input works, so stop at startup
            if (root.panel == null) throw new InvalidOperationException("[GameUiInput] GameUi root is not attached to a panel");
            GameUiInputRules.Attach(root);

            // フォーカスが無い間の Tab 由来の選択移動はパネルの根宛てに届くので、パネルの根にも付ける
            // A Tab-driven navigation move with nothing focused is aimed at the panel root, so attach there too
            GameUiInputRules.AttachPanelRootGuard(root.panel.visualTree);
            UiInputExclusivity.SetGameUiPointerProbe(_pointerProbe);
        }
```

クラスの summary（8〜11 行目）も合わせて直す:

```csharp
    /// <summary>
    /// GameUi のルートとパネルの根へ入力規則を付け、Pick の probe を入力排他へ登録する
    /// Attaches the input rules to the GameUi root and the panel root, and registers the Pick probe with the input exclusivity
    /// </summary>
```

- [ ] **Step 5: コミットして push し、コンパイルして .meta を持ち帰る**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/UiTabFocusEntryBlockMark.cs \
        moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs \
        moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootGuardTest.cs
git commit -m "feat(ui): 印の付いた画面が出ている間、パネルの根へ届く Tab 由来の選択移動を打ち消す"
git push -u origin fix/build-menu-tab-focus
~/bin/winunity compile fix/build-menu-tab-focus
```

Expected: `summary.json` でコンパイルエラー 0。
続けて、次の winunity の実行より前に .meta を2つ取ってコミットする:

```bash
W=winbuild:C:/dev/worktrees/moorestech/fix-build-menu-tab-focus
for F in moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/UiTabFocusEntryBlockMark.cs \
         moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootGuardTest.cs; do
  scp "$W/$F.meta" "$F.meta" && git add "$F.meta"
done
git commit -m "chore: Unity が生成した .meta を追加" && git push
```

- [ ] **Step 6: L1 テストが通ることを確認する**

Run: `~/bin/winunity test fix/build-menu-tab-focus --class "Client.Tests.Ui.Input.GameUiPanelRootGuardTest"`
Expected: 3 件 PASS。
2本目（隠れている間はフォーカスが移る）が通ることが、1本目（出ている間は入らない）が空振りでない証拠になる。2本目が FAIL する（打ち消していないのにフォーカスが移らない）場合は、テストがこの不具合の状態を作れていない。止めて報告する（合格にして進めない）。

---

### Task 2: ビルドメニューに印を付け、L1 と実ゲームで確かめる

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BuildMenu/BuildMenuView.cs:1-11,37-39`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuTabFocusEntryTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/BuildMenuTabOpensInventoryTest.cs`

**Interfaces:**
- Consumes: Task 1 の `UiTabFocusEntryBlockMark.Mark(VisualElement)`・`GameUiInputRules.AttachPanelRootGuard(VisualElement)`。既存の `BuildMenuScreenTestHost`（`View`・`OpenMenu(BuildMenuViewModel)`・`CloseMenu()`・`Find<T>(VisualElement, string)`。`Client.Tests.Ui.Screens.BuildMenu.Support`）、`BuildMenuSamples.Create()`、`EditModeInPlayingTestUtil.EnterPlayModeUtil()`・`LoadMainGame()`、`UiStateTransitionWait.TransitionTo(UIStateControl, UIStateEnum)` と `EditorBackgroundInputScope.Open()`（どちらも名前空間 `Client.Tests.EditModeInPlayingTest.Ui`。新しいテストはその子の名前空間なので using は要らない）、`SemanticInput.TapKey(Key)`（`Client.Playtest.Input`）、`GameUiRoot.Instance.GetRootElement()`
- Produces: なし

実ゲームのテストのキーの送り方: UITK 側は、`PanelEventHandler` が Tab で作るのと同じ `NavigationMoveEvent`（宛先はパネルの根）をテストが直接送る。ゲーム側は InputSystem の Tab を `SemanticInput.TapKey` で送る。IMGUI のキー（`EditorGUIUtility.QueueGameViewInputEvent`）は使わない（batchmode では Game View が無く届かないため、届かないと不具合の状態を作れずに通ってしまう）。

- [ ] **Step 1: L1 テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuTabFocusEntryTest.cs`:

```csharp
using Client.Input;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu
{
    /// <summary>
    /// ビルドメニューを開いている間だけ、フォーカスが無いときの Tab 由来の選択移動でフォーカスが入らないことを確かめる（L1）
    /// Verifies that only while the build menu is open, a Tab-driven navigation move with nothing focused cannot move focus in (L1)
    /// </summary>
    public class BuildMenuTabFocusEntryTest : BuildMenuScreenTestHost
    {
        private TextField _otherScreenField;

        [SetUp]
        public void AttachRulesGuardAndOtherScreenField()
        {
            // 規則は画面の要素へ（前例 BuildMenuSearchInputGuardTest）、打ち消しは本番と同じくパネルの根へ付ける
            // Rules go on the screen element (precedent BuildMenuSearchInputGuardTest); the guard goes on the panel root as in production
            GameUiInputRules.Attach(View);
            GameUiInputRules.AttachPanelRootGuard(rootVisualElement.panel.visualTree);

            // ビルドメニューの外にある、別の画面の入力欄の代わり
            // Stands in for a text field of another screen, outside the build menu
            _otherScreenField = new TextField { name = "other-screen-text" };
            _otherScreenField.style.width = 300;
            rootVisualElement.Add(_otherScreenField);
            OpenMenu(BuildMenuSamples.Create());
        }

        [Test]
        public void 開いている間はフォーカスが無いときのTab由来の移動で検索欄へフォーカスが入らない()
        {
            Assert.IsNull(panel.focusController.focusedElement);

            SendTabMoveAimedAtPanelRoot();

            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void 閉じた後は打ち消さず別の画面の入力欄へフォーカスが移る()
        {
            CloseMenu();

            SendTabMoveAimedAtPanelRoot();

            // ビルドメニューが隠れている間は今までどおり。出ている入力欄へ既定のフォーカス移動が起きる
            // While the build menu is hidden nothing changes: the default focus move lands in the shown field
            var focused = panel.focusController.focusedElement as VisualElement;
            Assert.IsTrue(focused != null && (focused == _otherScreenField || _otherScreenField.Contains(focused)), $"focused: {focused?.name}");
        }

        // PanelEventHandler.SendTabEvent と同じく、宛先をパネルの根にして送る
        // Aim the event at the panel root, exactly as PanelEventHandler.SendTabEvent does
        private void SendTabMoveAimedAtPanelRoot()
        {
            var panelRoot = rootVisualElement.panel.visualTree;
            using (var tab = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next))
            {
                tab.target = panelRoot;
                panelRoot.SendEvent(tab);
            }
            Settle();
        }
    }
}
```

- [ ] **Step 2: 実ゲームのテストを書く**

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
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.BuildMenu
{
    /// <summary>
    /// 実ゲームで、ビルドメニュー表示中・フォーカス無しの Tab が検索欄へ吸われずインベントリを開くことを確かめる
    /// Verifies in the real game that Tab opens the inventory instead of being swallowed by the search field while the build menu is shown and nothing is focused
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
                Assert.IsNotNull(panel.visualTree.Q<TextField>("build-menu-search"), "前提: ビルドメニューの検索欄がパネルに載っている");
                Assert.IsNull(panel.focusController.focusedElement, "前提: パネル内にフォーカスが無い");
                Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "前提: 入力中の旗が立っていない");

                // フォーカスが無いときに PanelEventHandler が Tab で送るのと同じ選択移動（宛先はパネルの根）を送る
                // Send the same navigation move PanelEventHandler sends for Tab with nothing focused (aimed at the panel root)
                using (var tabMove = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next))
                {
                    tabMove.target = panel.visualTree;
                    panel.visualTree.SendEvent(tabMove);
                }
                await UniTask.DelayFrame(2);
                var focusedAfterMove = panel.focusController.focusedElement as VisualElement;
                Assert.IsNull(focusedAfterMove, $"Tab 由来の選択移動でフォーカスが移った: {focusedAfterMove?.GetType().Name} '{focusedAfterMove?.name}'");
                Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "Tab 由来の選択移動で入力中の旗が立った（検索欄へ吸われた）");

                // ゲームが読む InputSystem の Tab でインベントリが開く
                // The InputSystem Tab the game reads opens the inventory
                await SemanticInput.TapKey(Key.Tab);
                for (var i = 0; i < InventoryWaitFrameLimit && uiStateControl.CurrentState != UIStateEnum.PlayerInventory; i++) await UniTask.Yield();
                Assert.AreEqual(UIStateEnum.PlayerInventory, uiStateControl.CurrentState, "Tab でインベントリが開いていない");
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: ビルドメニューの画面に印を付ける**

`moorestech_client/Assets/Scripts/Client.Ui/Screens/BuildMenu/BuildMenuView.cs` を2か所変える。

(1) using に1行足す（`using Client.Ui.Content;` の次）:

```csharp
using Client.Ui.Foundation.Input;
```

(2) コンストラクタの `pickingMode = PickingMode.Ignore;`（39 行目）のすぐ下に足す:

```csharp

            // Tab はここではインベントリを開くキーなので、出ている間はフォーカスが無いときの Tab で検索欄へフォーカスを入れさせない
            // Tab opens the inventory here, so while shown do not let Tab move focus into the search field when nothing is focused
            UiTabFocusEntryBlockMark.Mark(this);
```

- [ ] **Step 4: コミットして push し、コンパイルして .meta を持ち帰る**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BuildMenu/BuildMenuView.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuTabFocusEntryTest.cs \
        moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/BuildMenuTabOpensInventoryTest.cs
git commit -m "fix(ui): ビルドメニューを開いている間、Tab が検索欄へ吸われずインベントリを開くようにする"
git push
~/bin/winunity compile fix/build-menu-tab-focus
```

Expected: コンパイルエラー 0。続けて、次の winunity の実行より前に .meta を3つ取ってコミットする:

```bash
W=winbuild:C:/dev/worktrees/moorestech/fix-build-menu-tab-focus
D=moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu
for F in moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuTabFocusEntryTest.cs \
         "$D" "$D/BuildMenuTabOpensInventoryTest.cs"; do
  scp "$W/$F.meta" "$F.meta" && git add "$F.meta"
done
git commit -m "chore: Unity が生成した .meta を追加" && git push
```

- [ ] **Step 5: テストが通ることを確認する**

Run:
- `~/bin/winunity test fix/build-menu-tab-focus --class "Client.Tests.Ui.Screens.BuildMenu.BuildMenuTabFocusEntryTest"` → Expected: 2 件 PASS
- `~/bin/winunity test fix/build-menu-tab-focus --class "Client.Tests.EditModeInPlayingTest.Ui.BuildMenu.BuildMenuTabOpensInventoryTest" --timeout 1500` → Expected: 1 件 PASS

- [ ] **Step 6: 印を外すとテストが落ちることを確かめる（赤の確認）**

使い捨てのブランチで、ビルドメニューの印だけを外して同じ2つのテストを回す:

```bash
git switch -c tmp/tab-red-check
```

`BuildMenuView` の `UiTabFocusEntryBlockMark.Mark(this);` の1行をコメントアウトし、

```bash
git commit -am "tmp: 赤の確認（ビルドメニューの印を外す）"
git push -u origin tmp/tab-red-check
~/bin/winunity test tmp/tab-red-check --class "Client.Tests.Ui.Screens.BuildMenu.BuildMenuTabFocusEntryTest"
~/bin/winunity test tmp/tab-red-check --class "Client.Tests.EditModeInPlayingTest.Ui.BuildMenu.BuildMenuTabOpensInventoryTest" --timeout 1500
git switch fix/build-menu-tab-focus
git branch -D tmp/tab-red-check && git push origin --delete tmp/tab-red-check
~/bin/winunity rm tmp-tab-red-check
```

Expected:
- L1 の `開いている間はフォーカスが無いときのTab由来の移動で検索欄へフォーカスが入らない` が FAIL（`focusedElement` が検索欄か、その内側の要素になる）。もう1本は PASS のまま
- 実ゲームのテストが FAIL（`Tab 由来の選択移動でフォーカスが移った: … 'build-menu-search' …` か、その内側の要素の名前）

FAIL しなかったテストは、不具合の状態を作れていない。失敗メッセージと `results.xml` を添えて止めて報告する（合格として進めない）。再現の実測値は再現報告の 2 章・3 章にある。

- [ ] **Step 7: 既存のテストが通り続けることを確認する**

Run:
- `~/bin/winunity test fix/build-menu-tab-focus --regex "Client\.Tests\.Ui\.|BuildMenuStateTextInputGuardTest" --timeout 1500`
- `~/bin/winunity test fix/build-menu-tab-focus --class "Client.Tests.EditModeInPlayingTest.Ui.GameUiInputTest" --timeout 1500`

Expected: 全件 PASS（`GameUiInputTest` は 2 件。パネルの根で数える「規則より前」の件数が変わらない）。この PR の前から落ちているテストがあれば、origin/master の同じテストの結果と並べて報告に書く。新しく落ちたものがあれば止めて原因を調べる。

---

### Task 3: 全ブランチレビュー

- [ ] **Step 1:** 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映が打ち消しの判定（宛先の判定・印の付いた画面が出ているかの判定）・付ける先・付ける時点に触れたら、Task 2 の EditModeInPlayingTest を反映後のコードで再実行してから完了とする。

### Task 4: セッション終了可能状態にする

- [ ] **Step 1:** pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する。PR 本文の先頭行に報告ページの URL を張る。PR の報告には次を書く: 原因（再現報告の結論）、赤の確認の結果（Task 2 Step 6）、直したのはビルドメニューだけで、ほかの画面の同じ動き（コード上は起きる。未確認）は触っていないこと、残り（実際のキーボードでの確認は作者がプレイで行う。確かめる手順: ビルドメニューを開く → 一覧のどこかを1回クリック → Tab → インベントリが開くこと）。
- [ ] **Step 2:** 全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認する。使い捨てブランチが origin に残っていないことを `git ls-remote --heads origin 'tmp/tab-red-check*'` で確かめる。PR を出したら `moores-wt rm build-menu-tab-focus` で作業場所を畳む。マージはしない。

---

## 判断記録（ADR）

- 設計の出所: この不具合の直しには専用の ADR を作らない（戻しやすい修正で、ADR の3条件を満たさない）。判断はここに書く。関連する既存の ADR は `docs/adr/0101-uitk-switch-revises-web-premised-adrs-and-rulings.md`（GameUi は `GameUiInputRules` で選択移動を打ち消す）。この plan は同じ向きで、矛盾しない。
- **原因: 実測（証拠: `../moorestech_logs/harness/ui-clarity-kgfsp/tab-focus-repro-report.md`。Editor PlayMode で InputSystem と IMGUI の両方へ Tab を注入。条件3つ〈EventSystem が GameUi のパネルを選択済み・パネル内にフォーカス無し・ビルドメニュー表示中〉がそろうと 7 回中 7 回 `uiState=BuildMenu / focused=TextField 'build-menu-search' / flag=True`。パネルの根で NavigationMoveEvent を打ち消すと 2 回中 2 回 `uiState=PlayerInventory`、外すと再発）。** 測った経路は注入したキーで、実際のキーボード・ビルドでは測っていない（同報告 6 章）。
- Tab の直しはほかの6件より先に小さな PR で出す。出所: ユーザー裁定 2026-11-20 原文「ビルドメニューでtabキーを押したらインベントリが開くようにしてほしい」、場面の問いへの赤「検索窓に入力が吸われることがある」→ 問い「Tab の直しを、ほかの6件より先に小さな PR で出しますか」→ 選択「A. Tab だけ先に出す (PR は2本)」（`.decisions/2026-11-20-Tabの直しはほかの6件より先に小さなPRで出す.md`。ブランチ `feat/ui-clarity-rocket-cleanroom-dicing` にある）
- 効かせるのはビルドメニューを開いている間だけ。ほかの画面の Tab の動きは変えない。出所: agent前提（原文は「ビルドメニューで」。依頼に無い画面の既存の動きは変えない〈moores-grill-with-docs §1.6〉。2026-11-20 の仕分けシート 04 に「そのまま通す項目」e9 として載せ、ほかの画面もまとめて止めたいときの見直しの口を付けた。回答は未着で、見直しが付いたら後続の PR で印を外す形〈比較表の A2〉に広げる）
- 置く場所を決めている間の Tab は変えない。出所: agent前提（原文は「ビルドメニューで」。2026-11-20 の仕分けシート 03 で「そのまま通す項目」として提示し、異論なし）
- 検索欄に入力している間の Tab は変えない（今も1回でインベントリが開く）。出所: agent前提（実測で今の挙動を確認。仕分けシート 04 に「そのまま通す項目」e7 として載せた。回答は未着）
- 直し方は案 A（パネルの根の打ち消し＋画面の印）。出所: agent前提（機構の比較は「直し方の比較」節。パネルの根での打ち消しだけが実測で効果を確かめてある。印は `UiKeyFocusKeepMark`・`WheelPassthroughMark` の前例の形）
- 打ち消すのは NavigationMoveEvent だけ。出所: agent前提（パネルの根宛ての決定・取消はフォーカスが無ければ何も起こさず、故障の場面を作れない）
- 自動テストは IMGUI の実キー経路（`PanelEventHandler` が Tab から選択移動を作る所）を通さない。実ゲームのテストは `PanelEventHandler` と同じ形のイベントを直接送る。`PanelEventHandler` がパネルの根宛てに送ることは、パッケージのソース（`PanelEventHandler.cs:308, 360-375`）と再現報告の計測が根拠。出所: agent前提（batchmode と CI で IMGUI のキーが届かず、届かないとテストが不具合の状態を作れずに通るため）
- ランタイム挙動に触れるので EditModeInPlayingTest（Task 2）を入れる。unityプレイ録画テストの代わり。出所: agent前提
- Mac の Editor を使わず winunity で回す。出所: agent前提（Mac mini の Editor は同時1本で、別のセッションが使用中。`CLAUDE.local.macmini.md`「運用上の注意」）
