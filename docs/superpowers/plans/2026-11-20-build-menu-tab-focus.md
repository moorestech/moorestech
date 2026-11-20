# ビルドメニューの Tab が検索欄へ吸われる不具合の修正 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ビルドメニューの一覧を開いた状態で Tab を押すと、検索欄へフォーカスが移らずにインベントリが開くようにする。

**Architecture:** GameUi の入力規則（`GameUiInputRules.Attach`）は UIDocument の根に TrickleDown で付いている。フォーカスが無いとき `PanelEventHandler` は Tab 由来の `NavigationMoveEvent` をパネルの根（`panel.visualTree`。UIDocument の根の親）宛てに送るので、規則を通らず、UITK の既定のフォーカス移動が検索欄へフォーカスを移す。ゲーム起動時は `GameUiInputRules.AttachToGameUi(documentRoot)` が文書へ既存の規則を登録し、パネルの根へその根宛ての選択移動を打ち消すハンドラを登録する。要素宛ての選択移動は今までどおり UIDocument の根の規則が打ち消す。

**Tech Stack:** Unity 6000.3.8f1 / UI Toolkit（C# で組む。UXML なし）/ com.unity.ugui 2.0.0 の `PanelEventHandler` / com.unity.inputsystem 1.18.0 / NUnit（`UiTestHost` の L1、EditModeInPlayingTest）

## Requirements

- R1: ビルドメニュー表示中・パネル内にフォーカス無しの状態で、Tab 由来の選択移動がパネルの根へ届いても、検索欄（`build-menu-search`）へフォーカスが移らず、Tab で UIState が `PlayerInventory` になる。受け入れ基準: Task 2 の EditModeInPlayingTest が、修正前のコードで失敗し修正後に通る。
- R2: フォーカスが無いときにパネルの根へ届いた `NavigationMoveEvent`（Tab・Shift+Tab 由来）は、どの画面でもフォーカスを動かさない（範囲が全画面である根拠は、末尾の判断記録の「範囲」）。受け入れ基準: Task 1 の L1 テスト1本目が、修正前のコードで失敗し修正後に通る。
- R3: 要素にフォーカスがあるときの挙動は変えない（要素宛ての選択移動はパネルの根の打ち消しを通り抜け、今までどおり UIDocument の根の規則が打ち消す）。受け入れ基準: Task 1 の L1 テスト2本目と、既存の `Client.Tests.Ui.` 配下・`GameUiInputTest`（パネルの根で「規則より前」の件数を数えている）が通り続ける。
- R4: `docs/adr/0101-uitk-switch-revises-web-premised-adrs-and-rulings.md` の裁定の読み替え表に、`2026-08-22-ブラウザのTabフォーカス移動は封じる` の行がある。受け入れ基準: Task 1 Step 4 の grep が 1 を返す。
- やらないこと:
  - 検索欄に入力している間の Tab（今も1回でインベントリが開く）は変えない。
  - 置く場所を決めている間（`PlaceBlock`）の Tab（今はビルドメニューを開き直す）は変えない。
  - パネルの根宛ての決定・取消（`NavigationSubmitEvent`・`NavigationCancelEvent`）は打ち消さない（フォーカスが無ければ何も起こさず、故障の場面が無い）。
  - 実際のキーボードでの確認は、この PR ではしない。PR の後に作者がプレイで確かめる（原因は Editor 上でキー入力をプログラムから送って確かめた）。PR の報告にこの残りを書く。
  - 同じ依頼のほかの6件（発射台・クリーンルーム・分岐器）は別の PR。
  - 画面ごとに打ち消しを切り替えない（ビルドメニューを開いている間だけ効かせる形にしない。判断記録の「範囲」）。

## Global Constraints

- 1ファイル200行以下（`GameUiInputRules.cs` は今 171 行。この plan の追加で 190 行前後になる。200 行を超えたら止めて報告する）。1ディレクトリの .cs は10ファイルまで（`Client.Tests/EditModeInPlayingTest/Ui/` は既に10ファイルなので、新しいテストはサブディレクトリ `BuildMenu/` に置く）。
- コメントは日本語→英語の2行セット。各1行。
- `.meta` は手で作らない。Prefab・シーンは触らない。
- try-catch・partial・`Func<>`・デフォルト引数は使わない。
- 作業場所: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/build-menu-tab-focus`（ブランチ `fix/build-menu-tab-focus`、origin/master 28c483852a 起点）。
- **この plan のコマンドの塊はすべて、先頭で作業場所へ `cd` してから流す**（`cd /Users/sakastudio/hermes-agent/data/worktrees/moorestech/build-menu-tab-focus && …`）。シェルは呼ぶたびに起動したフォルダ（メインのクローン）へ戻るので、`cd` を省くと `scp`・`git add`・`git commit`・`git push` がメインのクローンの master に対して走る。各塊の最初に `git rev-parse --abbrev-ref HEAD` が `fix/build-menu-tab-focus`（赤の確認の間は `tmp/tab-red-check-*`）を返すことを確かめる。subagent へ渡す依頼文にも同じことを書く。
- PR はマージしない（moorestech は PR 作成まで）。PR 本文の先頭行は報告ページの URL（`CLAUDE.local.macmini.md`「PR の成果物の載せ先」）。
- 再現の実測は `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/ui-clarity-kgfsp/tab-focus-repro-report.md`（worktree からは `../moorestech_logs/harness/ui-clarity-kgfsp/tab-focus-repro-report.md` でも開ける）。

### 実行環境（Unity の回し方）

Mac mini の Unity Editor は同時に1本までで、今は別のセッション（worktree `dicing-quality-saturation`）が使っている。**この plan は Mac で Editor を起動せず、コンパイルとテストを全部 Windows 機の `~/bin/winunity` で回す。** 他のセッションの Editor は落とさない。

- コンパイル: `git push` してから `~/bin/winunity compile fix/build-menu-tab-focus`
- テスト: `git push` してから `~/bin/winunity test fix/build-menu-tab-focus --class "<クラスの完全名>"` か `--regex "<正規表現>"`（EditModeInPlayingTest も同じ口。制限時間は既定の 1800 秒のまま使う）。batchmode で回る。実ゲームのテストが、テストの中身でなく環境の都合（グラフィックスの初期化の警告など）で落ちたときは、同じコマンドに `--editor` を付けて GUI Editor で回し直し、どちらで回したかを報告に書く
- 結果は `~/.cache/winunity/runs/<runId>/`（`summary.json`・`results.xml`・`log-excerpt.txt`）。失敗はこの3つで読む
- ブランチは毎回 push 済みであること（Windows が GitHub から取る）。Windows 機が寝ていれば winunity が起こす。枠が埋まっていればエラーにならず空くまで待つ
- **.meta の持ち帰り**: 新しい .cs とフォルダの .meta は Windows 側の Unity が作る。winunity は .meta を持ち帰らないので、新規ファイルを足したコミットを push → `winunity compile` を流した直後、**次の winunity の実行より前に** scp で取る（次の実行は準備で `git clean -fdq` を流し、未追跡の .meta を消す）。Windows 側の作業場所は `C:/dev/worktrees/moorestech/fix-build-menu-tab-focus`（ブランチ名の `/` を `-` にした名前）。取り忘れて消えたら、もう一度 `winunity compile` を流して取り直す

  ```bash
  scp "winbuild:C:/dev/worktrees/moorestech/fix-build-menu-tab-focus/<repo 相対パス>.meta" "<同じ repo 相対パス>.meta"
  ```
- **赤の確認（修正を外すとテストが落ちることの確認）**: winunity は push 済みのブランチしか回せないので、使い捨てのブランチで行う。手順は各タスクのステップに書いてある。使い捨てブランチは確認の後に消す。回すときは必ず `--name fix-build-menu-tab-focus` を付け、Windows 側の今の作業場所を使い回す（付けないとブランチごとに新しい作業場所ができ、Library 約 80GB を複製する。順番に回すので衝突しない）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 死活表の「止まる」2行は 2026-08-22 の裁定の原文で覆われると独立に判定。注目点（remarks の引用の欠け・Shift+Tab の表記）を反映（報告: `../moorestech_logs/harness/ui-clarity-kgfsp/tab-plan-v4-arch-review-phase1-2_5.md`）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / 発火なし。備考1件（裁定の「stopPropagation はしない」の読み分け）を ADR 0101 に足す行と判断記録へ反映（報告: `../moorestech_logs/harness/ui-clarity-kgfsp/tab-plan-v4-arch-review-phase2_6.md`）

---

## File Structure

| ファイル | 役割 |
|---|---|
| Modify `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs` | パネルの根宛ての選択移動を打ち消す入口 `AttachToGameUi` を足す |
| Modify `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs:30` | 起動時にパネルの根へ付ける |
| Modify `docs/adr/0101-uitk-switch-revises-web-premised-adrs-and-rulings.md`（裁定の表） | 2026-08-22 の Tab の裁定が UITK のどこで保たれるかを 1 行足す |
| Create `moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootGuardTest.cs` | L1: フォーカス無しでパネルの根へ届いた選択移動がフォーカスを動かさない／要素宛ては通す |
| Create `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/BuildMenuTabOpensInventoryTest.cs` | 実ゲーム: ビルドメニューでフォーカス無しの Tab がインベントリを開く |

本番コードに新しいファイルは無い（.meta が要るのはテスト2本とフォルダ `BuildMenu` の3つ）。

## データの流れ

```
Tab キー
 ├─(A) InputSystem → InputManager.UI.OpenInventory → BuildMenuState.GetNextUpdate → PlayerInventory へ遷移（BuildMenuState.cs:52）
 └─(B) IMGUI のイベント → PanelEventHandler.Update（EventSystem がこのパネルを選んでいるときだけ）
        → 宛先 = フォーカス中の要素。無ければ panel.visualTree（PanelEventHandler.cs:308）
        → Tab 由来の NavigationMoveEvent(target = 宛先)（PanelEventHandler.cs:360-375）
        → TrickleDown: パネルの根【新: 宛先がパネルの根のものだけ打ち消す】→ UIDocument の根【既存: GameUiInputRules.CancelNavigation】→ 宛先
        → 打ち消されなければ UITK の既定のフォーカス移動 → 検索欄にフォーカス
        → GameUiInputRules.OnFocusIn → UiInputExclusivity.SetGameUiTextInputFocused(true) → (A) が止まる（これが不具合）
```

新しい登録の立ち位置は「打ち消し役 `GameUiInputRules` の、届く範囲の穴ふさぎ」。打ち消し役は `GameUiInputRules` の1クラスのままで、打ち消しの本体 `CancelNavigation` も1つのまま使う。

## 配置と前例

- この直しは新しい決まりを足すものではない。`GameUiInputRules` は、要素にフォーカスがあるときの Tab 由来の選択移動を既に全画面で打ち消している（`Attach` が登録する `CancelNavigation`）。フォーカスが無いときだけ宛先がパネルの根になって規則を通らない穴があり、それをふさぐ。Tab でフォーカスを動かさない決まりの出所は 2026-08-22 のユーザー裁定（`.decisions/2026-08-22-ブラウザのTabフォーカス移動は封じる.md`）。
- 置き場は既存の `GameUiInputRules`。同クラスの remarks が「全画面に共通するキー・フォーカスの規則はここの Attach に登録を足す」と決めている（`GameUiInputRules.cs:12-13`）。パネルの根宛てのものは `Attach` の中では登録できない（比較表の B）。文書とパネル双方への登録はゲーム起動用の内部メソッドにまとめる。
- 付ける側は既存の `GameUiInputInstaller.Initialize`。installer の `Initialize` で `root.panel.visualTree` に部品を付ける前例は `Client.Ui/Foundation/Tooltip/GameUiHoverTooltipInstaller.cs:25-31`。新しい DI 登録は足さない。

### 直し方の比較

| 案 | 中身 | 判定 |
|---|---|---|
| **A（採用）: パネルの根で、宛先がパネルの根の選択移動だけ打ち消す** | `GameUiInputRules.AttachToGameUi(documentRoot)` を足し、installer から付ける。打ち消しの本体は既存の `CancelNavigation` | 再現の計測で、パネルの根で NavigationMoveEvent を打ち消すと直ることを確かめてある（2回中2回。外すと再発） |
| B: `GameUiInputRules.Attach(root)` の中で `root.panel.visualTree` にも登録する | 入口を増やさない | 採らない。`Attach` はテスト 17 か所から、テストごとの部分木に規則を付けるために呼ばれている。中でパネルの根にも登録すると、渡された部分木の外にまでテストの作用が広がる |
| C: 発生源で止める（シーンの `EventSystem.sendNavigationEvents` を偽にし、Tab 由来の NavigationMoveEvent を作らせない） | 前例: UI 専用の入力アクション資産で Navigate・Submit・Cancel を発生源から外している | 採らない。`sendNavigationEvents` はシーンの EventSystem の設定で、uGUI と全パネルに効く。GameUi の決まり（選択移動を使わない）がコード（`GameUiInputRules`）とシーンの設定の2か所に分かれ、GameUi を別のシーンで使うと設定を忘れても気づけない。入力欄にフォーカスがある間の Tab（今は1回でインベントリが開く）がこの設定でどう変わるかも測っていない |
| D: 既定のフォーカス移動に手を付けず、入力欄を Tab の順番から外す（`tabIndex` を負にする） | 既存の機構を止めない | 成り立たない。ビルドメニュー表示中にフォーカスを取れる要素は検索欄の3個のほかにスクロールバーの部品が6個ある（再現報告 3 章）。入力欄を外しても Tab はスクロールバーの部品へフォーカスを移し、`GameUiInputRules.OnFocusIn` が警告を出して外す動きになる。入力欄を足すたびに外し忘れも起きる。2026-08-22 の裁定が棄却した案「全ボタンへ `tabIndex={-1}` を付ける（新規ボタンのたびに付け忘れが起きる）」と同じ形でもある |
| E: ビルドメニューを開いている間だけ打ち消す（ビルドメニューの画面に印を付け、パネルの根の打ち消しが「印の付いた画面が表示中か」を見る） | 依頼に無い画面の動きを変えない | 採らない。2026-08-22 の裁定が棄却した案「`activeLayer` でレイヤーごとに封殺を切り替える（modal内でだけTabを生かす価値がなく分岐が増える）」と同じ形。ほかの画面に同じ不具合（Tab が入力欄へ吸われる）を残す |
| （付け替え）規則一式を UIDocument の根からパネルの根へ移す | 規則が1か所に付く | 採らない。`GameUiInputTest` の `NavigationEventCounter` がパネルの根の TrickleDown で「規則より前」の件数を数えている（`NavigationEventCounter.cs:38-47`）。規則が同じ要素に先に付くと数えられなくなる |

### 同じ仕組みにぶら下がる操作の死活表

| 操作 | plan の後 | 根拠 |
|---|---|---|
| ビルドメニュー・フォーカス無しの Tab → インベントリが開く | 直る | 宛先がパネルの根の選択移動を打ち消すので、検索欄へフォーカスが移らない |
| 検索欄に入力中の Tab → インベントリが開く | 変わらない | 宛先は検索欄の内側の要素で、パネルの根の打ち消しは通す |
| `PlaceBlock` 中の Tab → ビルドメニューを開き直す | 変わらない | InputSystem の経路で、UITK の選択移動を通らない |
| 入力欄にフォーカス中の ESC（値を保ってフォーカスだけ外す）、フォーカス無しの ESC・B・右短押し | 変わらない | KeyDown と InputSystem の経路。打ち消すのは NavigationMoveEvent だけ |
| クラフトのボタンをクリックした後の Enter 長押し、ボタンにフォーカス中の Tab（フォーカスが動かない） | 変わらない | 宛先はボタンで、パネルの根の打ち消しは通す（要素宛てを通すことは L1 の2本目と Task 1 Step 7 の2つ目の赤の確認が守る。ボタンでフォーカスが動かないことは `CraftHoldKeyTest.cs:78-92` が規則の側で守る） |
| 入力欄のクリック、ツールチップ、モーダルの入力欄の自動フォーカス、録画 DSL のキー注入 | 変わらない | ポインタの経路・`Focus()` の直接呼び出し・フォーカス中の要素宛て |
| フォーカス無しの Tab で入力欄にフォーカスが入る（不具合報告フォーム・列車の駅名） | **止まる**（コード上は今できる。実際に押しては未確認） | ビルドメニューと同じ仕組み。入力欄はクリックで使う。Shift+Tab も同じ。2026-08-22 の裁定の決定「`Tab` を無条件に `preventDefault` し、ブラウザnativeのフォーカス移動を封じる」が覆い、代償「Mantine modalのfocus trapとbuild menu検索欄へのフォーカス移動が失われる。いずれもクリックで到達できる」と同じ向き |
| フォーカス無しの Tab でクラフトのボタンにフォーカスが入り、Enter 長押しでクラフトする | **止まる**（コード上は今できる。実際に押しては未確認） | ボタンは印付きでフォーカスを保てる（`CraftRecipeEntryView.cs:50-51`）。Tab からの入口が無くなる。クリックした後の Enter 長押しは変わらない。2026-08-22 の裁定の理由「native Tabがフォーカスを動かし `:focus-visible` のリングで別ボタンが選択中に見えていた」と、棄却案に書かれた「Enter/Spaceの誤爆」を止める向き |

「止まる」2行は、2026-08-22 のユーザー裁定が既に決めている代償と、その裁定が止めようとした動きである（対応する句を各行に引いた）。その裁定を UITK に当てはめる読み替えは agent 前提なので、PR の報告の注目点に書く（判断記録の「範囲」）。

---

### Task 1: パネルの根宛ての選択移動の打ち消しと L1 テスト

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs:8-11,23-32`
- Modify: `docs/adr/0101-uitk-switch-revises-web-premised-adrs-and-rulings.md`（裁定の表に1行）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootGuardTest.cs`

**Interfaces:**
- Consumes: `GameUiRoot.GetRootElement()`（UIDocument の根。`root.panel.visualTree` がパネルの根）、既存の `private static void GameUiInputRules.CancelNavigation(EventBase evt)`
- Produces: `internal static void GameUiInputRules.AttachToGameUi(VisualElement documentRoot)`（Task 2 は起動済みのゲームで間接的に使う）

- [ ] **Step 1: L1 テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootGuardTest.cs`:

```csharp
using Client.Input;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// 実パネルで検証（L1）：根宛てはフォーカス移動を抑止、要素宛ては通過
    /// On a real panel (L1), root-targeted moves preserve focus and element-targeted moves pass the guard.
    /// </summary>
    public class GameUiPanelRootGuardTest : UiTestHost
    {
        private VisualElement _belowPanelRoot;
        private TextField _textField;
        private int _movesBelowPanelRoot;

        [SetUp]
        public void AttachRulesGuardAndTextField()
        {
            // ガード通過件数を根の直下で測る
            // Count moves just below the root after they pass the guard.
            _movesBelowPanelRoot = 0;
            _belowPanelRoot = new VisualElement { name = "guard-below-panel-root", pickingMode = PickingMode.Ignore };
            _belowPanelRoot.StretchToParentSize();
            _belowPanelRoot.RegisterCallback<NavigationMoveEvent>(_ => _movesBelowPanelRoot++, TrickleDown.TrickleDown);
            rootVisualElement.Add(_belowPanelRoot);

            // 本番と同じ組み合わせ: 規則は UIDocument の根に当たる器へ、打ち消しはパネルの根へ付ける
            // Same pairing as production: the rules on a container standing in for the UIDocument root, the guard on the panel root
            var rulesRoot = new VisualElement { name = "guard-rules-root", pickingMode = PickingMode.Ignore };
            rulesRoot.StretchToParentSize();
            _belowPanelRoot.Add(rulesRoot);
            GameUiInputRules.AttachToGameUi(rulesRoot);

            _textField = new TextField { name = "guard-text" };
            _textField.style.width = 300;
            rulesRoot.Add(_textField);
            simulate.FrameUpdate();
        }

        [TestCase(NavigationMoveEvent.Direction.Next)]
        [TestCase(NavigationMoveEvent.Direction.Previous)]
        public void NavigationMoveAimedAtPanelRootWithNothingFocusedDoesNotFocusTextField(NavigationMoveEvent.Direction direction)
        {
            Assert.IsNull(panel.focusController.focusedElement);

            // PanelEventHandler.SendTabEvent と同じく、宛先をパネルの根にして送る
            // Aim the event at the panel root, exactly as PanelEventHandler.SendTabEvent does
            var panelRoot = rootVisualElement.panel.visualTree;
            using (var tab = NavigationMoveEvent.GetPooled(direction))
            {
                tab.target = panelRoot;
                panelRoot.SendEvent(tab);
            }
            simulate.FrameUpdate();

            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void NavigationMoveAimedAtFocusedElementPassesPanelRootAndIsCancelledByRules()
        {
            _textField.Focus();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);

            // PanelEventHandler と同じく、宛先をフォーカス中の要素にして送る
            // Aim the event at the focused element, as PanelEventHandler does
            var focusedBefore = (VisualElement)panel.focusController.focusedElement;
            var movesAtTarget = 0;
            focusedBefore.RegisterCallback<NavigationMoveEvent>(_ => movesAtTarget++, TrickleDown.TrickleDown);
            using (var tab = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next))
            {
                tab.target = focusedBefore;
                rootVisualElement.panel.visualTree.SendEvent(tab);
            }
            simulate.FrameUpdate();

            // パネルの根は通し（下の器まで1件届く）、UIDocument の根の規則が打ち消すのでフォーカスは動かない
            // The panel root lets it through (one reaches the container below) and the root rules cancel it, so focus stays put
            Assert.AreEqual(1, _movesBelowPanelRoot);
            Assert.AreEqual(0, movesAtTarget);
            Assert.AreSame(focusedBefore, panel.focusController.focusedElement);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }
    }
}
```

このクラスに「打ち消しを付けないとフォーカスが移る」ことを見る対照のテストを足さない。テスト用のパネルはテストクラスごとに1枚で、パネルの根に付けたコールバックは後片付けで消えず、同じクラスの後のテストにも効くため（`EditorUITestFixture` の後片付けは子要素・クラス・スタイルシートだけを消す）。対照は Step 7 の赤の確認で取る。

- [ ] **Step 2: `GameUiInputRules` に入口を足す**

`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs` で部分木用の公開入口 `Attach` を保ち、接続検証と文書・パネルへの登録をこのゲーム用入口にまとめる。

```csharp
        internal static void AttachToGameUi(VisualElement documentRoot)
        {
            // 入力を受け取れない状態でゲームが起動するのを防ぐ
            // Reject startup when the document cannot receive input.
            if (documentRoot.panel == null) throw new InvalidOperationException("[GameUiInput] GameUi root is not attached to a panel");
            Attach(documentRoot);

            // フォーカスが無いときの選択移動は文書の外のパネルの根に届く
            // Without focus, navigation targets the panel root outside the document.
            documentRoot.panel.visualTree.RegisterCallback<NavigationMoveEvent>(CancelNavigationAimedAtPanelRoot, TrickleDown.TrickleDown);

            #region Internal

            static void CancelNavigationAimedAtPanelRoot(NavigationMoveEvent evt)
            {
                // silent-ok: 要素宛ての選択移動は Attach 先の根の CancelNavigation が打ち消すので、ここは通す / 規則を越えた件数 0 を実ゲームの GameUiInputTest が確かめる
                // silent-ok: a move aimed at an element is cancelled by CancelNavigation on the Attach root, so let it pass here / the in-game GameUiInputTest asserts none gets past the rules
                if (evt.target != evt.currentTarget) return;
                CancelNavigation(evt);
            }

            #endregion
        }
```

変更後に `wc -l` で 200 行以下であることを確かめる。

- [ ] **Step 3: 起動時にパネルの根へ付ける**

`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs` の `Initialize` からゲーム用入口を呼ぶ。

```csharp
        public void Initialize()
        {
            GameUiInputRules.AttachToGameUi(_gameUiRoot.GetRootElement());
            UiInputExclusivity.SetGameUiPointerProbe(_pointerProbe);
        }
```

- [ ] **Step 4: ADR 0101 の裁定の表に 2026-08-22 の裁定の行を足す**

`docs/adr/0101-uitk-switch-revises-web-premised-adrs-and-rulings.md` の `### 裁定（`.decisions/`）` の表で、`| [[2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる]] |` で始まる行のすぐ上に、次の1行を足す（表の行なので途中で折り返さない）:

```markdown
| [[2026-08-22-ブラウザのTabフォーカス移動は封じる]] | 読み替え | `Client.Ui/Foundation/Input/GameUiInputRules.cs`（`Attach` が要素宛ての `NavigationMoveEvent` を、`AttachToGameUi` がフォーカスの無いときにパネルの根宛てに届くものを、TrickleDown で打ち消す） | 「Tab でフォーカスを動かさない。画面ごとに切り替えない」は UITK の根の入力規則で保たれる。代償（Tab で入力欄へ入れない。クリックで到達できる）も同じ。裁定の「`stopPropagation` はしない」は Web の keydown を Unity へ届けるための話で、UITK では選択移動のイベントそのものを止める（ゲームは InputSystem から Tab を読むので影響しない）。`tabIndex`・Mantine の focus trap の話は Web と一緒に消える |
```

確認: `grep -c "2026-08-22-ブラウザのTabフォーカス移動は封じる" docs/adr/0101-uitk-switch-revises-web-premised-adrs-and-rulings.md` が 1 を返す。

- [ ] **Step 5: コミットして push し、コンパイルして .meta を持ち帰る**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputRules.cs \
        moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/GameUiInputInstaller.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootGuardTest.cs \
        docs/adr/0101-uitk-switch-revises-web-premised-adrs-and-rulings.md
git commit -m "fix(ui): フォーカスが無い間にパネルの根へ届く選択移動を打ち消し、Tab が検索欄へ吸われないようにする"
git push -u origin fix/build-menu-tab-focus
~/bin/winunity compile fix/build-menu-tab-focus
```

Expected: `summary.json` でコンパイルエラー 0。
続けて、次の winunity の実行より前に .meta を取ってコミットする:

```bash
F=moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/GameUiPanelRootGuardTest.cs
scp "winbuild:C:/dev/worktrees/moorestech/fix-build-menu-tab-focus/$F.meta" "$F.meta"
git add "$F.meta" && git commit -m "chore: Unity が生成した .meta を追加" && git push
```

- [ ] **Step 6: L1 テストが通ることを確認する**

Run: `~/bin/winunity test fix/build-menu-tab-focus --class "Client.Tests.Ui.Input.GameUiPanelRootGuardTest"`
Expected: 3 件 PASS（Next・Previous・要素宛て）

- [ ] **Step 7: テストがこの不具合を検出することを確かめる（赤の確認）**

使い捨てのブランチで、パネルの根への登録だけを外して同じテストを回す:

```bash
git switch -c tmp/tab-red-check-l1
```

`GameUiInputRules.AttachToGameUi` の中の `documentRoot.panel.visualTree.RegisterCallback<NavigationMoveEvent>(...)` の1行をコメントアウトし、

```bash
git commit -am "tmp: 赤の確認（パネルの根の打ち消しを外す）"
git push -u origin tmp/tab-red-check-l1
~/bin/winunity test tmp/tab-red-check-l1 --name fix-build-menu-tab-focus --class "Client.Tests.Ui.Input.GameUiPanelRootGuardTest"
git switch fix/build-menu-tab-focus
git branch -D tmp/tab-red-check-l1 && git push origin --delete tmp/tab-red-check-l1
```

Expected: `NavigationMoveAimedAtPanelRootWithNothingFocusedDoesNotFocusTextField` の Next・Previous が FAIL（フォーカスが `guard-text` に移る）。要素宛ては PASS。
FAIL しなかった場合は、テストが不具合を検出できていない。止めて報告する（合格にして進めない）。

次に、宛先の判定がテストで守られていることも同じ形で確かめる: 使い捨てブランチ `tmp/tab-red-check-l1b` で `CancelNavigationAimedAtPanelRoot` の `if (evt.target != evt.currentTarget) return;` の1行をコメントアウトして同じクラスを回す（手順は上と同じで、ブランチ名だけ変える。`--name fix-build-menu-tab-focus` は同じ）。
Expected: `NavigationMoveAimedAtFocusedElementPassesPanelRootAndIsCancelledByRules` が FAIL（下の器へ届く件数が 0）。

- [ ] **Step 8: 既存の UI テストが通り続けることを確認する**

Run: `~/bin/winunity test fix/build-menu-tab-focus --regex "Client\.Tests\.Ui\.|BuildMenuStateTextInputGuardTest"`
Expected: 全件 PASS（この PR の前から落ちているテストがあれば、origin/master の同じテストの結果と並べて報告に書く。新しく落ちたものがあれば止めて原因を調べる）

---

### Task 2: 実ゲームで、ビルドメニューのフォーカス無しの Tab がインベントリを開くことを確かめる

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/BuildMenuTabOpensInventoryTest.cs`

**Interfaces:**
- Consumes: Task 1 の修正（起動時に `GameUiInputInstaller` がパネルの根へ打ち消しを付ける）。既存の `EditModeInPlayingTestUtil.EnterPlayModeUtil()`・`LoadMainGame()`、`UiStateTransitionWait.TransitionTo(UIStateControl, UIStateEnum)` と `EditorBackgroundInputScope.Open()`（どちらも名前空間 `Client.Tests.EditModeInPlayingTest.Ui`。新しいテストはその子の名前空間なので using は要らない）、`SemanticInput.TapKey(Key)`（`Client.Playtest.Input`）、`GameUiRoot.Instance.GetRootElement()`
- Produces: なし

キーの送り方: UITK 側は、`PanelEventHandler` が Tab で作るのと同じ `NavigationMoveEvent`（宛先はパネルの根）をテストが直接送る。ゲーム側は InputSystem の Tab を `SemanticInput.TapKey` で送る。IMGUI のキー（`EditorGUIUtility.QueueGameViewInputEvent`）は使わない（batchmode では Game View が無く届かないため、届かないと不具合の状態を作れずに通ってしまう）。

- [ ] **Step 1: テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/BuildMenuTabOpensInventoryTest.cs`:

```csharp
using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest.Input;
using Client.Playtest.UiElements.GameUi;
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
    /// 実ゲームでのビルドメニュー入力を検証
    /// Checks build-menu input in the running game.
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
                LogAssert.ignoreFailingMessages = false;
                var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
                var panel = GameUiRoot.Instance.GetRootElement().panel;

                // 背面の Editor でも注入が届く設定にしてから、ビルドメニューを開く
                // Let injection reach a background Editor, then open the build menu
                using var backgroundInput = EditorBackgroundInputScope.Open();
                await UiStateTransitionWait.TransitionTo(uiStateControl, UIStateEnum.BuildMenu);
                await UniTask.DelayFrame(3);

                // 検索欄はメニューが閉じていてもパネルに載っているので、自分と祖先がすべて表示中であることを前提として確かめる
                // The search field stays in the panel even while the menu is closed, so assert it and every ancestor are displayed
                Assert.AreEqual(UIStateEnum.BuildMenu, uiStateControl.CurrentState, "前提: ビルドメニューの状態になっている");
                var search = panel.visualTree.Q<TextField>("build-menu-search");
                Assert.IsNotNull(search, "前提: ビルドメニューの検索欄がパネルに載っている");
                Assert.IsTrue(GameUiElementQuery.IsVisible(search), "前提: 検索欄が表示中");
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

                // ゲーム側はInputSystem経由
                // The game reads Tab through the Input System.
                await SemanticInput.TapKey(Key.Tab);
                for (var i = 0; i < InventoryWaitFrameLimit && uiStateControl.CurrentState != UIStateEnum.PlayerInventory; i++) await UniTask.Yield();
                Assert.AreEqual(UIStateEnum.PlayerInventory, uiStateControl.CurrentState, "Tab でインベントリが開いていない");
            }

            #endregion
        }
    }
}
```

- [ ] **Step 2: コミットして push し、コンパイルして .meta を持ち帰る**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu/BuildMenuTabOpensInventoryTest.cs
git commit -m "test(ui): ビルドメニューでフォーカスが無いときの Tab がインベントリを開くことを実ゲームで確かめる"
git push
~/bin/winunity compile fix/build-menu-tab-focus
```

Expected: コンパイルエラー 0。続けて、次の winunity の実行より前に .meta を2つ取ってコミットする:

```bash
D=moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/BuildMenu
scp "winbuild:C:/dev/worktrees/moorestech/fix-build-menu-tab-focus/$D.meta" "$D.meta"
scp "winbuild:C:/dev/worktrees/moorestech/fix-build-menu-tab-focus/$D/BuildMenuTabOpensInventoryTest.cs.meta" "$D/BuildMenuTabOpensInventoryTest.cs.meta"
git add "$D.meta" "$D/BuildMenuTabOpensInventoryTest.cs.meta" && git commit -m "chore: Unity が生成した .meta を追加" && git push
```

- [ ] **Step 3: テストが通ることを確認する**

Run: `~/bin/winunity test fix/build-menu-tab-focus --class "Client.Tests.EditModeInPlayingTest.Ui.BuildMenu.BuildMenuTabOpensInventoryTest"`
Expected: 1 件 PASS

- [ ] **Step 4: 修正前のコードでこのテストが失敗することを確かめる（赤の確認）**

```bash
git switch -c tmp/tab-red-check-play
```

`GameUiInputRules.AttachToGameUi` 内の `documentRoot.panel.visualTree.RegisterCallback<NavigationMoveEvent>(...);` の1行をコメントアウトし、

```bash
git commit -am "tmp: 赤の確認（起動時の打ち消しの取り付けを外す）"
git push -u origin tmp/tab-red-check-play
~/bin/winunity test tmp/tab-red-check-play --name fix-build-menu-tab-focus --class "Client.Tests.EditModeInPlayingTest.Ui.BuildMenu.BuildMenuTabOpensInventoryTest"
git switch fix/build-menu-tab-focus
git branch -D tmp/tab-red-check-play && git push origin --delete tmp/tab-red-check-play
```

Expected: FAIL（`Tab 由来の選択移動でフォーカスが移った: … 'build-menu-search' …` か、その内側の要素の名前）。
FAIL しなかった場合は、このテストは不具合の状態を作れていない。失敗メッセージと `results.xml` を添えて止めて報告する（合格として進めない）。再現の実測値は再現報告の 2 章・3 章にある。

- [ ] **Step 5: 既存の実ゲーム入力テストが通り続けることを確認する**

Run: `~/bin/winunity test fix/build-menu-tab-focus --class "Client.Tests.EditModeInPlayingTest.Ui.GameUiInputTest"`
Expected: 2 件 PASS（パネルの根で数える「規則より前」の件数が変わらない）

---

### Task 3: 全ブランチレビュー

- [ ] **Step 1:** 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映が打ち消しの判定（`evt.target != evt.currentTarget`）・付ける先・付ける時点に触れたら、Task 2 の EditModeInPlayingTest を反映後のコードで再実行してから完了とする。

### Task 4: セッション終了可能状態にする

- [ ] **Step 1:** pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する。PR 本文の先頭行に報告ページの URL を張る。PR の報告には次を書く: 原因（再現報告の結論）、赤の確認の結果（Task 1 Step 7・Task 2 Step 4）、ビルドメニュー以外の画面で変わること（死活表の「止まる」2行）、注目点（Web UI 前提の 2026-08-22 の裁定を UITK に当てはめたのは実装側の読み替えであること。ADR 0101 の表に足した行もその読み替え。違っていれば差し戻してもらう）、検証範囲（自動テストはイベント直接送信と InputSystem の Tab。実キーボード・配布ビルドは未確認。GUI の Editor でなら再現報告 4.3 章と同じ注入で測れる見込みだが、確かめていない。確かめる手順: ビルドメニューを開く → 一覧のどこかを1回クリック → Tab を1回押す → 検索欄にフォーカスが移らずインベントリが開くこと）。
- [ ] **Step 2:** 全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認する。使い捨てブランチ（`tmp/tab-red-check-*`）が origin に残っていないことを `git ls-remote --heads origin 'tmp/tab-red-check-*'` で確かめる。PR を出したら `moores-wt rm build-menu-tab-focus` で作業場所を畳む。マージはしない。

---

## 判断記録（ADR）

- 設計の出所: この不具合の直しには専用の ADR を作らない（戻しやすい1か所の修正で、ADR の3条件を満たさない）。判断はここに書く。関連する既存の ADR は `docs/adr/0101-uitk-switch-revises-web-premised-adrs-and-rulings.md`（GameUi は `GameUiInputRules` で選択移動を打ち消す）。この plan は同じ向きの穴をふさぐもので、矛盾しない。同 ADR の裁定の表に 1 行足す（Task 1 Step 4）。
- **原因: 実測（証拠: `../moorestech_logs/harness/ui-clarity-kgfsp/tab-focus-repro-report.md`。Editor PlayMode で InputSystem と IMGUI の両方へ Tab を注入。条件3つ〈EventSystem が GameUi のパネルを選択済み・パネル内にフォーカス無し・ビルドメニュー表示中〉がそろうと 7 回中 7 回 `uiState=BuildMenu / focused=TextField 'build-menu-search' / flag=True`。パネルの根で NavigationMoveEvent を打ち消すと 2 回中 2 回 `uiState=PlayerInventory`、外すと再発）。** 測った経路は注入したキーで、実際のキーボード・ビルドでは測っていない（同報告 6 章）。
- Tab の直しはほかの6件より先に小さな PR で出す。出所: ユーザー裁定 2026-11-20 原文「ビルドメニューでtabキーを押したらインベントリが開くようにしてほしい」、場面の問いへの赤「検索窓に入力が吸われることがある」→ 問い「Tab の直しを、ほかの6件より先に小さな PR で出しますか」→ 選択「A. Tab だけ先に出す (PR は2本)」（`.decisions/2026-11-20-Tabの直しはほかの6件より先に小さなPRで出す.md`。ブランチ `feat/ui-clarity-rocket-cleanroom-dicing` にある）
- **範囲は全画面（フォーカスが無いときにパネルの根へ届く選択移動を、どの画面でも打ち消す）。** 出所は二段に分かれる。
  - 一段目（決まりそのもの）: ユーザー裁定 2026-08-22 原文「決定: `useWebInputExclusivity` のcaptureフェーズkeydownで `Tab` を無条件に `preventDefault` し、ブラウザnativeのフォーカス移動を封じる。`stopPropagation` はしない。」／棄却案の原文「全ボタンへ `tabIndex={-1}` を付ける（新規ボタンのたびに付け忘れが起きる）／`activeLayer` でレイヤーごとに封殺を切り替える（modal内でだけTabを生かす価値がなく分岐が増える）／CSSで `:focus-visible` を消す（見た目は直るがEnter/Spaceの誤爆とUnityキーボード黙殺は残る）。」／代償の原文「Mantine modalのfocus trapとbuild menu検索欄へのフォーカス移動が失われる。いずれもクリックで到達できる。」（`.decisions/2026-08-22-ブラウザのTabフォーカス移動は封じる.md`）。2026-10-01 の裁定も「Tabと同じく、Web UI全体で…封じる」と、Tab が UI 全体で封じられていることを前提にしている（`.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md`）。
  - 二段目（UITK への当てはめ）: agent前提。2026-08-22 の裁定は Web UI 前提で、ADR 0101 の読み替え表に行が無い。当てはめの根拠は2つ。(1) UITK の `GameUiInputRules.Attach` は既に、要素宛ての `NavigationMoveEvent` を全画面で打ち消している（`GameUiInputRules.cs` の `CancelNavigation`）。(2) ADR 0101 は、同じ並びの 2026-10-01 の Space の裁定を、同じ `GameUiInputRules` への読み替えとして載せている。この plan は ADR 0101 の表に 2026-08-22 の行を足す（Task 1 Step 4）。行の「読み替え」という分類も agent前提で、PR の報告の注目点に書く。裁定の「`stopPropagation` はしない」は Web の keydown を Unity 側へ届けるための但し書きで、UITK の `CancelNavigation` が選択移動のイベントを `StopImmediatePropagation` で止めることとは対象が違う（ゲームは InputSystem から Tab を読む。Task 2 のテストが Tab でインベントリが開くことを確かめる）。この読み分けも agent前提。
  - 画面ごとに切り替える形（比較表の E）は、一段目の棄却案に当たるので採らない。この範囲について新しい `.decisions/` は作らない。
- 置く場所を決めている間の Tab は変えない。出所: agent前提（原文は「ビルドメニューで」。2026-11-20 の仕分けシート 03 で「そのまま通す項目」として提示し、異論なし）
- 検索欄に入力している間の Tab は変えない（今も1回でインベントリが開く）。出所: agent前提（実測で今の挙動を確認。仕分けシート 04 に「そのまま通す項目」として載せた）
- 直し方は案 A（`GameUiInputRules` に入口を足し、パネルの根宛ての選択移動だけ打ち消す）。ゲーム起動時は文書の根だけを受け取り、規則が両方の登録先を決める（レビュー D1 の推奨案 A を実装側が採用）。出所: agent前提（機構の比較は「直し方の比較」節。A だけが実測で効果を確かめてある）
- 打ち消すのは NavigationMoveEvent だけ。出所: agent前提（パネルの根宛ての決定・取消はフォーカスが無ければ何も起こさず、故障の場面を作れない）
- 自動テストは IMGUI の実キー経路（`PanelEventHandler` が Tab から選択移動を作る所）を通さない。Task 2 は `PanelEventHandler` と同じ形のイベントを直接送る。`PanelEventHandler` がパネルの根宛てに送ることは、パッケージのソース（`PanelEventHandler.cs:308, 360-375`）と再現報告の計測が根拠。出所: agent前提（batchmode と CI で IMGUI のキーが届かず、届かないとテストが不具合の状態を作れずに通るため）
- 反証検査（plan を書いた本人でない検査役がリポジトリを読んで欠陥を探す）: 重大 0 件・注意 4 件・設計判断 0 件。注意は全部この plan に反映した（コマンドを作業場所で流す決まり・死活表の根拠・行番号・PR の残りの書き方）。報告: `../moorestech_logs/harness/ui-clarity-kgfsp/tab-plan-v4-refutation.md`、ひとつ前の版への報告: 同じ場所の `tab-plan-v3-refutation.md`。出所: agent前提
- ランタイム挙動に触れるので EditModeInPlayingTest（Task 2）を入れる。unityプレイ録画テストの代わり。出所: agent前提
- Mac の Editor を使わず winunity で回す。出所: agent前提（Mac mini の Editor は同時1本で、別のセッションが使用中。`CLAUDE.local.macmini.md`「運用上の注意」）
