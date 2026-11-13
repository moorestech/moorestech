using System;
using System.Linq;
using Client.Ui.Components.Controls;
using Client.Ui.Components.NodeGraph;
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
    /// チャレンジ一覧の視点（L1）
    /// Challenge list viewport (L1)
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

            // 選んだカテゴリの進行中へ寄る
            // Centers on the selected category's current node
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
            // カテゴリ往復で NodeGraphView は常に 1 本
            // NodeGraphView stays single across category round trips
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

        [Test]
        public void 外している間に寸法が変わると付け直したとき中央に見えていた点を中央に保つ()
        {
            PanBy(new Vector2(-60f, 30f));
            var offsetFromCenter = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound.center - GraphContentCenter();
            var sizeBefore = SingleGraph().contentRect.size;
            Select(Kit.Logistics);

            // B を見ている間に一覧の寸法を変える（舞台の枠は層の中で決まるので一覧の右・下を詰める）
            // Resize the list while showing B (the stage frame is set inside the layer, so pull in the list's right and bottom)
            _view.style.right = 200f;
            _view.style.bottom = 100f;
            Settle();
            Assert.AreNotEqual(sizeBefore, SingleGraph().contentRect.size, "寸法が変わっている");

            // A に戻ると中央に見えていた点が中央のまま（Web は保存視点のまま。例外 LX-200・計画 D-1 の帰結）
            // Back on A, the point seen at the center stays there (the Web keeps the saved viewport; look exception LX-200, plan D-1)
            Select(Kit.Basics);
            var offsetAfter = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound.center - GraphContentCenter();
            Assert.AreEqual(offsetFromCenter.x, offsetAfter.x, 1f);
            Assert.AreEqual(offsetFromCenter.y, offsetAfter.y, 1f);
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

        // 置き場の View は常に 1 本
        // Exactly one view is attached
        private NodeGraphView SingleGraph()
        {
            var graphs = rootVisualElement.Query<NodeGraphView>().ToList();
            Assert.AreEqual(1, graphs.Count, "木の中の NodeGraphView の数");
            return graphs[0];
        }

        private Vector2 GraphContentCenter()
        {
            var graph = SingleGraph();
            return graph.LocalToWorld(graph.contentRect.center);
        }

        private void AssertCentered(Guid challenge)
        {
            var viewport = Find<VisualElement>(rootVisualElement, "challenge-viewport").worldBound.center;
            var node = Kit.Card(rootVisualElement, challenge).worldBound.center;
            Assert.AreEqual(viewport.x, node.x, 1.5f, $"{challenge} の中心 x");
            Assert.AreEqual(viewport.y, node.y, 1.5f, $"{challenge} の中心 y");
        }

        // 背景をドラッグし進行中カードの移動後位置を返す
        // Drag the background and return the moved current card rect
        private Rect PanBy(Vector2 delta)
        {
            var before = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound;
            Drag(Kit.EmptyBackgroundPoint(rootVisualElement), delta);
            var after = Kit.Card(rootVisualElement, Kit.SecondStep).worldBound;
            Assert.LessOrEqual(after.x - before.x, delta.x + 0.5f);
            Assert.GreaterOrEqual(after.y - before.y, delta.y - 0.5f);
            return after;
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
