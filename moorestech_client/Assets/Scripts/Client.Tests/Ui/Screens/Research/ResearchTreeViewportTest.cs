using System;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Research;
using Client.Ui.State.Research.Tree;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究ツリーの視野（中央寄せ・開き直しで残る・ホイールとパン・ノードのドラッグとタップ）（L1）
    /// Research tree viewport (centering, kept across reopen, wheel and pan, node drag and tap) (L1)
    /// </summary>
    public class ResearchTreeViewportTest : ScreenTestHost
    {
        private Texture2D _texture;
        private RecordingResearchTreeCommands _commands;
        private ResearchTreeView _view;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            var content = Kit.LoadMasterAndContent(_texture);
            _commands = new RecordingResearchTreeCommands();
            _view = new ResearchTreeView(_commands, content, Toaster, new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            _view.Render(Kit.Tree(ResearchSelection.None));
            _view.SetShown(true);
            UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.OverlayPanel, UiFamily.Stage).Add(_view);
            Settle();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/research/researchViewport.spec.ts">research tree opens centered on the researchable node</web-test>
        [Test]
        public void 研究可能なノードを中央にして開く()
        {
            var viewport = Find<VisualElement>(rootVisualElement, "research-viewport").worldBound.center;
            var node = ResearchableCard().worldBound.center;
            Assert.AreEqual(viewport.x, node.x, 1.5f);
            Assert.AreEqual(viewport.y, node.y, 1.5f);
        }

        /// <web-test file="e2e/tests/research/researchViewport.spec.ts">research tree keeps its pan position across close and reopen</web-test>
        [Test]
        public void パンした位置は閉じて開き直しても残る()
        {
            var before = ResearchableCard().worldBound;
            Drag(EmptyBackgroundPoint(), new Vector2(-60f, 30f));
            var panned = ResearchableCard().worldBound;
            Assert.LessOrEqual(panned.x - before.x, -59.5f);
            Assert.GreaterOrEqual(panned.y - before.y, 29.5f);

            // 閉じて開き直す（Model は入り直しで選択の無い同じ並びを配る）
            // Close and reopen (the Model hands the same order without selection on re-entry)
            _view.SetShown(false);
            Settle();
            _view.SetShown(true);
            _view.Render(Kit.Tree(ResearchSelection.None));
            Settle();
            var reopened = ResearchableCard().worldBound;
            Assert.AreEqual(panned.x, reopened.x, 1f);
            Assert.AreEqual(panned.y, reopened.y, 1f);
        }

        /// <web-test file="e2e/tests/research/researchViewport.spec.ts">research tree zooms with the wheel and pans by dragging its empty background</web-test>
        [Test]
        public void ホイールで拡縮し空の背景のドラッグでパンし右ドラッグでは動かない()
        {
            // ホイール 4 ノッチ（Web の deltaY 240）でカーソルを中心に拡大し、戻すと縮む
            // Four notches (Web deltaY 240) zoom in around the cursor, and the reverse shrinks
            var beforeZoom = ResearchableCard().worldBound;
            var cursor = beforeZoom.center;
            Wheel(cursor, -4f);
            var zoomed = ResearchableCard().worldBound;
            Assert.Greater(zoomed.width, beforeZoom.width);
            Assert.AreEqual(cursor.x, zoomed.center.x, 1f);
            Assert.AreEqual(cursor.y, zoomed.center.y, 1f);
            Wheel(cursor, 4f);
            Assert.Less(ResearchableCard().worldBound.width, zoomed.width);

            // 空の背景を左ドラッグするとドラッグ量以上に動く（慣性の上限は C4-d の L1 が見る）
            // A left drag on empty background moves at least the drag distance (C4-d's L1 owns the glide cap)
            var grip = EmptyBackgroundPoint();
            var beforePan = ResearchableCard().worldBound;
            Drag(grip, new Vector2(-80f, -50f));
            var afterPan = ResearchableCard().worldBound;
            Assert.LessOrEqual(afterPan.x - beforePan.x, -79.5f);
            Assert.LessOrEqual(afterPan.y - beforePan.y, -49.5f);

            // 右ドラッグはパンしない
            // A right drag does not pan
            simulate.MouseDown(grip, MouseButton.RightMouse);
            simulate.MouseMove(grip + new Vector2(-80f, -50f));
            simulate.MouseUp(grip + new Vector2(-80f, -50f), MouseButton.RightMouse);
            Settle();
            Assert.AreEqual(afterPan.position, ResearchableCard().worldBound.position);
        }

        /// <web-test file="e2e/tests/research/researchViewport.spec.ts">ノードを掴んだドラッグもパンになり、詳細ペインは開かない</web-test>
        [Test]
        public void ノードを掴んだドラッグもパンになり選択しない()
        {
            var before = ResearchableCard().worldBound;
            Drag(before.center, new Vector2(-60f, 30f));
            var after = ResearchableCard().worldBound;
            Assert.LessOrEqual(after.x - before.x, -59.5f);
            Assert.GreaterOrEqual(after.y - before.y, 29.5f);
            CollectionAssert.IsEmpty(_commands.Issued);
        }

        /// <web-test file="e2e/tests/research/researchViewport.spec.ts">閾値未満のノード押下はタップとして詳細ペインを開く</web-test>
        [Test]
        public void 閾値未満の押下はタップとして選択を切り替えパンしない()
        {
            var before = ResearchableCard().worldBound;
            Drag(before.center, new Vector2(2f, 2f));
            CollectionAssert.AreEqual(new[] { $"toggle:{Kit.Researchable}" }, _commands.Issued);
            var after = ResearchableCard().worldBound;
            Assert.AreEqual(before.x, after.x, 0.5f);
            Assert.AreEqual(before.y, after.y, 0.5f);
        }

        #region Helpers

        private VisualElement ResearchableCard()
        {
            return Find<VisualElement>(rootVisualElement, $"research-node-{Kit.Researchable}");
        }

        // ビューポートの右下から内へ探し、カードの無い点を返す（Web の findEmptyBackgroundPoint）
        // Probe inward from the viewport's bottom right for a point with no card (Web findEmptyBackgroundPoint)
        private Vector2 EmptyBackgroundPoint()
        {
            var box = Find<VisualElement>(rootVisualElement, "research-viewport").worldBound;
            for (var inset = 40f; inset < Mathf.Min(box.width, box.height) / 2f; inset += 8f)
            {
                var point = new Vector2(box.xMax - inset, box.yMax - inset);
                var picked = rootVisualElement.panel.Pick(point);
                if (picked == null || picked.GetFirstAncestorOfType<ResearchTreeView>() == null) continue;
                if (picked.ClassListContains("research-tree__card") || picked.GetFirstAncestorOfType<Client.Ui.Screens.Research.Parts.ResearchNodeCard>() != null) continue;
                return point;
            }
            throw new InvalidOperationException("研究ツリーに空の背景の掴み点が無い");
        }

        private void Drag(Vector2 from, Vector2 delta)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(from + delta * 0.5f);
            simulate.MouseMove(from + delta);
            simulate.MouseUp(from + delta, MouseButton.LeftMouse);
            Settle();
        }

        private void Wheel(Vector2 point, float notches)
        {
            // 負の y が上（Web の deltaY < 0 ＝拡大）。1 ノッチは scrollDeltaPerTick（C4-d の WheelNotches と同じ）
            // Negative y is up (Web deltaY < 0 = zoom in); one notch is scrollDeltaPerTick (same as C4-d WheelNotches)
            simulate.ScrollWheel(new Vector2(0f, notches * WheelEvent.scrollDeltaPerTick), point);
            Settle();
        }

        // 逃げの読み取り・中央寄せ・慣性の数フレームを流す
        // Run a few frames for clearance reads, centering and glide
        private void Settle()
        {
            for (var i = 0; i < 4; i++) simulate.FrameUpdate();
        }

        #endregion
    }
}
