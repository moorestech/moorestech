# 削除ツールのF長押し段階化とCtrlによる選択からの除外 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 削除ツールの F 長押し満了時間を選択件数で 0.75／1.25／1.75 秒の3段にし、Ctrl を押して始めたなぞり・箱で削除候補を選択から外せるようにする。

**Architecture:** 長押し対象 `DeleteHoldSubject` が件数を運び、`DeleteHoldConfirm` が `DeleteHoldDuration.SecondsFor(件数)` で満了時間を引く（件数は選択側から購読でプッシュ済みの subject に乗る）。選択モデル `DeleteSelection` の内部集合を `DeleteSelectedTargetSet` へ切り出して除外（赤戻し・変更通知）を足す。なぞり・箱の「追加か除外か」は押下瞬間の Ctrl を `DeleteSelectionIntent` として選択モデルの入口（`BeginStroke(intent)`・`ApplyBoxTargets(targets, intent)`）へ渡し、意図による分岐は選択モデルに閉じる。Ctrl はサービスが1フレーム1回だけ読む。箱の対象解決 `DeleteBoxTargetResolver` は撤去可能な対象を返す1本の問い合わせのまま、除外の箱ではそのうち選択済みだけに絞る。箱の意図ごとの表示（文言キー・色・名前）は `DeleteBoxIntentView` にまとめる。

**Tech Stack:** Unity (C#)、UniRx、Unity InputSystem（`HybridInput`）、NUnit EditMode（`Client.Tests`）、unity-playmode-recorded-playtest（プレイテスト DSL）。

## Requirements

- R1 F 長押しの満了時間は選択件数で 1〜4件 0.75秒／5〜19件 1.25秒／20件以上 1.75秒。受け入れ: `DeleteHoldDurationTest` の境界 1・4・5・19・20・500、`DeleteHoldConfirmTest` の 5件・20件ケース、録画で5件選択の1秒長押しでは消えない。
- R2 件数はツールチップの選択件数（`DeleteSelection.Count`。種類を問わず1候補=1件、巻き込みの接続線は数えない）。選択が空のホバー1件削除は1件として 0.75秒。受け入れ: `DeleteHoldSubject.Hovered` の件数が1、`DeleteHoldConfirmTest` のホバーケースが 0.75秒で満了。
- R3 Ctrl（Mac は Command も。Ctrl+Z 判定と同じキー）を押して始めたなぞり（Ctrl+左ドラッグ、動かさない Ctrl+左クリックは1件分）は、通った選択済み対象を選択から外し赤を戻す。未選択は無視。照準は追加のなぞりと同じ（最前面・最初に外した対象のカテゴリーに固定）。受け入れ: `DeleteSelectionDeselectTest`、録画 Step 3。
- R4 Ctrl+Shift+2点の箱は、追加の箱と同じ解決結果（撤去可能・カテゴリーを問わず・接続線は入らない）のうち選択済みの対象を選択から外す（選択後に撤去不可になった候補は箱では外れない）。終点待ちのツールチップは「範囲内の選択済み: N件」、0件の終点クリックは理由（「範囲に選択済みの対象がありません」）をツールチップとログへ出して拒否。除外の箱は青（`MaterialConst.PlaceableColor`）で描く。受け入れ: `DeleteBoxSelectionTest` の意図保持、`DeleteSelectionDeselectTest` の箱ケース、録画 Step 4。
- R5 追加か除外かは押下の瞬間（なぞり開始・箱の始点クリック）の Ctrl で決め、途中で Ctrl を離しても変わらない。受け入れ: `DeleteBoxSelection.Intent` が終点まで保たれる、`BeginStroke(Add)` は前回の除外を引き継がない。
- R6 除外で選択が変わると長押し進捗は 0 に戻る（ADR 0079 の既存規則。Revision が進む）。受け入れ: `DeleteSelectionDeselectTest` で Revision が変わる、録画 Step 2→3。
- R7 通常の左操作は追加のまま、ESC／パネル外右短押しの全解除も残す。既存テストがすべて通る。
- R8 破壊モードのキーヒントに「Ctrl+左ドラッグ 選択から外す」を足す（12件）。ADR 0032 の表と `UIStateKeyHintCatalogTest` を同時更新。
- やらないこと: サーバー変更、選択上限、Ctrl 単独押下中のツールチップ案内、除外の箱専用キーヒント、巻き込み件数の表示。

## Global Constraints

- 満了時間の段: 1〜4件 0.75秒 / 5〜19件 1.25秒 / 20件以上 1.75秒（ADR 0084）。時間はクライアントの定数で `Time.deltaTime` 累積（ADR 0079 の採掘前例のまま）。
- 除外の修飾キー: `HybridInput.GetKey(KeyCode.LeftControl) || HybridInput.GetKey(KeyCode.LeftCommand)`（前例 `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/BuildUndoService.cs:36`）。
- 1ファイル200行未満、1ディレクトリ10ファイルまで（`DragDelete/` 直下は既に8件のため新規型は `DragDelete/Selection/` へ。`Client.Tests/UIState/DragDelete/` 直下は既に11件のため新規テストは `Confirm/`・`Selection/` サブディレクトリへ）。
- コメントは日英2行セット、`#region Internal` はメソッド内ローカル関数だけ、デフォルト引数・`Func<>`・partial・try-catch 新設は禁止。`.meta` は手で作らない（Unity 生成分のコミットは可）。
- ローカライズ: `Localization/localization.csv`（列 `key,Source,english,japanese,german,korean`）。C# 側は `uloop compile --project-path ./moorestech_client --force-recompile` の後に通常 `uloop compile`。Web UI 側は `cd moorestech_web/webui && npm run gen:i18n` で `src/shared/i18n/generated/localizationKeys.ts` を再生成してコミット。
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`。ドメインリロードエラー時は45秒待って再試行。
- パッチは worktree ルート（`/Users/sakastudio/hermes-agent/data/worktrees/moorestech/delete-hold-steps-and-ctrl-deselect`）で `git apply` できる形で書いてある。`git apply --check` は plan 作成時点の HEAD（`0bb8f30823`）で全件通過済み。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / なぞりと箱で意図の分岐が呼び出し側に分散→選択モデルの入口 `BeginStroke(intent)`・`ApplyBoxTargets(targets, intent)` に集約
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強2・弱5・第3バケツ1 / 強: 件数の型はこのまま（前提宣言）・除外の箱の判定は ADR に揃える（ユーザー裁定）、弱: ①②④を直す（ユーザー裁定）、第3バケツ: Ctrl||Command 判定の共通化を PR 外提案へ

---

## File Structure

| 種別 | パス | 責務 |
|---|---|---|
| Create | `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldDuration.cs` | 件数→満了秒数の3段表 |
| Modify | `.../State/DragDelete/Confirm/DeleteHoldSubject.cs` | 選択対象が件数を運ぶ（ホバーは1） |
| Modify | `.../State/DragDelete/Confirm/DeleteHoldConfirm.cs` | 固定 2 秒を `DeleteHoldDuration` に置換 |
| Modify | `.../State/DragDelete/DeleteObjectService.cs` | subject に件数を載せる・Ctrl を1フレーム1回読み箱となぞりへ渡す |
| Create | `.../State/DragDelete/Selection/DeleteSelectedTargetSet.cs` | 選択集合・赤表示・赤戻し待ち・変更通知（DeleteSelection から切り出し＋除外） |
| Create | `.../State/DragDelete/Selection/DeleteSelectionIntent.cs` | 追加／除外の判別子 |
| Create | `.../State/DragDelete/Selection/DeleteSelectionModifierKey.cs` | Ctrl/Command を意図へ読む |
| Create | `.../State/DragDelete/Box/DeleteBoxIntentView.cs` | 箱の意図ごとの表示一式（件数・0件の文言キー、ログ名、色、可視化） |
| Modify | `.../State/DragDelete/DeleteSelection.cs` | `BeginStroke(intent)`・`ApplyBoxTargets(targets, intent)`・除外のなぞり、集合の委譲 |
| Modify | `moorestech_client/Assets/Scripts/Client.Tests/` 配下 13 ファイル（`BeginStroke()` の呼び出し） | 新シグネチャ `BeginStroke(DeleteSelectionIntent.Add)` へ一括置換 |
| Modify | `.../State/DragDelete/Box/DeleteBoxSelection.cs` | 箱の意図を始点で保持 |
| Modify | `.../State/DragDelete/Box/DeleteBoxTargetResolver.cs` | 撤去可能な対象を返し、除外の箱では選択済みに絞る |
| Modify | `.../State/DragDelete/Box/DeleteBoxInput.cs` | 押下時の意図の保持・意図ごとの表示の選び分け（1か所）・反映は `ApplyBoxTargets` |
| Modify | `.../State/DeleteObjectState.cs` | キーヒント追加 |
| Modify | `Localization/localization.csv`、`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` | キー4件追加・再生成 |
| Modify | `docs/adr/0032-key-hint-hud-owned-by-ui-state.md` | 破壊モードのヒント表 |
| Test | `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Confirm/DeleteHoldDurationTest.cs`（新規）、`.../DragDelete/DeleteHoldConfirmTest.cs`（書き換え）、`.../DragDelete/Selection/DeleteSelectionDeselectTest.cs`（新規）、`.../DragDelete/DeleteBoxSelectionTest.cs`、`.../UIState/UIStateKeyHintCatalogTest.cs` | 単体 |
| Scenario | `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/delete-tool/delete-ctrl-deselect-and-hold-tiers.cs`（新規）、`.../scenarios/building/delete-tool-box-select-hold-confirm.cs`・`.../scenarios/train/delete-tool-pier-removal-undo-rail.cs`・`.../scenarios/connect/delete-tool-cut-and-undo-connection-lines.cs`（文言更新） | unityプレイ録画テスト |

## 配置と前例

- 満了時間を件数で決める判断は削除ツール側（Confirm）に閉じ、進捗バー `ProgressBarState` は値を受けるだけ（汎用基盤にドメイン語彙を入れない。ADR 0079「長押し Advance はキー状態と時間のみ扱う」）。件数は既存の `OnSelectionChanged` 購読で subject に乗せてプッシュする（毎フレームの同値判定を足さない）。
- 除外は選択モデルが持つ（ADR 0079「選択モデルがストローク・ホバー可否・確定・取消手順を所有する」）。サービスは入力（Ctrl）を1回読んだ意図を `BeginStroke(intent)` と箱の `Update(..., heldIntent)` へ渡すだけで、意図による分岐を書かない。箱入力は結果を `ApplyBoxTargets(targets, intent)` へ渡すだけ。
- 箱の除外も「AABB を受け取り IDeleteTarget の列を返す1本の問い合わせ」（ADR 0079 呼び出し側の線引き）を保ち、撤去可能の判定は追加と共有したうえで、除外のときだけ選択済みに絞る（ADR 0084 の裁定）。
- 修飾キーの読み方は `BuildUndoService.cs:36` と同じ Ctrl||Command。
- データフロー: （左押下＋Ctrl）→ `DeleteObjectService`／`DeleteBoxInput`（書き手）→［`DeleteSelection` の選択集合］→ `OnSelectionChanged` →（`DeleteHoldConfirm`・ツールチップ＝読み手）。除外は既存の書き手に「外す」操作が1つ増えるだけで、新しい経路は無い。

## Tasks

### Task 1: F長押しの満了時間を選択件数で3段にする

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldDuration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldSubject.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldConfirm.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs`（subject の件数のみ。Ctrl の配線は Task 3）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Confirm/DeleteHoldDurationTest.cs`（新規）、`moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs`（全面書き換え）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Input/PlayableInteractInputTest.cs:55,62,78`（`DeleteHoldSubject.Selection(1)`→`Selection(1, 1)`、`Selection(2)`→`Selection(2, 1)`。待ち秒数 `2f`/`3f` は1件の段 0.75 秒以上なので据え置き）

**Interfaces:**
- Produces: `public static float DeleteHoldDuration.SecondsFor(int targetCount)`（1未満は `ArgumentOutOfRangeException`）／`public static DeleteHoldSubject DeleteHoldSubject.Selection(int revision, int targetCount)`（旧 `Selection(int revision)` は削除）／`internal int DeleteHoldSubject.TargetCount`（None=0・Hovered=1）／`private DeleteHoldSubject DeleteObjectService.SelectionHoldSubject()`
- Consumes: なし

- [ ] **Step 1: テストを書く（新規 `DeleteHoldDurationTest.cs`・`DeleteHoldConfirmTest.cs` 全文置換）**

```diff
diff --git a/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Confirm/DeleteHoldDurationTest.cs b/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Confirm/DeleteHoldDurationTest.cs
new file mode 100644
index 0000000..feae477
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Confirm/DeleteHoldDurationTest.cs
@@ -0,0 +1,26 @@
+using System;
+using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
+using NUnit.Framework;
+
+namespace Client.Tests.UIState.DragDelete.Confirm
+{
+    public class DeleteHoldDurationTest
+    {
+        [TestCase(1, 0.75f)]
+        [TestCase(4, 0.75f)]
+        [TestCase(5, 1.25f)]
+        [TestCase(19, 1.25f)]
+        [TestCase(20, 1.75f)]
+        [TestCase(500, 1.75f)]
+        public void 件数の段ごとに満了時間が決まる(int targetCount, float expectedSeconds)
+        {
+            Assert.AreEqual(expectedSeconds, DeleteHoldDuration.SecondsFor(targetCount), 0.0001f);
+        }
+
+        [Test]
+        public void 対象0件の満了時間は求めない()
+        {
+            Assert.Throws<ArgumentOutOfRangeException>(() => DeleteHoldDuration.SecondsFor(0));
+        }
+    }
+}
diff --git a/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs b/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs
index 8a9fd6f..9b1b611 100644
--- a/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs
+++ b/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs
@@ -6,35 +6,52 @@ namespace Client.Tests.UIState.DragDelete
 {
     public class DeleteHoldConfirmTest
     {
-        private static readonly DeleteHoldSubject SelectionRev1 = DeleteHoldSubject.Selection(1);
+        // 1件=0.75秒 / 5件=1.25秒 / 20件=1.75秒（DeleteHoldDuration の段）
+        // One target = 0.75s / five = 1.25s / twenty = 1.75s (DeleteHoldDuration tiers)
+        private static readonly DeleteHoldSubject SelectionRev1 = DeleteHoldSubject.Selection(1, 1);
 
         [Test]
-        public void 押し続けると進捗が伸び2秒で満了する()
+        public void 押し続けると進捗が伸び1件なら0点75秒で満了する()
         {
             var bar = new ProgressBarState();
             var hold = new DeleteHoldConfirm(bar);
             hold.SetSubject(SelectionRev1);
-            Assert.IsFalse(hold.Advance(true, 1f));
+            Assert.IsFalse(hold.Advance(true, 0.375f));
             Assert.IsTrue(bar.IsShown);
             Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
             hold.SetSubject(SelectionRev1);
-            Assert.IsTrue(hold.Advance(true, 1f));
+            Assert.IsTrue(hold.Advance(true, 0.375f));
             Assert.IsFalse(bar.IsShown);
         }
 
+        [Test]
+        public void 選択件数の段で満了時間が伸びる()
+        {
+            var bar = new ProgressBarState();
+            var hold = new DeleteHoldConfirm(bar);
+            var fiveTargets = DeleteHoldSubject.Selection(1, 5);
+            hold.SetSubject(fiveTargets);
+            Assert.IsFalse(hold.Advance(true, 1f));
+            Assert.AreEqual(0.8f, bar.CurrentProgress, 0.001f);
+            Assert.IsTrue(hold.Advance(true, 0.25f));
+            hold.Advance(false, 0f);
+            var twentyTargets = DeleteHoldSubject.Selection(2, 20);
+            hold.SetSubject(twentyTargets);
+            Assert.IsFalse(hold.Advance(true, 1.5f));
+            Assert.IsTrue(hold.Advance(true, 0.25f));
+        }
+
         [Test]
         public void 途中で離すと進捗が0に戻りバーが消える()
         {
             var bar = new ProgressBarState();
             var hold = new DeleteHoldConfirm(bar);
             hold.SetSubject(SelectionRev1);
-            hold.Advance(true, 1f);
-            hold.SetSubject(SelectionRev1);
+            hold.Advance(true, 0.5f);
             Assert.IsFalse(hold.Advance(false, 0.1f));
             Assert.IsFalse(bar.IsShown);
-            hold.SetSubject(SelectionRev1);
-            Assert.IsFalse(hold.Advance(true, 1.5f));
-            Assert.AreEqual(0.75f, bar.CurrentProgress, 0.001f);
+            Assert.IsFalse(hold.Advance(true, 0.375f));
+            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
         }
 
         [Test]
@@ -42,35 +59,33 @@ namespace Client.Tests.UIState.DragDelete
         {
             var bar = new ProgressBarState();
             var hold = new DeleteHoldConfirm(bar);
-            hold.SetSubject(DeleteHoldSubject.Selection(1));
-            hold.Advance(true, 1.5f);
-            hold.SetSubject(DeleteHoldSubject.Selection(2));
-            Assert.IsFalse(hold.Advance(true, 1f));
+            hold.SetSubject(DeleteHoldSubject.Selection(1, 1));
+            hold.Advance(true, 0.6f);
+            hold.SetSubject(DeleteHoldSubject.Selection(2, 1));
+            Assert.IsFalse(hold.Advance(true, 0.375f));
             Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
             var key = new object();
             hold.SetSubject(DeleteHoldSubject.Hovered(key));
-            Assert.IsFalse(hold.Advance(true, 1f));
+            Assert.IsFalse(hold.Advance(true, 0.375f));
             Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
             hold.SetSubject(DeleteHoldSubject.Hovered(key));
-            Assert.IsTrue(hold.Advance(true, 1f), "the same hovered key continues the hold");
+            Assert.IsTrue(hold.Advance(true, 0.375f), "the same hovered key continues the hold");
         }
 
         [Test]
-        public void 別のホバーキーへ移ったらその対象だけで2秒待つ()
+        public void 別のホバーキーへ移ったらその対象だけで0点75秒待つ()
         {
             var bar = new ProgressBarState();
             var hold = new DeleteHoldConfirm(bar);
             var first = DeleteHoldSubject.Hovered(new object());
             var second = DeleteHoldSubject.Hovered(new object());
             hold.SetSubject(first);
-            Assert.IsFalse(hold.Advance(true, 1.5f));
+            Assert.IsFalse(hold.Advance(true, 0.6f));
             hold.SetSubject(second);
-            Assert.IsFalse(hold.Advance(true, 0.5f));
+            Assert.IsFalse(hold.Advance(true, 0.1875f));
             Assert.AreEqual(0.25f, bar.CurrentProgress, 0.001f);
-            hold.SetSubject(second);
-            Assert.IsFalse(hold.Advance(true, 1f));
-            hold.SetSubject(second);
-            Assert.IsTrue(hold.Advance(true, 0.5f));
+            Assert.IsFalse(hold.Advance(true, 0.375f));
+            Assert.IsTrue(hold.Advance(true, 0.1875f));
         }
 
         [Test]
@@ -89,14 +104,12 @@ namespace Client.Tests.UIState.DragDelete
             var bar = new ProgressBarState();
             var hold = new DeleteHoldConfirm(bar);
             hold.SetSubject(SelectionRev1);
-            Assert.IsTrue(hold.Advance(true, 2f));
-            hold.SetSubject(DeleteHoldSubject.Selection(2));
+            Assert.IsTrue(hold.Advance(true, 0.75f));
+            hold.SetSubject(DeleteHoldSubject.Selection(2, 1));
             Assert.IsFalse(hold.Advance(true, 5f));
             Assert.IsFalse(bar.IsShown);
-            hold.SetSubject(DeleteHoldSubject.Selection(2));
             hold.Advance(false, 0.1f);
-            hold.SetSubject(DeleteHoldSubject.Selection(2));
-            Assert.IsTrue(hold.Advance(true, 2f));
+            Assert.IsTrue(hold.Advance(true, 0.75f));
         }
 
         [Test]
@@ -105,17 +118,14 @@ namespace Client.Tests.UIState.DragDelete
             var bar = new ProgressBarState();
             var hold = new DeleteHoldConfirm(bar);
             hold.SetSubject(SelectionRev1);
-            hold.Advance(true, 1f);
+            hold.Advance(true, 0.5f);
             hold.Cancel(true);
             Assert.AreEqual(0f, bar.CurrentProgress);
-            var hovered = DeleteHoldSubject.Hovered(new object());
-            hold.SetSubject(hovered);
+            hold.SetSubject(DeleteHoldSubject.Hovered(new object()));
             Assert.IsFalse(hold.Advance(true, 5f));
             Assert.IsFalse(bar.IsShown);
-            hold.SetSubject(hovered);
             hold.Advance(false, 0.1f);
-            hold.SetSubject(hovered);
-            Assert.IsTrue(hold.Advance(true, 2f));
+            Assert.IsTrue(hold.Advance(true, 0.75f));
         }
 
         [Test]
@@ -124,7 +134,7 @@ namespace Client.Tests.UIState.DragDelete
             var hold = new DeleteHoldConfirm(new ProgressBarState());
             hold.Cancel(false);
             hold.SetSubject(SelectionRev1);
-            Assert.IsTrue(hold.Advance(true, 2f));
+            Assert.IsTrue(hold.Advance(true, 0.75f));
         }
 
         [Test]
@@ -133,7 +143,7 @@ namespace Client.Tests.UIState.DragDelete
             var bar = new ProgressBarState();
             var hold = new DeleteHoldConfirm(bar);
             hold.SetSubject(SelectionRev1);
-            hold.Advance(true, 1f);
+            hold.Advance(true, 0.5f);
             hold.Reset();
             Assert.AreEqual(0f, bar.CurrentProgress);
             Assert.IsFalse(bar.IsShown);
```

- [ ] **Step 2: 実装を書く（`DeleteHoldDuration` 新設・subject に件数・Confirm の時間を差し替え）**

```diff
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldDuration.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldDuration.cs
new file mode 100644
index 0000000..3c6f64a
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldDuration.cs
@@ -0,0 +1,27 @@
+using System;
+
+namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
+{
+    /// <summary>
+    ///     F長押しの満了時間を選択件数から3段で決める（1〜4件 0.75秒 / 5〜19件 1.25秒 / 20件以上 1.75秒）
+    ///     Hold-to-confirm duration in three tiers by target count (1-4: 0.75s / 5-19: 1.25s / 20+: 1.75s)
+    /// </summary>
+    public static class DeleteHoldDuration
+    {
+        private const int MediumTierMinCount = 5;
+        private const int LargeTierMinCount = 20;
+        private const float SmallTierSeconds = 0.75f;
+        private const float MediumTierSeconds = 1.25f;
+        private const float LargeTierSeconds = 1.75f;
+
+        public static float SecondsFor(int targetCount)
+        {
+            // 対象なしの長押しは進捗を始めないので0件はここに来ない
+            // A hold without targets never starts progress, so zero never reaches here
+            if (targetCount < 1) throw new ArgumentOutOfRangeException(nameof(targetCount), targetCount, "hold duration needs at least one target");
+            if (LargeTierMinCount <= targetCount) return LargeTierSeconds;
+            if (MediumTierMinCount <= targetCount) return MediumTierSeconds;
+            return SmallTierSeconds;
+        }
+    }
+}
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldSubject.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldSubject.cs
index 9111296..763fdc0 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldSubject.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldSubject.cs
@@ -10,28 +10,32 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
     }
 
     /// <summary>
-    ///     F長押しの対象。対象なし／選択集合（Revision で同一性）／ホバー1件（論理キーで同一性）を判別子で表す
-    ///     The hold-to-confirm subject: none, the selection (identified by revision) or one hovered target (identified by its logical key)
+    ///     F長押しの対象。対象なし／選択集合（Revision で同一性・件数を運ぶ）／ホバー1件（論理キーで同一性）を判別子で表す
+    ///     The hold-to-confirm subject: none, the selection (identified by revision, carrying its count) or one hovered target (identified by its logical key)
     /// </summary>
     public readonly struct DeleteHoldSubject : IEquatable<DeleteHoldSubject>
     {
-        public static DeleteHoldSubject None => new(DeleteHoldSubjectKind.None, null);
-        public static DeleteHoldSubject Selection(int revision) => new(DeleteHoldSubjectKind.Selection, revision);
-        public static DeleteHoldSubject Hovered(object targetKey) => new(DeleteHoldSubjectKind.Hovered, targetKey);
+        public static DeleteHoldSubject None => new(DeleteHoldSubjectKind.None, null, 0);
+        public static DeleteHoldSubject Selection(int revision, int targetCount) => new(DeleteHoldSubjectKind.Selection, revision, targetCount);
+        public static DeleteHoldSubject Hovered(object targetKey) => new(DeleteHoldSubjectKind.Hovered, targetKey, 1);
 
         internal DeleteHoldSubjectKind Kind { get; }
         internal bool IsNone => Kind == DeleteHoldSubjectKind.None;
+        // 満了時間を決める件数（ホバー1件は1件）
+        // Count that decides the hold duration (one hovered target counts as one)
+        internal int TargetCount { get; }
         private readonly object _key;
 
-        private DeleteHoldSubject(DeleteHoldSubjectKind kind, object key)
+        private DeleteHoldSubject(DeleteHoldSubjectKind kind, object key, int targetCount)
         {
             Kind = kind;
             _key = key;
+            TargetCount = targetCount;
         }
 
         public bool Equals(DeleteHoldSubject other)
         {
-            return Kind == other.Kind && Equals(_key, other._key);
+            return Kind == other.Kind && Equals(_key, other._key) && TargetCount == other.TargetCount;
         }
 
         public override bool Equals(object obj)
@@ -41,7 +45,7 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
 
         public override int GetHashCode()
         {
-            return HashCode.Combine((int)Kind, _key);
+            return HashCode.Combine((int)Kind, _key, TargetCount);
         }
     }
 }
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldConfirm.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldConfirm.cs
index 5a8d5be..dbeacc4 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldConfirm.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldConfirm.cs
@@ -3,13 +3,11 @@ using Client.Game.InGame.UI.ProgressBar;
 namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
 {
     /// <summary>
-    ///     F長押しによる削除確定の進捗。件数に関係なく一定時間で満了し、採掘と同じ進捗バーへ流す
-    ///     Hold-to-confirm progress for deletion: a fixed duration regardless of count, shown on the mining progress bar
+    ///     F長押しによる削除確定の進捗。対象の件数で決まる時間で満了し、採掘と同じ進捗バーへ流す
+    ///     Hold-to-confirm progress for deletion: completes after a duration set by the subject's count, shown on the mining progress bar
     /// </summary>
     public class DeleteHoldConfirm
     {
-        private const float HoldSeconds = 2f;
-
         private readonly ProgressBarState _progressBar;
         private DeleteHoldSubject _subject = DeleteHoldSubject.None;
         private float _heldSeconds;
@@ -37,9 +35,10 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
 
             if (!_isHolding) Begin();
 
+            var holdSeconds = DeleteHoldDuration.SecondsFor(_subject.TargetCount);
             _heldSeconds += deltaTime;
-            _progressBar.SetProgress(_heldSeconds / HoldSeconds);
-            if (_heldSeconds < HoldSeconds) return false;
+            _progressBar.SetProgress(_heldSeconds / holdSeconds);
+            if (_heldSeconds < holdSeconds) return false;
 
             Reset();
             _awaitRelease = true;
```

- [ ] **Step 3: サービスが subject に選択件数を載せる**

`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs` の `DeleteHoldSubject.Selection(_selection.Revision)` 2箇所（ctor の購読・`UpdateHoldConfirm`）を `SelectionHoldSubject()` に置き換え、`TryCancel` のコメントの直前へ次を足す。Task 3 のパッチはこの差分を含むので、Task 3 では Task 1 で入れた分を除いて当てる（`git apply` が失敗したら手で残りの差分だけ入れる）:

```csharp
        // 選択集合の長押し対象（件数が満了時間を決める）
        // Hold subject for the selection; its count decides the hold duration
        private DeleteHoldSubject SelectionHoldSubject() => DeleteHoldSubject.Selection(_selection.Revision, _selection.Count);
```

- [ ] **Step 4: 既存の呼び出しテストを新シグネチャへ直す**

`moorestech_client/Assets/Scripts/Client.Tests/Input/PlayableInteractInputTest.cs` の 55・62・78 行を `DeleteHoldSubject.Selection(1, 1)`／`DeleteHoldSubject.Selection(2, 1)`／`DeleteHoldSubject.Selection(2, 1)` にする。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DeleteHoldDurationTest|DeleteHoldConfirmTest|DeleteSelection|DragDelete|PlayableInteractInputTest"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Confirm moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs moorestech_client/Assets/Scripts/Client.Tests/Input/PlayableInteractInputTest.cs
git commit -m "feat: 削除のF長押しを選択件数で0.75/1.25/1.75秒の3段にする"
```

### Task 2: 選択モデルに追加／除外の意図と除外（なぞり・箱）を足す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectedTargetSet.cs`、`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectionIntent.cs`、`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectionModifierKey.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteSelection.cs`（内部 Dictionary・設置演出購読・解除キュー・Revision 通知を `DeleteSelectedTargetSet` へ移し、`BeginStroke(intent)`・`TryRemoveStrokeTarget`・`ApplyBoxTargets`・`IsSelected` を足す。193行）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs:92`（`_selection.BeginStroke()` → `_selection.BeginStroke(DeleteSelectionIntent.Add)` と using 追加。Ctrl の配線は Task 3 で置き換える）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/` 配下の `BeginStroke()` 呼び出し 13 ファイル（`BuildUndo/RemoveOperationHistoryTest.cs`、`UIState/DeleteSelectionCategoryTest.cs`、`UIState/DeleteSelectionTest.cs`、`UIState/DragDelete/Completion/DeleteCompletionBatchTest.cs`、`UIState/DragDelete/Completion/LateRemovalUndoTest.cs`、`UIState/DragDelete/DeleteSelectionPreviewTest.cs`、`UIState/DragDelete/DestroyedSelectionTest.cs`、`UIState/DragDelete/Recovery/PendingPreviewReleaseTest.cs`、`UIState/DragDelete/Recovery/SharedPreviewKeyTest.cs`、`UIState/DragDelete/ResetFailureRecoveryTest.cs`、`UIState/DragDelete/SharedResetRecoveryTest.cs`、`UIState/DragDeleteDenyReasonTest.cs`、`UIState/UIStateControlTest.cs`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Selection/DeleteSelectionDeselectTest.cs`（新規）

**Interfaces:**
- Consumes: なし（Task 1 と独立）
- Produces: `public enum DeleteSelectionIntent { Add, Remove }`（namespace `Client.Game.InGame.UI.UIState.State.DragDelete.Selection`）／`internal static DeleteSelectionIntent DeleteSelectionModifierKey.ReadIntent()`／`public void DeleteSelection.BeginStroke(DeleteSelectionIntent intent)`（旧 `BeginStroke()` は削除）／`public bool DeleteSelection.TryRemoveStrokeTarget(IDeleteTarget target)`／`public int DeleteSelection.ApplyBoxTargets(IReadOnlyList<IDeleteTarget> targets, DeleteSelectionIntent intent)`（意図による分岐はここだけ。`AddBoxTargets` は公開のまま残し内部から呼ぶ）／`internal bool DeleteSelection.IsSelected(IDeleteTarget target)`。`UpdateStroke(DeleteAimResult)` は `BeginStroke(Remove)` 後なら除外する。`Revision`・`OnSelectionChanged` の外形は不変（中身は集合へ委譲）。

- [ ] **Step 1: テストを書く**

```diff
diff --git a/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Selection/DeleteSelectionDeselectTest.cs b/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Selection/DeleteSelectionDeselectTest.cs
new file mode 100644
index 0000000..5f415fb
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Selection/DeleteSelectionDeselectTest.cs
@@ -0,0 +1,117 @@
+using System.Collections.Generic;
+using Client.Game.Common;
+using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
+using Client.Game.InGame.UI.UIState.State;
+using Client.Game.InGame.UI.UIState.State.DragDelete;
+using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
+using Client.Tests.BuildUndo;
+using Client.Tests.UIState.Fakes;
+using NUnit.Framework;
+
+namespace Client.Tests.UIState.DragDelete.Selection
+{
+    public class DeleteSelectionDeselectTest
+    {
+        private static DeleteSelection Create() => new(new BuildOperationHistory(), new FakeRemovalRestoreSender(), new DeletePreviewResetRecovery());
+
+        [Test]
+        public void 除外のなぞりは選択済みを外して赤を戻す()
+        {
+            var selection = Create();
+            var kept = new FakeDeleteTarget { Removable = true };
+            var removed = new FakeDeleteTarget { Removable = true };
+            selection.AddBoxTargets(new List<IDeleteTarget> { kept, removed });
+            var revision = selection.Revision;
+            selection.BeginStroke(DeleteSelectionIntent.Remove);
+            Assert.IsTrue(selection.TryRemoveStrokeTarget(removed));
+            Assert.AreEqual(1, selection.Count);
+            Assert.AreEqual(1, removed.ResetCount);
+            Assert.AreEqual(0, kept.ResetCount);
+            Assert.AreNotEqual(revision, selection.Revision);
+            selection.CommitDelete();
+            Assert.AreEqual(1, kept.DeleteCount);
+            Assert.AreEqual(0, removed.DeleteCount);
+        }
+
+        [Test]
+        public void 未選択の対象は外さず版も進めない()
+        {
+            var selection = Create();
+            selection.AddBoxTargets(new List<IDeleteTarget> { new FakeDeleteTarget { Removable = true } });
+            var unselected = new FakeDeleteTarget { Removable = true };
+            var revision = selection.Revision;
+            selection.BeginStroke(DeleteSelectionIntent.Remove);
+            Assert.IsFalse(selection.TryRemoveStrokeTarget(unselected));
+            Assert.AreEqual(revision, selection.Revision);
+            Assert.AreEqual(0, unselected.ResetCount);
+            Assert.AreEqual(1, selection.Count);
+        }
+
+        [Test]
+        public void 除外のなぞりは最初に外したカテゴリーに固定する()
+        {
+            var selection = Create();
+            var line = new FakeDeleteTarget { Removable = true, Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
+            var block = new FakeDeleteTarget { Removable = true };
+            selection.BeginStroke(DeleteSelectionIntent.Add);
+            selection.TryAddTarget(line);
+            selection.AddBoxTargets(new List<IDeleteTarget> { block });
+            selection.BeginStroke(DeleteSelectionIntent.Remove);
+            Assert.IsTrue(selection.TryRemoveStrokeTarget(line));
+            Assert.IsFalse(selection.TryRemoveStrokeTarget(block), "a deselect stroke stays in the first category");
+            selection.EndStroke();
+            selection.BeginStroke(DeleteSelectionIntent.Remove);
+            Assert.IsTrue(selection.TryRemoveStrokeTarget(block));
+            Assert.IsFalse(selection.HasSelection());
+        }
+
+        [Test]
+        public void 同じ論理キーの別ラッパーでも外せる()
+        {
+            var selection = Create();
+            var block = new FakeDeleteTarget { Removable = true };
+            selection.AddBoxTargets(new List<IDeleteTarget> { block });
+            selection.BeginStroke(DeleteSelectionIntent.Remove);
+            Assert.IsTrue(selection.TryRemoveStrokeTarget(new FakeDeleteTarget { Removable = true, Key = block }));
+            Assert.AreEqual(1, block.ResetCount, "the selected instance is un-redded");
+        }
+
+        [Test]
+        public void 箱の除外は選択済みだけを外し件数を返す()
+        {
+            var selection = Create();
+            var a = new FakeDeleteTarget { Removable = true };
+            var b = new FakeDeleteTarget { Removable = true };
+            var c = new FakeDeleteTarget { Removable = true };
+            selection.AddBoxTargets(new List<IDeleteTarget> { a, b, c });
+            Assert.AreEqual(2, selection.ApplyBoxTargets(new List<IDeleteTarget> { a, b, new FakeDeleteTarget { Removable = true } }, DeleteSelectionIntent.Remove));
+            Assert.AreEqual(1, selection.Count);
+            Assert.IsTrue(selection.IsSelected(c));
+            Assert.IsFalse(selection.IsSelected(a));
+        }
+
+        [Test]
+        public void 通常のなぞりは除外を引き継がない()
+        {
+            var selection = Create();
+            var target = new FakeDeleteTarget { Removable = true };
+            selection.BeginStroke(DeleteSelectionIntent.Remove);
+            selection.EndStroke();
+            selection.BeginStroke(DeleteSelectionIntent.Add);
+            Assert.IsTrue(selection.TryAddTarget(target).IsAdded);
+            Assert.AreEqual(1, selection.Count);
+        }
+
+        [Test]
+        public void 全件外した後の全解除は外した対象の赤を二重に戻さない()
+        {
+            var selection = Create();
+            var target = new FakeDeleteTarget { Removable = true };
+            selection.AddBoxTargets(new List<IDeleteTarget> { target });
+            selection.BeginStroke(DeleteSelectionIntent.Remove);
+            selection.TryRemoveStrokeTarget(target);
+            Assert.IsFalse(selection.TryCancelSelection());
+            Assert.AreEqual(1, target.ResetCount);
+        }
+    }
+}
```

- [ ] **Step 2: 実装を書く**

```diff
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectedTargetSet.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectedTargetSet.cs
new file mode 100644
index 0000000..f1018d3
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectedTargetSet.cs
@@ -0,0 +1,93 @@
+using System;
+using System.Collections.Generic;
+using Client.Game.InGame.Block;
+using UniRx;
+
+namespace Client.Game.InGame.UI.UIState.State.DragDelete.Selection
+{
+    /// <summary>
+    ///     選択済みの削除対象を論理キーで保持し、赤表示・設置演出後の赤の復元・解除待ちの赤戻し・変更通知を持つ
+    ///     Holds selected delete targets by logical key, owning their red preview, its restore after placement, pending un-red releases and change notification
+    /// </summary>
+    internal sealed class DeleteSelectedTargetSet
+    {
+        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
+        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
+        private readonly Dictionary<object, IDeleteTarget> _targets = new();
+        private readonly CompositeDisposable _placeAnimationSubscriptions = new();
+        private readonly DeletePreviewResetRecovery _resetRecovery;
+        private readonly DeletePreviewReleaseQueue _pendingResets;
+        private readonly Subject<int> _changed = new();
+
+        internal int Count => _targets.Count;
+        internal IEnumerable<IDeleteTarget> Values => _targets.Values;
+        internal int Revision { get; private set; }
+        internal IObservable<int> OnChanged => _changed;
+
+        internal DeleteSelectedTargetSet(DeletePreviewResetRecovery resetRecovery)
+        {
+            _resetRecovery = resetRecovery;
+            _pendingResets = new DeletePreviewReleaseQueue(resetRecovery);
+        }
+
+        internal bool Contains(object key) => _targets.ContainsKey(key);
+
+        internal void Add(object key, IDeleteTarget target)
+        {
+            _targets.Add(key, target);
+            if (_resetRecovery.CanPreview(target)) target.SetRemovePreviewing();
+            // 設置演出で戻された赤を、選択が続く間だけ復元する
+            // Restore red after placement animation only while selection still owns the target
+            if (target is BlockGameObjectChild child)
+            {
+                child.BlockGameObject.OnFinishedPlaceAnimation.Take(1).Subscribe(_ =>
+                {
+                    if (_targets.TryGetValue(key, out var selected) && ReferenceEquals(selected, target) &&
+                        DeleteTargetLiveness.IsAliveOrWarn(target, "[DragDelete] selected target destroyed after placement") && _resetRecovery.CanPreview(target)) target.SetRemovePreviewing();
+                }).AddTo(_placeAnimationSubscriptions);
+            }
+            NotifyChanged();
+        }
+
+        // 選択済みなら外して赤を戻す。未選択は外す物が無いので false
+        // Remove and un-red a selected target; false when it was not selected
+        internal bool TryRemove(IDeleteTarget target)
+        {
+            if (!_targets.Remove(target.GetDeleteTargetKey(), out var removed)) return false; // silent-ok: 未選択の対象は外す物が無い / 選択件数のツールチップが変わらないことで見える
+            // 論理状態と通知を先に済ませ、赤戻しが失敗しても選択から外れた状態を保つ
+            // Update logical state and notify first so a failing un-red still leaves the target deselected
+            _pendingResets.Enqueue(new[] { removed });
+            NotifyChanged();
+            _pendingResets.Drain("[DragDelete] selected target was destroyed before deselect");
+            return true;
+        }
+
+        internal int RemoveAll(IReadOnlyList<IDeleteTarget> targets)
+        {
+            var removed = 0;
+            foreach (var target in targets)
+            {
+                if (TryRemove(target)) removed++;
+            }
+            return removed;
+        }
+
+        // 全件を解除待ちへ積んで空にする（赤戻しは DrainReleases で行う）
+        // Queue every target for release and empty the set; DrainReleases performs the un-red
+        internal void Clear()
+        {
+            _pendingResets.Enqueue(_targets.Values);
+            _targets.Clear();
+            _placeAnimationSubscriptions.Clear();
+            NotifyChanged();
+        }
+
+        internal void DrainReleases(string destroyedWarning) => _pendingResets.Drain(destroyedWarning);
+
+        private void NotifyChanged()
+        {
+            Revision++;
+            _changed.OnNext(Revision);
+        }
+    }
+}
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectionIntent.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectionIntent.cs
new file mode 100644
index 0000000..d219f84
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectionIntent.cs
@@ -0,0 +1,12 @@
+namespace Client.Game.InGame.UI.UIState.State.DragDelete.Selection
+{
+    /// <summary>
+    ///     なぞり・箱が選択へ足すか選択から外すか。押下の瞬間の Ctrl で決まり、その操作の間は変わらない
+    ///     Whether a stroke or box adds to or removes from the selection; fixed by Ctrl at the press and unchanged for that operation
+    /// </summary>
+    public enum DeleteSelectionIntent
+    {
+        Add,
+        Remove,
+    }
+}
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectionModifierKey.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectionModifierKey.cs
new file mode 100644
index 0000000..4f23915
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection/DeleteSelectionModifierKey.cs
@@ -0,0 +1,18 @@
+using Client.Input;
+using UnityEngine;
+
+namespace Client.Game.InGame.UI.UIState.State.DragDelete.Selection
+{
+    /// <summary>
+    ///     なぞり・箱を選択からの除外にする修飾キー（Ctrl／Mac の Command。Ctrl+Z の判定と同じキー）
+    ///     Modifier that turns a stroke or box into deselection (Ctrl, or Command on Mac; same keys as the Ctrl+Z check)
+    /// </summary>
+    internal static class DeleteSelectionModifierKey
+    {
+        internal static DeleteSelectionIntent ReadIntent()
+        {
+            var isDeselectHeld = HybridInput.GetKey(KeyCode.LeftControl) || HybridInput.GetKey(KeyCode.LeftCommand);
+            return isDeselectHeld ? DeleteSelectionIntent.Remove : DeleteSelectionIntent.Add;
+        }
+    }
+}
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteSelection.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteSelection.cs
index b069464..4d87931 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteSelection.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteSelection.cs
@@ -1,6 +1,6 @@
 using Mooresmaster.Localization.Generated;
-using Client.Game.InGame.Block;
 using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
+using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
 using System;
 using UniRx;
 using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
@@ -14,22 +14,17 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
     {
         private readonly BuildOperationHistory _buildOperationHistory;
         private readonly IRemovalRestoreSender _restoreSender;
-        private readonly DeletePreviewResetRecovery _resetRecovery;
         private readonly SingleHoverDeletePreview _hoverPreview;
-        private readonly DeletePreviewReleaseQueue _pendingResets;
-        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
-        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
-        private readonly Dictionary<object, IDeleteTarget> _selectedTargets = new();
-        private readonly CompositeDisposable _placeAnimationSubscriptions = new();
+        private readonly DeleteSelectedTargetSet _selectedTargets;
         private bool _strokeCanceled;
+        private DeleteSelectionIntent _strokeIntent = DeleteSelectionIntent.Add;
         private IDeleteTarget _hoverTarget;
         private readonly DeleteResultTracker _results = new();
         private readonly Subject<DeleteCompletion> _deleteCompleted = new();
         public IObservable<DeleteCompletion> OnDeleteCompleted => _deleteCompleted;
-        private readonly Subject<int> _selectionChanged = new();
-        public IObservable<int> OnSelectionChanged => _selectionChanged;
+        public IObservable<int> OnSelectionChanged => _selectedTargets.OnChanged;
         public DeleteAimFilter AimFilter { get; private set; } = DeleteAimFilter.Frontmost;
-        public int Revision { get; private set; }
+        public int Revision => _selectedTargets.Revision;
         public int Count => _selectedTargets.Count;
         // 今赤くしているホバー対象（選択済みとは別管理）
         // The hovered target currently shown red (tracked apart from the selection)
@@ -38,17 +33,17 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
         {
             _buildOperationHistory = buildOperationHistory;
             _restoreSender = restoreSender;
-            _resetRecovery = resetRecovery;
             _hoverPreview = new SingleHoverDeletePreview(resetRecovery);
-            _pendingResets = new DeletePreviewReleaseQueue(resetRecovery);
+            _selectedTargets = new DeleteSelectedTargetSet(resetRecovery);
             _deleteCompleted.Subscribe(_results.Receive);
         }
-        // ストローク開始: キャンセル解除とカテゴリー固定のリセットだけで、選択は消さない
-        // Stroke start: only revives the stroke and resets the category fix; the selection survives
-        public void BeginStroke()
+        // ストローク開始: キャンセル解除・カテゴリー固定のリセット・追加か除外かの固定だけで、選択は消さない
+        // Stroke start: revives the stroke, resets the category fix and fixes add/remove; the selection survives
+        public void BeginStroke(DeleteSelectionIntent intent)
         {
             _strokeCanceled = false;
             AimFilter = DeleteAimFilter.Frontmost;
+            _strokeIntent = intent;
         }
         // 左ボタン解放: カテゴリー固定を最前面へ戻す（離した直後のホバーが別カテゴリーも拾えるように）
         // Button release: return the category fix to frontmost so the next hover can pick any category
@@ -76,12 +71,26 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
             }
             return added;
         }
+        // 除外のなぞり: 選択済みだけ外し、最初に外した対象のカテゴリーへ固定する。外したら true
+        // Deselect stroke: remove only selected targets and lock the category on the first removal; true when removed
+        public bool TryRemoveStrokeTarget(IDeleteTarget target)
+        {
+            if (_strokeCanceled || !AimFilter.Accepts(target) || !_selectedTargets.TryRemove(target)) return false; // silent-ok: 取消済み・別カテゴリー・未選択は外さない / 選択件数のツールチップが変わらないことで見える
+            if (!AimFilter.IsCategoryRequired) AimFilter = DeleteAimFilter.Category(target.GetDestructionCategory());
+            return true;
+        }
+        // 箱の対象を意図どおり足すか外し、変わった件数を返す（意図による分岐はここ1か所）
+        // Add or remove box targets per intent and return how many changed (the only intent branch for boxes)
+        public int ApplyBoxTargets(IReadOnlyList<IDeleteTarget> targets, DeleteSelectionIntent intent)
+        {
+            return intent == DeleteSelectionIntent.Remove ? _selectedTargets.RemoveAll(targets) : AddBoxTargets(targets);
+        }
         // ホバー替え: 旧ホバーが選択済みでなければ赤を戻し、新ホバーを赤にする（null で解除）
         // Hover swap: un-red the previous hover unless it is selected, then red the new one (null clears)
         public void SetHoverPreview(IDeleteTarget hovered)
         {
             _hoverTarget = hovered;
-            if (hovered == null || _selectedTargets.ContainsKey(hovered.GetDeleteTargetKey())) _hoverPreview.Release();
+            if (hovered == null || IsSelected(hovered)) _hoverPreview.Release();
             else _hoverPreview.Hover(hovered);
         }
         public void CancelSelection()
@@ -93,18 +102,14 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
         {
             // 戻し処理より先に論理状態と長押しsubjectを解除する
             // Clear logical state and the hold subject before any reset can throw
-            _pendingResets.Enqueue(_selectedTargets.Values);
-            _selectedTargets.Clear();
-            _placeAnimationSubscriptions.Clear();
             _hoverTarget = null;
             _strokeCanceled = true;
             AimFilter = DeleteAimFilter.Frontmost;
-            Revision++;
-            _selectionChanged.OnNext(Revision);
+            _selectedTargets.Clear();
         }
         private void ReleasePreviews(string destroyedWarning)
         {
-            try { _pendingResets.Drain(destroyedWarning); }
+            try { _selectedTargets.DrainReleases(destroyedWarning); }
             finally { _hoverPreview.Release(); }
         }
         public void CommitDelete()
@@ -126,7 +131,10 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
         internal LocalizationKey? UpdateStroke(DeleteAimResult aim)
         {
             if (_strokeCanceled) return null; // silent-ok: 取消済みストローク / ESCで選択表示を解除済み
-            return aim.Target == null ? aim.GetDragDenyReason() : TryAddTarget(aim.Target).DenyReason;
+            if (aim.Target == null) return aim.GetDragDenyReason();
+            if (_strokeIntent == DeleteSelectionIntent.Add) return TryAddTarget(aim.Target).DenyReason;
+            TryRemoveStrokeTarget(aim.Target);
+            return null;
         }
         internal DeleteHoldSubject UpdateHoverPreview(IDeleteTarget target, out LocalizationKey? reason)
         {
@@ -141,7 +149,7 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
             reason = null;
             if (subject.Kind == DeleteHoldSubjectKind.Hovered)
             {
-                BeginStroke();
+                BeginStroke(DeleteSelectionIntent.Add);
                 var outcome = TryAddTarget(_hoverTarget);
                 if (!outcome.IsAdded)
                 {
@@ -168,29 +176,17 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
             return !_results.IsPending(target.GetDeleteTargetKey()) && target.IsRemovable(out reason);
         }
         public bool HasSelection() => 0 < _selectedTargets.Count;
+        internal bool IsSelected(IDeleteTarget target) => _selectedTargets.Contains(target.GetDeleteTargetKey());
         // 既に同じ論理対象が選択済みなら何もしない（Revisionも進めない）
         // No-op when the same logical target is already selected (the revision stays)
         private bool Add(IDeleteTarget target)
         {
             var key = target.GetDeleteTargetKey();
-            if (_selectedTargets.ContainsKey(key) || !IsTargetSelectable(target, out _)) return false; // silent-ok: 選択済み/結果待ちの対象を重複送信しない / 選択赤と結果ツールチップで表示
+            if (IsSelected(target) || !IsTargetSelectable(target, out _)) return false; // silent-ok: 選択済み/結果待ちの対象を重複送信しない / 選択赤と結果ツールチップで表示
             // 同一論理対象への移譲前に旧ホバーを必ず手放す
             // Release the old hover before transferring ownership of the same logical target
             if (HoverPreview != null && ((HoverPreview is UnityEngine.Object view && view == null) || Equals(HoverPreview.GetDeleteTargetKey(), key))) _hoverPreview.Release();
             _selectedTargets.Add(key, target);
-            if (_resetRecovery.CanPreview(target)) target.SetRemovePreviewing();
-            // 設置演出で戻された赤を、選択が続く間だけ復元する
-            // Restore red after placement animation only while selection still owns the target
-            if (target is BlockGameObjectChild child)
-            {
-                child.BlockGameObject.OnFinishedPlaceAnimation.Take(1).Subscribe(_ =>
-                {
-                    if (_selectedTargets.TryGetValue(key, out var selected) && ReferenceEquals(selected, target) &&
-                        DeleteTargetLiveness.IsAliveOrWarn(target, "[DragDelete] selected target destroyed after placement") && _resetRecovery.CanPreview(target)) target.SetRemovePreviewing();
-                }).AddTo(_placeAnimationSubscriptions);
-            }
-            Revision++;
-            _selectionChanged.OnNext(Revision);
             return true;
         }
     }
```

注意: `ClearSelectionState` は「論理状態を先に空にしてから赤戻し」の順を保つ（`_selectedTargets.Clear()` が解除キューへ積み通知まで済ませ、`ReleasePreviews` が `DrainReleases` する）。`TryRemove` も通知を赤戻しより先に行う。

- [ ] **Step 3: 既存の `BeginStroke()` 呼び出しを一括で新シグネチャにする**

worktree ルートで次を実行する（`BeginStroke()` を `BeginStroke(DeleteSelectionIntent.Add)` に置き換え、名前空間の using を足す）:

```bash
python3 - <<'PY'
import pathlib
using = "using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;\n"
paths = [p for p in pathlib.Path("moorestech_client/Assets/Scripts").rglob("*.cs") if "BeginStroke()" in p.read_text() and "public void BeginStroke" not in p.read_text()]
for p in paths:
    s = p.read_text().replace("BeginStroke()", "BeginStroke(DeleteSelectionIntent.Add)")
    if using not in s: s = using + s
    p.write_text(s)
    print(p)
PY
```
Expected: 13 個のテストファイルと `DeleteObjectService.cs` の計14ファイルが出力される。`grep -rn "BeginStroke()" moorestech_client/Assets/Scripts` が0件になる。

- [ ] **Step 4: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DeleteSelection|DragDelete|RemoveOperationHistoryTest|CascadePreviewRedTest|UIStateControlTest"`
Expected: ErrorCount 0 / 全 PASS（既存の選択・プレビュー・回復テストが呼び出しの置換だけで通ること）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Selection moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteSelection.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Selection moorestech_client/Assets/Scripts/Client.Tests/
git commit -m "feat: 削除ツールの選択モデルに追加と除外の意図を入れる"
```

### Task 3: Ctrl の入力・箱の除外・キーヒント・ローカライズを配線する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxIntentView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs`（Ctrl を1フレーム1回読み、箱となぞりへ渡す）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxSelection.cs`、`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxTargetResolver.cs`、`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxInput.cs`（177行）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`
- Modify: `Localization/localization.csv`、`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（再生成）
- Modify: `docs/adr/0032-key-hint-hud-owned-by-ui-state.md:52`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs`

**Interfaces:**
- Consumes: Task 2 の `DeleteSelectionIntent`・`DeleteSelectionModifierKey.ReadIntent()`・`BeginStroke(DeleteSelectionIntent)`・`ApplyBoxTargets(...)`・`IsSelected(...)`、Task 1 の `SelectionHoldSubject()`
- Produces: `public void DeleteBoxSelection.SelectStart(Vector3Int cell, DeleteSelectionIntent intent)`／`public DeleteSelectionIntent DeleteBoxSelection.Intent { get; private set; }`／`internal IReadOnlyList<IDeleteTarget> DeleteBoxTargetResolver.Resolve(Vector3Int min, Vector3Int max, DeleteSelection selection, DeleteSelectionIntent intent)`（撤去可能な対象。Remove のときはそのうち選択済みだけ）／`internal void DeleteBoxInput.Update(DeleteSelection deleteSelection, List<TooltipLine> tooltipLines, bool isPointerOverUi, DeleteSelectionIntent heldIntent)`／`internal sealed class DeleteBoxIntentView`（`CreateAdd()`・`CreateRemove()`・`CountKey`・`EmptyRangeKey`・`TargetLabelForLog`・`GetVisualizer()`・`Hide()`）／ローカライズキー `LocalizationKeys.Ui.KeyHint.Key.CtrlLeftDrag`・`LocalizationKeys.Ui.KeyHint.Text.DeselectDrag`・`LocalizationKeys.Ui.Tooltip.DeleteBoxSelectedInRange`・`LocalizationKeys.Ui.Tooltip.DeleteBoxNoSelectedInRange`／除外の箱の GameObject 名接頭辞 `DeleteDeselectBox`（`DeleteDeselectBoxRangeBox` 等。録画が参照）

- [ ] **Step 1: ローカライズ行を足して再生成する**

`Localization/localization.csv` の `ui.keyHint.key.leftDrag` 行の直後に1行、`ui.keyHint.text.boxSelect` 行の直後に1行、`ui.tooltip.deleteBoxEmptyRange` 行の直後に2行を足す:

```csv
ui.keyHint.key.ctrlLeftDrag,Ctrl + Left Drag,Ctrl + Left Drag,Ctrl+左ドラッグ,Strg + mit links ziehen,Ctrl + 왼쪽 드래그
```
```csv
ui.keyHint.text.deselectDrag,Remove from Selection,Remove from Selection,選択から外す,Aus Auswahl entfernen,선택에서 제외
```
```csv
ui.tooltip.deleteBoxSelectedInRange,Selected in range: {p0},Selected in range: {p0},範囲内の選択済み: {p0}件,Ausgewählt im Bereich: {p0},범위 내 선택됨: {p0}개
ui.tooltip.deleteBoxNoSelectedInRange,No selected targets in range,No selected targets in range,範囲に選択済みの対象がありません,Keine ausgewählten Ziele im Bereich,범위에 선택된 대상이 없습니다
```

Run: `uloop compile --project-path ./moorestech_client --force-recompile` → `cd moorestech_web/webui && npm run gen:i18n`
Expected: `LocalizationKeys.Ui.KeyHint.Key.CtrlLeftDrag` 等が生成され、`localizationKeys.ts` に4キーが増える

- [ ] **Step 2: テストを更新する**

```diff
diff --git a/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs b/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs
index ec0d6d3..014e09c 100644
--- a/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs
+++ b/moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs
@@ -1,5 +1,6 @@
 using System;
 using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
+using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
 using NUnit.Framework;
 using UnityEngine;
 
@@ -12,7 +13,7 @@ namespace Client.Tests.UIState.DragDelete
         {
             var selection = new DeleteBoxSelection();
             Assert.AreEqual(DeleteBoxPhase.Idle, selection.Phase);
-            selection.SelectStart(new Vector3Int(1, 2, 3));
+            selection.SelectStart(new Vector3Int(1, 2, 3), DeleteSelectionIntent.Add);
             Assert.AreEqual(DeleteBoxPhase.SelectingEnd, selection.Phase);
             Assert.AreEqual(new Vector3Int(1, 2, 3), selection.StartCell);
             selection.Clear();
@@ -23,12 +24,12 @@ namespace Client.Tests.UIState.DragDelete
         public void LastValidEndSurvivesMissingCursorUntilNextValidCell()
         {
             var selection = new DeleteBoxSelection();
-            selection.SelectStart(new Vector3Int(2, 3, 4));
+            selection.SelectStart(new Vector3Int(2, 3, 4), DeleteSelectionIntent.Add);
             Assert.AreEqual(selection.StartCell, selection.EndCell);
             selection.SetEndCell(new Vector3Int(8, 9, 10));
             Assert.AreEqual(new Vector3Int(8, 9, 10), selection.EndCell);
             selection.Clear();
-            selection.SelectStart(Vector3Int.zero);
+            selection.SelectStart(Vector3Int.zero, DeleteSelectionIntent.Add);
             Assert.AreEqual(Vector3Int.zero, selection.EndCell);
         }
 
@@ -36,8 +37,20 @@ namespace Client.Tests.UIState.DragDelete
         public void 終点待ち中にもう一度始点は選べない()
         {
             var selection = new DeleteBoxSelection();
-            selection.SelectStart(Vector3Int.zero);
-            Assert.Throws<InvalidOperationException>(() => selection.SelectStart(Vector3Int.one));
+            selection.SelectStart(Vector3Int.zero, DeleteSelectionIntent.Add);
+            Assert.Throws<InvalidOperationException>(() => selection.SelectStart(Vector3Int.one, DeleteSelectionIntent.Add));
+        }
+
+        [Test]
+        public void 始点で決めた除外の意図を終点まで保つ()
+        {
+            var selection = new DeleteBoxSelection();
+            selection.SelectStart(Vector3Int.zero, DeleteSelectionIntent.Remove);
+            selection.SetEndCell(Vector3Int.one);
+            Assert.AreEqual(DeleteSelectionIntent.Remove, selection.Intent);
+            selection.Clear();
+            selection.SelectStart(Vector3Int.zero, DeleteSelectionIntent.Add);
+            Assert.AreEqual(DeleteSelectionIntent.Add, selection.Intent);
         }
     }
 }
diff --git a/moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs b/moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs
index 06a9ee8..bbf0d32 100644
--- a/moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs
+++ b/moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs
@@ -40,6 +40,7 @@ namespace Client.Tests.UIState
             var expected = new[]
             {
                 (LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DragSelect),
+                (LocalizationKeys.Ui.KeyHint.Key.CtrlLeftDrag, LocalizationKeys.Ui.KeyHint.Text.DeselectDrag),
                 (LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick, LocalizationKeys.Ui.KeyHint.Text.BoxSelect),
                 (LocalizationKeys.Ui.KeyHint.Key.Q, LocalizationKeys.Ui.KeyHint.Text.LowerBoxHeight),
                 (LocalizationKeys.Ui.KeyHint.Key.E, LocalizationKeys.Ui.KeyHint.Text.RaiseBoxHeight),
@@ -76,7 +77,7 @@ namespace Client.Tests.UIState
         public void EveryScreenHintCountMatchesAdr()
         {
             Assert.AreEqual(10, PlaceBlockStateHints.Hints.Count);
-            Assert.AreEqual(11, DeleteObjectStateHints.Hints.Count);
+            Assert.AreEqual(12, DeleteObjectStateHints.Hints.Count);
             Assert.AreEqual(6, PlayerInventoryStateHints.Hints.Count);
             Assert.AreEqual(5, SubInventoryStateHints.Hints.Count);
             Assert.AreEqual(2, ResearchTreeStateHints.Hints.Count);
```

- [ ] **Step 3: 実装を書く（箱の意図・解決・表示一式・入力・サービス・キーヒント）**

```diff
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxIntentView.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxIntentView.cs
new file mode 100644
index 0000000..fb8d171
--- /dev/null
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxIntentView.cs
@@ -0,0 +1,41 @@
+using Client.Common;
+using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;
+using Mooresmaster.Localization.Generated;
+using UnityEngine;
+
+namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
+{
+    /// <summary>
+    ///     箱の意図（追加／除外）ごとの表示一式。件数と0件理由の文言キー、ログ上の対象名、範囲の色と可視化オブジェクトを持つ
+    ///     Per-intent (add / remove) box presentation: count and empty-range text keys, the target label for logs, the range color and its visualizer
+    /// </summary>
+    internal sealed class DeleteBoxIntentView
+    {
+        internal static DeleteBoxIntentView CreateAdd() => new("DeleteBox", MaterialConst.NotPlaceableColor, LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange, LocalizationKeys.Ui.Tooltip.DeleteBoxEmptyRange, "removable");
+        // 除外の箱は追加の赤と見分けるため設置可の青で描く
+        // A remove box is drawn in the placeable blue to tell it from the red add box
+        internal static DeleteBoxIntentView CreateRemove() => new("DeleteDeselectBox", MaterialConst.PlaceableColor, LocalizationKeys.Ui.Tooltip.DeleteBoxSelectedInRange, LocalizationKeys.Ui.Tooltip.DeleteBoxNoSelectedInRange, "selected");
+
+        internal LocalizationKey CountKey { get; }
+        internal LocalizationKey EmptyRangeKey { get; }
+        internal string TargetLabelForLog { get; }
+        private readonly string _objectNamePrefix;
+        private readonly Color _rangeColor;
+        private CellRangeBoxVisualizer _visualizer;
+
+        private DeleteBoxIntentView(string objectNamePrefix, Color rangeColor, LocalizationKey countKey, LocalizationKey emptyRangeKey, string targetLabelForLog)
+        {
+            _objectNamePrefix = objectNamePrefix;
+            _rangeColor = rangeColor;
+            CountKey = countKey;
+            EmptyRangeKey = emptyRangeKey;
+            TargetLabelForLog = targetLabelForLog;
+        }
+
+        // 実際に表示するときだけ可視化オブジェクトを作る
+        // Create the visualizer only when something is actually shown
+        internal CellRangeBoxVisualizer GetVisualizer() => _visualizer ??= new CellRangeBoxVisualizer(_objectNamePrefix, _rangeColor);
+
+        internal void Hide() => _visualizer?.HideAll();
+    }
+}
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxSelection.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxSelection.cs
index 823647f..f0f318e 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxSelection.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxSelection.cs
@@ -1,4 +1,5 @@
 using System;
+using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
 using UnityEngine;
 
 namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
@@ -10,20 +11,22 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
     }
 
     /// <summary>
-    ///     箱の局面と始点・最後の有効終点を保持する。カーソルが外れても表示範囲は消えない
-    ///     Retain the box phase, start and last valid end so losing the cursor surface preserves the displayed range
+    ///     箱の局面・追加か除外か・始点・最後の有効終点を保持する。カーソルが外れても表示範囲は消えない
+    ///     Retain the box phase, its add/remove intent, start and last valid end so losing the cursor surface preserves the displayed range
     /// </summary>
     public class DeleteBoxSelection
     {
         public DeleteBoxPhase Phase { get; private set; } = DeleteBoxPhase.Idle;
         public Vector3Int StartCell { get; private set; }
         public Vector3Int EndCell { get; private set; }
+        public DeleteSelectionIntent Intent { get; private set; } = DeleteSelectionIntent.Add;
 
         public void SetEndCell(Vector3Int cell) => EndCell = cell;
 
-        public void SelectStart(Vector3Int cell)
+        public void SelectStart(Vector3Int cell, DeleteSelectionIntent intent)
         {
             if (Phase != DeleteBoxPhase.Idle) throw new InvalidOperationException($"SelectStart in {Phase}");
+            Intent = intent;
             StartCell = cell;
             EndCell = cell;
             Phase = DeleteBoxPhase.SelectingEnd;
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxTargetResolver.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxTargetResolver.cs
index 2d26679..6bdda7c 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxTargetResolver.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxTargetResolver.cs
@@ -5,13 +5,14 @@ using Client.Game.InGame.Train.RailGraph;
 using Client.Game.InGame.Train.View.Object.Core;
 using Game.Block.Interface.Extension;
 using Game.Blueprint;
+using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
 using UnityEngine;
 
 namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
 {
     /// <summary>
-    ///     AABBに入る撤去可能な削除対象（ブロック・レール edge・列車車両）の列を返す1本の問い合わせ。撤去不可は件数と理由をログして除く。接続線は端点ブロックの撤去に巻き込まれるので含めない
-    ///     One query returning the removable delete targets (blocks, rail edges, train cars) inside an AABB; non-removable ones are logged and dropped, connection lines ride on their endpoint blocks
+    ///     AABBに入る撤去可能な削除対象（ブロック・レール edge・列車車両）の列を返す1本の問い合わせ。撤去不可は件数と理由をログして除き、除外の箱はそのうち選択済みだけを返す。接続線は端点ブロックの撤去に巻き込まれるので含めない
+    ///     One query returning the removable delete targets (blocks, rail edges, train cars) inside an AABB; non-removable ones are logged and dropped, a remove box keeps only the selected ones, connection lines ride on their endpoint blocks
     /// </summary>
     public class DeleteBoxTargetResolver
     {
@@ -29,7 +30,7 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
         // Report each diagnostic once per box operation, then allow it for the next box
         internal void ClearDiagnostics() => _reportedDiagnostics.Clear();
 
-        internal IReadOnlyList<IDeleteTarget> Resolve(Vector3Int min, Vector3Int max, DeleteSelection selection)
+        internal IReadOnlyList<IDeleteTarget> Resolve(Vector3Int min, Vector3Int max, DeleteSelection selection, DeleteSelectionIntent intent)
         {
             var targets = new List<IDeleteTarget>();
             var newlyReportedSkipped = 0;
@@ -55,7 +56,7 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
                         LogWarningOnce($"[DeleteBox] block has no BlockGameObjectChild: {block.BlockPosInfo.OriginalPos}");
                         continue;
                     }
-                    AddIfRemovable(child);
+                    AddIfMatchesIntent(child);
                 }
             }
 
@@ -71,7 +72,7 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
                         continue;
                     }
                     if (!chain.IntersectsCellBox(min, max)) continue;
-                    AddIfRemovable(rail);
+                    AddIfMatchesIntent(rail);
                 }
             }
 
@@ -86,17 +87,17 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
                         LogWarningOnce($"[DeleteBox] train car has no TrainCarEntityChildrenObject: {car.TrainCarInstanceId}");
                         continue;
                     }
-                    AddIfRemovable(child);
+                    AddIfMatchesIntent(child);
                 }
             }
 
-            // 撤去不可は理由をログして除く（表示件数・0件拒否・追加が同じ列を使う）
-            // Drop non-removable targets with their reason logged so count, empty-check and add share one list
-            void AddIfRemovable(IDeleteTarget target)
+            // 撤去不可は理由をログして除き、除外の箱はさらに選択済みだけに絞る（表示件数・0件拒否・反映が同じ列を使う）
+            // Drop non-removable targets with their reason logged and keep only selected ones for a remove box, so count, empty-check and apply share one list
+            void AddIfMatchesIntent(IDeleteTarget target)
             {
                 if (selection.IsTargetSelectable(target, out var denyReason))
                 {
-                    targets.Add(target);
+                    if (intent == DeleteSelectionIntent.Add || selection.IsSelected(target)) targets.Add(target); // silent-ok: 除外の箱で未選択は外す物が無い / 範囲内の選択済み件数のツールチップで見える
                     return;
                 }
                 var diagnostic = $"[DeleteBox] skipped non-removable target key={target.GetDeleteTargetKey()} reason={denyReason?.Key ?? "(none)"}";
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxInput.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxInput.cs
index d9832a2..7736385 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxInput.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxInput.cs
@@ -1,11 +1,11 @@
 using System.Collections.Generic;
-using Client.Common;
 using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
 using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height;
 using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;
 using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
 using Client.Game.InGame.Control;
 using Client.Game.InGame.UI.Tooltip;
+using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
 using Client.Input;
 using Mooresmaster.Localization.Generated;
 using UnityEngine;
@@ -20,8 +20,8 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
     }
 
     /// <summary>
-    ///     Shift+左クリックで始点、次の左クリックで終点を取る箱選択の入力・セル解決・可視化。終点確定時に選択へ直接足す。高さは設置モードと共用のオフセット
-    ///     Box-selection input (Shift+click start, click end), cell resolution and visuals; adds to the selection directly on end confirm. Height shares the placement offset
+    ///     Shift+左クリックで始点、次の左クリックで終点を取る箱選択の入力・セル解決・可視化。終点確定時に選択へ直接反映する（始点押下で Ctrl なら選択から外す）。高さは設置モードと共用のオフセット
+    ///     Box-selection input (Shift+click start, click end), cell resolution and visuals; applies to the selection on end confirm (removes when Ctrl was held at the start press). Height shares the placement offset
     /// </summary>
     public class DeleteBoxInput
     {
@@ -29,8 +29,10 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
         private readonly DeleteBoxTargetResolver _resolver;
         private readonly PlacementHeightOffset _heightOffset;
         private readonly DeleteBoxSelection _selection = new();
-        private CellRangeBoxVisualizer _visualizer;
+        private readonly DeleteBoxIntentView _addView = DeleteBoxIntentView.CreateAdd();
+        private readonly DeleteBoxIntentView _removeView = DeleteBoxIntentView.CreateRemove();
         private bool _isPressRegistered;
+        private DeleteSelectionIntent _pressIntent = DeleteSelectionIntent.Add;
 
         private bool IsSelectingEnd => _selection.Phase == DeleteBoxPhase.SelectingEnd;
 
@@ -48,7 +50,9 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
             return IsSelectingEnd || HybridInput.GetKey(KeyCode.LeftShift);
         }
 
-        internal void Update(DeleteSelection deleteSelection, List<TooltipLine> tooltipLines, bool isPointerOverUi)
+        // heldIntent は呼び出し側がこのフレームに1回読んだ Ctrl の意図
+        // heldIntent is the Ctrl intent the caller read once this frame
+        internal void Update(DeleteSelection deleteSelection, List<TooltipLine> tooltipLines, bool isPointerOverUi, DeleteSelectionIntent heldIntent)
         {
             PlacementHeightKeyInput.Apply(_heightOffset);
             var isShiftHeld = HybridInput.GetKey(KeyCode.LeftShift);
@@ -60,7 +64,7 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
             if (!hasCursorCell)
             {
                 if (isClicked) Debug.Log("[DeleteBox] click refused: cursor has no placement surface");
-                if (!IsSelectingEnd) { _visualizer?.HideAll(); return; } // silent-ok: 始点未選択で指す面なし / 始点表示を隠して未選択を示す
+                if (!IsSelectingEnd) { HideViewsExcept(null); return; } // silent-ok: 始点未選択で指す面なし / 始点表示を隠して未選択を示す
                 tooltipLines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceGroundNotFound));
                 isClicked = false;
             }
@@ -75,7 +79,13 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
             // Fold a press the box owns (ClaimsLeftPress) and a release outside UI into one click; a release over UI is dropped with a reason
             DeleteBoxClick ConsumeClick()
             {
-                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !isPointerOverUi && ClaimsLeftPress()) _isPressRegistered = true;
+                // 追加か除外かは押下の瞬間の Ctrl で決める
+                // Ctrl at the press decides add or remove
+                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !isPointerOverUi && ClaimsLeftPress())
+                {
+                    _isPressRegistered = true;
+                    _pressIntent = heldIntent;
+                }
                 if (!InputManager.Playable.ScreenLeftClick.GetKeyUp || !_isPressRegistered) return DeleteBoxClick.None;
                 _isPressRegistered = false;
                 return isPointerOverUi ? DeleteBoxClick.ReleasedOverUi : DeleteBoxClick.Confirmed;
@@ -86,10 +96,10 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
                 // Shift中だけ始点候補を表示
                 // Show the start candidate only while Shift is held
                 if (isShiftHeld) ShowStart(cursorCell);
-                else _visualizer?.HideAll();
+                else HideViewsExcept(null);
                 if (isClicked)
                 {
-                    _selection.SelectStart(cursorCell);
+                    _selection.SelectStart(cursorCell, _pressIntent);
                     isClicked = false;
                     UpdateSelectingEnd();
                 }
@@ -98,34 +108,34 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
             void UpdateSelectingEnd()
             {
                 var (min, max) = CellRangeBox.Calc(_selection.StartCell, _selection.EndCell);
-                var targets = _resolver.Resolve(min, max, deleteSelection);
-                tooltipLines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange, new[] { targets.Count.ToString() }));
-                if (targets.Count == 0) tooltipLines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxEmptyRange));
-                GetVisualizer().ShowSelectingEnd(_selection.StartCell, _selection.EndCell, min, max);
+                var intent = _selection.Intent;
+                var view = ViewFor(intent);
+                var targets = _resolver.Resolve(min, max, deleteSelection, intent);
+                tooltipLines.Add(new TooltipLine(view.CountKey, new[] { targets.Count.ToString() }));
+                if (targets.Count == 0) tooltipLines.Add(new TooltipLine(view.EmptyRangeKey));
+                HideViewsExcept(view);
+                view.GetVisualizer().ShowSelectingEnd(_selection.StartCell, _selection.EndCell, min, max);
                 if (!isClicked) return;
 
                 // 空範囲拒否を表示とログへ記録
                 // Record empty-range refusal in the tooltip and log
                 if (targets.Count == 0)
                 {
-                    Debug.Log($"[DeleteBox] end click refused: no removable targets in {min}-{max}");
+                    Debug.Log($"[DeleteBox] end click refused: no {view.TargetLabelForLog} targets in {min}-{max}");
                     return;
                 }
-                var added = deleteSelection.AddBoxTargets(targets);
-                Debug.Log($"[DeleteBox] added={added} of {targets.Count} selected={deleteSelection.Count}");
+                var changed = deleteSelection.ApplyBoxTargets(targets, intent);
+                Debug.Log($"[DeleteBox] {intent} changed={changed} of {targets.Count} selected={deleteSelection.Count}");
                 Clear();
             }
 
-            // 始点を実際に表示するときだけ可視化オブジェクトを作る
-            // Create the visualizer only when the start marker is actually shown
+            // 押す前の始点表示は今押している Ctrl の意図で描く
+            // Before the press, the start marker follows the Ctrl intent currently held
             void ShowStart(Vector3Int cell)
             {
-                GetVisualizer().ShowSelectingStart(cell);
-            }
-
-            CellRangeBoxVisualizer GetVisualizer()
-            {
-                return _visualizer ??= new CellRangeBoxVisualizer("DeleteBox", MaterialConst.NotPlaceableColor);
+                var view = ViewFor(heldIntent);
+                HideViewsExcept(view);
+                view.GetVisualizer().ShowSelectingStart(cell);
             }
 
             #endregion
@@ -149,7 +159,19 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
             _resolver.ClearDiagnostics();
             _heightOffset.ResetToGround();
             _isPressRegistered = false;
-            _visualizer?.HideAll();
+            HideViewsExcept(null);
+        }
+
+        // 意図による表示の選び分けはここ1か所
+        // The only place that picks presentation by intent
+        private DeleteBoxIntentView ViewFor(DeleteSelectionIntent intent) => intent == DeleteSelectionIntent.Remove ? _removeView : _addView;
+
+        // 指定以外の意図の表示を隠す（null で両方）
+        // Hide every intent's visuals except the given one (null hides both)
+        private void HideViewsExcept(DeleteBoxIntentView kept)
+        {
+            if (!ReferenceEquals(_addView, kept)) _addView.Hide();
+            if (!ReferenceEquals(_removeView, kept)) _removeView.Hide();
         }
     }
 }
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs
index 6b0eaf8..e253aba 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs
@@ -9,6 +9,7 @@ using Client.Game.InGame.UI.ProgressBar;
 using Client.Game.InGame.UI.Tooltip;
 using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
 using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
+using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
 using Client.Input;
 using Mooresmaster.Localization.Generated;
 using UnityEngine;
@@ -42,7 +43,7 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
             InputManager.Playable.Interact.OnGetKeyUp += () => _hold.Advance(false, 0f);
             _tooltip = tooltip;
             _selection.OnDeleteCompleted.Subscribe(_results.Receive);
-            _selection.OnSelectionChanged.Subscribe(_ => _hold.SetSubject(_selection.HasSelection() ? DeleteHoldSubject.Selection(_selection.Revision) : DeleteHoldSubject.None));
+            _selection.OnSelectionChanged.Subscribe(_ => _hold.SetSubject(_selection.HasSelection() ? SelectionHoldSubject() : DeleteHoldSubject.None));
         }
 
         public void Update(float deltaTime, bool isPointerOverUi)
@@ -54,9 +55,13 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
             var aim = DeleteTargetRaycaster.AimAt(_selection.AimFilter);
             var hovered = aim.Target;
 
-            // 箱は終点確定のフレームに選択へ直接足す
-            // The box adds to the selection itself on the frame the end point is confirmed
-            _box.Update(_selection, _lines, isPointerOverUi);
+            // Ctrl の意図はこのフレームに1回だけ読み、箱となぞりで共有する
+            // Read the Ctrl intent once this frame and share it between the box and the stroke
+            var heldIntent = DeleteSelectionModifierKey.ReadIntent();
+
+            // 箱は終点確定のフレームに選択へ直接反映する
+            // The box applies to the selection itself on the frame the end point is confirmed
+            _box.Update(_selection, _lines, isPointerOverUi, heldIntent);
 
             HandleStrokeStart();
             if (_isStroking) AddDenyLine(_selection.UpdateStroke(aim));
@@ -89,7 +94,9 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
                 // ホバーの赤は選択側が引き継ぐ（追加時に再び赤になる）
                 // The selection takes over the hover red (adding re-reds it)
                 _selection.SetHoverPreview(null);
-                _selection.BeginStroke();
+                // 押下の瞬間の Ctrl で追加か除外かを決める
+                // Ctrl at the press decides whether the stroke adds or removes
+                _selection.BeginStroke(heldIntent);
                 _isStroking = true;
             }
 
@@ -104,7 +111,7 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
             void UpdateHoldConfirm()
             {
                 if (!_hold.Advance(InputManager.Playable.Interact.GetKey, deltaTime)) return;
-                var confirmedSubject = _selection.HasSelection() ? DeleteHoldSubject.Selection(_selection.Revision) : subject;
+                var confirmedSubject = _selection.HasSelection() ? SelectionHoldSubject() : subject;
                 _isStroking = false;
                 if (!_selection.TryCommit(confirmedSubject, out var reason)) AddDenyLine(reason);
             }
@@ -118,6 +125,10 @@ namespace Client.Game.InGame.UI.UIState.State.DragDelete
             #endregion
         }
 
+        // 選択集合の長押し対象（件数が満了時間を決める）
+        // Hold subject for the selection; its count decides the hold duration
+        private DeleteHoldSubject SelectionHoldSubject() => DeleteHoldSubject.Selection(_selection.Revision, _selection.Count);
+
         // ESC/右短押し: 長押しを戻し、終点待ちの箱 → 選択の全解除 の順に1段だけ畳む。畳む物が無ければ false（呼び出し側がモード終了を判断）
         // ESC / right short press: reset the hold, then fold the pending box, else clear the selection; false when nothing to fold (caller decides to exit)
         public bool TryCancel()
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs
index d0bab8d..fd5f9b9 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs
@@ -133,6 +133,7 @@ namespace Client.Game.InGame.UI.UIState.State
         public static readonly IReadOnlyList<KeyHint> Hints = new[]
         {
             new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DragSelect),
+            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.CtrlLeftDrag, LocalizationKeys.Ui.KeyHint.Text.DeselectDrag),
             new KeyHint(LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick, LocalizationKeys.Ui.KeyHint.Text.BoxSelect),
             new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Q, LocalizationKeys.Ui.KeyHint.Text.LowerBoxHeight),
             new KeyHint(LocalizationKeys.Ui.KeyHint.Key.E, LocalizationKeys.Ui.KeyHint.Text.RaiseBoxHeight),
```

`DeleteObjectService.cs` のパッチは plan 作成時点の HEAD からの全差分（Task 1 Step 3・Task 2 Step 3 の分を含む）。Task 1・2 で入れた分は除き、`heldIntent` を1回読んで `_box.Update(..., heldIntent)` と `_selection.BeginStroke(heldIntent)` に渡す差分だけを当てる。

- [ ] **Step 4: ADR 0032 の表を更新する**

`docs/adr/0032-key-hint-hud-owned-by-ui-state.md` の破壊モード行を次へ置き換える:

```markdown
| 破壊モード | 左ドラッグ まとめて選択／Ctrl+左ドラッグ 選択から外す／Shift+左クリック 範囲選択／Q 範囲の高さを下げる／E 範囲の高さを上げる／F（長押し） 選択を削除／G 破壊モード終了／B ビルドメニュー／Tab インベントリ／中クリック 設置物をスポイト／Ctrl+Z 元に戻す／V 視点切替 |
```

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DeleteBox|DeleteSelection|DragDelete|DeleteHold|UIStateKeyHintCatalogTest|RightShortPressTransitionTest|UIStateFocusRestorationTest|UIStateControlTest"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts docs/adr/0032-key-hint-hud-owned-by-ui-state.md moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs
git commit -m "feat: Ctrl+なぞり・Ctrl+Shift箱で削除候補を選択から外す"
```

### Task 4: unityプレイ録画テスト

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/delete-tool/delete-ctrl-deselect-and-hold-tiers.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/delete-tool-box-select-hold-confirm.cs:1-2,55-59`、`.agents/skills/unity-playmode-recorded-playtest/scenarios/train/delete-tool-pier-removal-undo-rail.cs:107`、`.agents/skills/unity-playmode-recorded-playtest/scenarios/connect/delete-tool-cut-and-undo-connection-lines.cs:125`

**Interfaces:**
- Consumes: Task 1〜3 の挙動、`DeleteBoxScenarioInput`（`Start`/`Click`/`AimAtGround`/`AimAtCell`/`ReadRequiredTooltipCount`/`HasTooltipLine`/`TooltipText`）、GameObject 名 `DeleteDeselectBoxRangeBox`
- Produces: 録画 mp4・result.json（報告に添付）

- [ ] **Step 1: 新規シナリオを書く**

```diff
diff --git a/.agents/skills/unity-playmode-recorded-playtest/scenarios/building/delete-tool/delete-ctrl-deselect-and-hold-tiers.cs b/.agents/skills/unity-playmode-recorded-playtest/scenarios/building/delete-tool/delete-ctrl-deselect-and-hold-tiers.cs
new file mode 100644
index 0000000..d835046
--- /dev/null
+++ b/.agents/skills/unity-playmode-recorded-playtest/scenarios/building/delete-tool/delete-ctrl-deselect-and-hold-tiers.cs
@@ -0,0 +1,100 @@
+// 削除ツール: F長押しの満了時間が選択件数で伸びること（5件=1.25秒・3件=0.75秒）と、Ctrl+クリック／Ctrl+Shift箱で選択から外せることを実操作で確かめる
+// Delete tool: hold time grows with the selection count (5 = 1.25s, 3 = 0.75s), and Ctrl+click / Ctrl+Shift box removes targets from the selection, via production input
+using Client.Game.InGame.UI.UIState;
+using Client.Game.InGame.Context;
+using Client.Game.InGame.UI.ProgressBar;
+using Client.Playtest;
+using Client.Playtest.Input;
+using Client.Playtest.Operations;
+using Client.Playtest.Operations.Ui;
+using static Client.Playtest.Operations.Ui.DeleteBoxScenarioInput;
+using Cysharp.Threading.Tasks;
+using Game.Block.Interface;
+using Mooresmaster.Localization.Generated;
+using UnityEngine;
+using UnityEngine.InputSystem;
+using VContainer;
+
+return PlaytestRunner.Run("delete-ctrl-deselect-and-hold-tiers", new PlaytestRunOptions { Record = true }, async p =>
+{
+    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
+    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
+    p.WarpPlayer(new Vector3(4.5f, 33.5f, -2f));
+    var progress = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ProgressBarState>();
+
+    // 準備: 木のチェストを5つ一列に直置き
+    // Setup: place five wooden chests in a row directly
+    var cells = new[] { new Vector3Int(2, 32, 4), new Vector3Int(4, 32, 4), new Vector3Int(6, 32, 4), new Vector3Int(8, 32, 4), new Vector3Int(10, 32, 4) };
+    for (var x = 1; x <= 11; x++)
+    for (var z = 1; z <= 8; z++)
+        p.Assert(p.GetBlock(new Vector3Int(x, 32, z)) == null, $"準備: fixture範囲({x},32,{z})は空いている");
+    foreach (var cell in cells) PlaytestBlockOps.PlaceBlockDirect("木のチェスト", cell, BlockDirection.North);
+    foreach (var cell in cells) await p.WaitBlockGameObject(cell);
+    await p.PressKey(Key.G);
+    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
+
+    // Step 1: 箱で5件選ぶ
+    // Step 1: box-select five chests
+    p.Note("Step 1: Shift+クリック2点の箱で5件選ぶ");
+    await Start(p, new Vector3Int(1, 32, 3));
+    await AimAtGround(p, new Vector3Int(11, 32, 5));
+    await p.Until(() => ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange) == 5, 5f, $"Step1: 範囲内5件 実際:{TooltipText()}");
+    await Click();
+    await p.Until(() => ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 5, 5f, $"Step1: 選択中5件 実際:{TooltipText()}");
+    await p.Screenshot("01-five-selected");
+
+    // Step 2: 5件は1.25秒なので1秒押しても消えない（離すと進捗は戻る）
+    // Step 2: five targets take 1.25s, so a 1s hold deletes nothing (release resets progress)
+    p.Note("Step 2: 5件選択でF長押し1秒→まだ消えない（5〜19件は1.25秒）");
+    SemanticInput.KeyDown(Key.F);
+    await p.WaitSeconds(1.0f);
+    await p.Screenshot("02-five-holding-1s");
+    p.Assert(progress.IsShown, "Step2: 長押し中は進捗バーが出る");
+    foreach (var cell in cells) p.Assert(p.GetBlock(cell) != null, $"Step2: 1秒では{cell}を撤去しない");
+    SemanticInput.KeyUp(Key.F);
+    await p.WaitSeconds(0.3f);
+    p.Assert(!progress.IsShown && ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 5, "Step2: 離すと進捗が消え選択は残る");
+
+    // Step 3: Ctrl+クリックで1件外す
+    // Step 3: Ctrl+click removes one target from the selection
+    p.Note("Step 3: Ctrl+クリックでチェスト1を選択から外す");
+    await AimAtCell(p, cells[0]);
+    SemanticInput.KeyDown(Key.LeftCtrl);
+    await UniTask.DelayFrame(3);
+    await Click();
+    SemanticInput.KeyUp(Key.LeftCtrl);
+    await p.Until(() => ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 4, 5f, $"Step3: 選択中4件 実際:{TooltipText()}");
+    await p.Screenshot("03-ctrl-click-deselected");
+
+    // Step 4: Ctrl+Shift の箱でもう1件外す（青い箱・範囲内の選択済み件数）
+    // Step 4: a Ctrl+Shift box removes another target (blue box, selected-in-range count)
+    p.Note("Step 4: Ctrl+Shift+クリック2点の箱でチェスト2を選択から外す");
+    await AimAtGround(p, new Vector3Int(3, 32, 3));
+    SemanticInput.KeyDown(Key.LeftCtrl);
+    SemanticInput.KeyDown(Key.LeftShift);
+    await UniTask.DelayFrame(3);
+    await Click();
+    SemanticInput.KeyUp(Key.LeftShift);
+    SemanticInput.KeyUp(Key.LeftCtrl);
+    await AimAtGround(p, new Vector3Int(5, 32, 5));
+    await p.Until(() => HasTooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxSelectedInRange) && ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteBoxSelectedInRange) == 1, 5f, $"Step4: 範囲内の選択済み1件 実際:{TooltipText()}");
+    p.Assert(GameObject.Find("DeleteDeselectBoxRangeBox") != null && GameObject.Find("DeleteDeselectBoxRangeBox").activeSelf, "Step4: 除外の箱が表示される");
+    await p.Screenshot("04-ctrl-shift-box");
+    await Click();
+    await p.Until(() => ReadRequiredTooltipCount(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 3, 5f, $"Step4: 選択中3件 実際:{TooltipText()}");
+
+    // Step 5: 3件は0.75秒。外した2件は残り、選択中の3件だけ消える
+    // Step 5: three targets take 0.75s; only the three selected are deleted and the two removed remain
+    p.Note("Step 5: 3件選択でF長押し→0.75秒で3件だけ撤去、外した2件は残る");
+    await AimAtCell(p, cells[0]);
+    SemanticInput.KeyDown(Key.F);
+    await p.WaitSeconds(0.5f);
+    p.Assert(p.GetBlock(cells[2]) != null, "Step5: 0.5秒ではまだ撤去しない");
+    await p.Until(() => p.GetBlock(cells[2]) == null && p.GetBlock(cells[3]) == null && p.GetBlock(cells[4]) == null, 15f, "Step5: 選択中の3件が撤去される");
+    SemanticInput.KeyUp(Key.F);
+    p.Assert(p.GetBlock(cells[0]) != null && p.GetBlock(cells[1]) != null, "Step5: 選択から外した2件は残る");
+    await p.Screenshot("05-three-deleted");
+
+    await p.PressKey(Key.Escape);
+    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
+});
```

時間の検査は下限だけを実時間で見る（録画負荷で fps が落ちると `Time.deltaTime` の上限で実時間の方が長くなるため、上限は `Until` の最終到達で見る）。5件の1秒で消えないこと＝1件の段（0.75秒）でないことの観測になる。

- [ ] **Step 2: 既存シナリオの「2秒」表記を直す**

- `delete-tool-box-select-hold-confirm.cs:1-2` のコメントの `F長押し(2秒)`／`F hold (2s)` を `F長押し(件数で0.75〜1.75秒)`／`F hold (0.75-1.75s by count)` にする。
- 同 `:55` の Note を `"Step 2: F長押し0.75秒（1件）で選択を削除"`、`:57` を `await p.WaitSeconds(0.75f * 0.5f);`、`:59` を `await p.WaitSeconds(0.75f * 0.5f + 0.3f);` にする（`03-holding` のスクショが満了前に撮れるように）。
- `delete-tool-pier-removal-undo-rail.cs:107` と `delete-tool-cut-and-undo-connection-lines.cs:125` のコメント `Fを2秒長押し` を `Fを長押し（件数で最大1.75秒）` にする（待ち `2f + 0.3f` は最大段より長いので据え置き）。

- [ ] **Step 3: 録画を実行する**

Run（worktree ルートで。master ピンは run-scenario.sh が自動解決）:
```bash
SKILL=.agents/skills/unity-playmode-recorded-playtest
for s in building/delete-tool/delete-ctrl-deselect-and-hold-tiers building/delete-tool-box-select-hold-confirm building/delete-tool/delete-hold-mode-reentry building/delete-tool/delete-hold-text-focus building/delete-tool/delete-selection-animation building/build-undo-ctrl-z train/delete-tool-pier-removal-undo-rail connect/delete-tool-cut-and-undo-connection-lines; do "$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/$s.cs"; done
```
Expected: 8本とも result.json が成功。新規シナリオの録画で、Step 2 の1秒時点で5件が残り進捗バーが出ていること、Step 4 で青い箱と「範囲内の選択済み: 1件」が出ていること、Step 5 で外した2件だけが残ることを目視で確かめ、スクショのパスを報告へ書く。

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios
git commit -m "test: 削除ツールのCtrl除外とF長押し段階の録画シナリオを足す"
```

### Task 5: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 4 の録画（少なくとも新規シナリオと `delete-tool-box-select-hold-confirm`）を反映後のバイナリで再実施してから完了とする。

### Task 6: PR 作成

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PR本文に ADR 0084・録画のスクショ/動画パスと「本PR外のリファクタ提案」節（下記）を載せる。PR を出したら worktree は `moores-wt rm delete-hold-steps-and-ctrl-deselect` で畳む。

## 本PR外のリファクタ提案

- 既存側 `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/BuildUndoService.cs:36`（`HybridInput.GetKey(KeyCode.LeftControl) || HybridInput.GetKey(KeyCode.LeftCommand)`）／新設側 `DragDelete/Selection/DeleteSelectionModifierKey.ReadIntent()`／同じ Ctrl・Command の修飾キー判定。共通の入力ヘルパーへ寄せるかは人間が決める（plan のタスクには入れない）。

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0084-delete-tool-hold-time-steps-by-count-and-ctrl-deselect.md`（ADR 0079 の「件数に関係なく2秒」「個別除外なし」を覆す）。裁定: `.decisions/2026-10-19-削除ツールのF長押しは件数で0.75秒1.25秒1.75秒の3段にする.md`、`.decisions/2026-10-19-削除ツールの候補はCtrl押下中のなぞりと箱で外す.md`、`.decisions/2026-10-19-削除ツールの除外の箱は撤去可能な選択済みだけを外す.md`。
- 件数は `DeleteHoldSubject` に載せてプッシュする（`DeleteHoldConfirm` に `SetTargetCount` を別に生やさない）。件数が変わるのは選択が変わるときだけで、その瞬間に subject は既に作り直されている。出所: agent前提（ADR 0079 の「選択変更は UniRx で長押しへ通知」に乗る）。
- `DeleteHoldSubject.TargetCount` は判別子つき struct のまま置き、None は 0（`Advance` が None で満了時間を引く前に抜けるので読まれない）。種類別の型に分けない。出所: シミュレーター予測・確信高（前提宣言として提示 2026-10-19。拒否権つき）。
- 意図による分岐は選択モデルの入口に集める: なぞりは `BeginStroke(DeleteSelectionIntent)`（旧 `BeginStroke()` を廃し既存テスト 13 ファイルを一括置換）、箱は `ApplyBoxTargets(targets, intent)`。サービスと箱入力は意図を渡すだけ。出所: agent前提（配置検査の違反是正。ADR 0079「選択モデルがストロークを所有」・AGENTS.md「同種の条件分岐は一箇所へ」）。
- 除外の箱は追加の箱と同じ解決（撤去可能）のうち選択済みだけを外す。出所: ユーザー裁定 2026-10-19 選択「撤去できる物だけ外す」。
- 型閉包検査の弱い指摘は ①Ctrl を1フレーム1回だけ読む（サービスが読み、箱へ `heldIntent` で渡す）②箱の意図ごとの表示を `DeleteBoxIntentView` にまとめ、選び分けを `DeleteBoxInput.ViewFor` 1か所にする ④`Add` 内の `Contains` を `IsSelected` に寄せる、を直す。③（長押し対象の式2回）⑤（外すなぞりの失敗理由が bool）は据え置く。出所: ユーザー裁定 2026-10-19 選択「①②④を直す」。
- `DeleteSelection` は 197 行で除外を足すと 200 行を超えるため、内部集合（Dictionary・設置演出の赤復元購読・赤戻し待ちキュー・Revision 通知）を `DeleteSelectedTargetSet` へ切り出した。出所: agent前提（200行規約）。
- 除外の箱は `MaterialConst.PlaceableColor`（青）、GameObject 名接頭辞 `DeleteDeselectBox`。始点マーカーは既存どおり赤。出所: agent前提（追加の赤と見分ける最小の手段）。
- Ctrl 単独押下中のツールチップ案内・除外の箱専用キーヒントは足さない（ADR 0084「キーヒントは Ctrl+左ドラッグ 1件」）。出所: agent前提。
- ランタイム挙動（入力・UI）に触れるため unityプレイ録画テスト（Task 4）を含める。
