using System;
using System.Linq;
using Client.Ui.Components.Controls;
using Client.Ui.Components.NodeGraph;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Challenge.Tree.ChallengeListScreenTestKit;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の視点（L1。Web の challenge.spec 1 件と裁定 2026-11-04 の初回訪問・再訪・開き直し）
    /// Challenge list viewport (L1; one Web challenge.spec test and the 2026-11-04 ruling on first visit, revisit and reopen)
    /// </summary>
    public class ChallengeListViewportTest : ScreenTestHost
    {
        private RecordingChallengeListCommands _commands;
        private ChallengeListView _view;

        [SetUp]
        public void SetUp()
        {
            Kit.LoadMasterAndDictionary();
            _commands = new RecordingChallengeListCommands();
            _view = Kit.MountList(UiLayerStack.Build(rootVisualElement), _commands);
            Settle();
        }

        /// <web-test file="e2e/tests/challenge.spec.ts">チャレンジ画面は進行中のチャレンジを中央にして開き、カテゴリ切替でもその進行中へ寄せる</web-test>
        [Test]
        public void 進行中のチャレンジを中央にして開きカテゴリ切替でもその進行中へ寄せる()
        {
            AssertCentered(Kit.SecondStep);

            // ボタンでカテゴリを選ぶと操作が 1 回出て、選んだカテゴリの進行中へ寄る
            // Pressing a category button issues one operation and centers on that category's current node
            Click(CategoryButton(Kit.Logistics));
            CollectionAssert.AreEqual(new[] { Kit.Logistics }, _commands.Selected);
            Select(Kit.Logistics);
            AssertCentered(Kit.LogisticsCurrent);
            Assert.IsTrue(CategoryButton(Kit.Logistics).ClassListContains(FormButton.FilledUssClassName));
            Assert.IsTrue(CategoryButton(Kit.Basics).ClassListContains(FormButton.SubtleUssClassName));
        }

        [Test]
        public void 一度開いたカテゴリへ戻ると寄せ直さず前回の視点を戻す()
        {
            // 先頭のカテゴリでパンし、別のカテゴリへ移って戻る。木の中の NodeGraphView は常に 1 本
            // Pan in the first category, move to another and come back; the tree always holds one NodeGraphView
            var basicsView = SingleGraph();
            var panned = PanBy(new Vector2(-60f, 30f));
            Select(Kit.Logistics);
            Assert.AreNotSame(basicsView, SingleGraph(), "別のカテゴリは別の View");
            Select(Kit.Basics);

            // 戻ると控えた同じ View が付け直され、作り直さないので寄せ直さない
            // Coming back re-attaches the same kept view; no rebuild, so no re-centering
            Assert.AreSame(basicsView, SingleGraph(), "再訪は控えた View の付け直し");
            var back = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound;
            Assert.AreEqual(panned.x, back.x, 1f);
            Assert.AreEqual(panned.y, back.y, 1f);

            // 2 つ目のカテゴリも初回の寄せのまま残る
            // The second category keeps its first-visit centering
            Select(Kit.Logistics);
            AssertCentered(Kit.LogisticsCurrent);
        }

        [Test]
        public void 閉じて開き直すと先頭のカテゴリへ戻り前回の視点を戻す()
        {
            var panned = PanBy(new Vector2(-60f, 30f));
            Select(Kit.Logistics);

            // 閉じると選択ストアが無しに戻すので、開き直しは選択無しで描く（先頭のカテゴリ）
            // Closing resets the selection store, so reopening renders with no selection (the first category)
            _view.SetShown(false);
            Settle();
            _view.SetShown(true);
            _view.Render(Kit.Tree(ChallengeCategorySelection.None));
            Settle();
            var reopened = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound;
            Assert.AreEqual(panned.x, reopened.x, 1f);
            Assert.AreEqual(panned.y, reopened.y, 1f);
            Assert.IsTrue(CategoryButton(Kit.Basics).ClassListContains(FormButton.FilledUssClassName));
        }

        #region Helpers

        private FormButton CategoryButton(Guid category)
        {
            return Find<FormButton>(rootVisualElement, $"challenge-category-{category}");
        }

        private void Select(Guid category)
        {
            _view.Render(Kit.Tree(ChallengeCategorySelection.Of(category)));
            Settle();
        }

        // 置き場に付いている View は常に 1 本（名前とアンカーが重ならない）
        // Exactly one view is attached (names and anchors never repeat)
        private NodeGraphView SingleGraph()
        {
            var graphs = rootVisualElement.Query<NodeGraphView>().ToList();
            Assert.AreEqual(1, graphs.Count, "木の中の NodeGraphView の数");
            return graphs[0];
        }

        private void AssertCentered(Guid challenge)
        {
            var viewport = Find<VisualElement>(rootVisualElement, "challenge-viewport").worldBound.center;
            var node = Kit.Card(rootVisualElement, challenge).worldBound.center;
            Assert.AreEqual(viewport.x, node.x, 1.5f, $"{challenge} の中心 x");
            Assert.AreEqual(viewport.y, node.y, 1.5f, $"{challenge} の中心 y");
        }

        // 空の背景を掴んで動かし、進行中のカードが同じだけ動いたことを見て、その位置を返す
        // Drag the empty background, check the current card moved by the same amount, and return its rect
        private Rect PanBy(Vector2 delta)
        {
            var before = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound;
            Drag(EmptyBackgroundPoint(), delta);
            var after = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound;
            Assert.LessOrEqual(after.x - before.x, delta.x + 0.5f);
            Assert.GreaterOrEqual(after.y - before.y, delta.y - 0.5f);
            return after;
        }

        // カードの無い点を右上から探す
        // Probe from the top right for a point without a card
        private Vector2 EmptyBackgroundPoint()
        {
            var box = Find<VisualElement>(rootVisualElement, "challenge-viewport").worldBound;
            for (var inset = 40f; inset < Mathf.Min(box.width, box.height) / 2f; inset += 8f)
            {
                var point = new Vector2(box.xMax - inset, box.yMin + inset);
                var picked = rootVisualElement.panel.Pick(point);
                if (picked == null || picked.GetFirstAncestorOfType<NodeGraphView>() == null && !(picked is NodeGraphView)) continue;
                if (picked is PaperSurface || picked.GetFirstAncestorOfType<PaperSurface>() != null) continue;
                return point;
            }
            throw new InvalidOperationException("チャレンジ一覧に空の背景の掴み点が無い");
        }

        private void Click(VisualElement target)
        {
            simulate.MouseDown(target.worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(target.worldBound.center, MouseButton.LeftMouse);
            Settle();
        }

        private void Drag(Vector2 from, Vector2 delta)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(from + delta * 0.5f);
            simulate.MouseMove(from + delta);
            simulate.MouseUp(from + delta, MouseButton.LeftMouse);
            Settle();
        }

        // 逃げの読み取り・中央寄せ・滑走の数フレームを流す
        // Run a few frames for clearance reads, centering and glide
        private void Settle()
        {
            for (var i = 0; i < 4; i++) simulate.FrameUpdate();
        }

        #endregion
    }
}
