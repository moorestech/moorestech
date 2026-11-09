using System.Collections.Generic;
using Client.Ui.Components.NodeGraph;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// ノード上の押下もパンになり、閾値未満の解放だけがタップになる（Web TreeViewNodeGesture.test.ts の写し）
    /// A press on a node also pans, and only a release under the threshold taps (copy of the Web TreeViewNodeGesture.test.ts)
    /// </summary>
    public class NodeGraphViewGestureTest : NodeGraphTestStage
    {
        private NodeGraphView _view;
        private VisualElement _card;
        private List<string> _taps;

        [SetUp]
        public void MountGraph()
        {
            _view = MountView(MountHost(400f, 300f));
            _card = Card("card-a");
            _view.SetGraph(new NodeGraphContent(new[] { Node("a", 0f, 0f, _card) }, NodeGraphFocus.None));
            _taps = new List<string>();
            _view.OnNodeTapped.Subscribe(_taps.Add);
            simulate.FrameUpdate();
        }

        /// <web-test file="src/shared/treeView/TreeViewNodeGesture.test.ts">selects the pressed node when the release stays under the drag threshold</web-test>
        [Test]
        public void 閾値未満で離すと押したノードを選ぶ()
        {
            var start = _card.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);
            simulate.MouseMove(start + new Vector2(3f, 2f));
            simulate.MouseUp(start + new Vector2(3f, 2f), MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "a" }, _taps);
        }

        /// <web-test file="src/shared/treeView/TreeViewNodeGesture.test.ts">keeps the canvas still until the threshold is exceeded</web-test>
        [Test]
        public void 閾値を超えるまでキャンバスは動かない()
        {
            var start = _card.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);
            simulate.MouseMove(start + new Vector2(3f, 2f));
            AssertViewport(0f, 0f, 1f, ReadViewport(_view));
        }

        /// <web-test file="src/shared/treeView/TreeViewNodeGesture.test.ts">pans from a node press and never selects once it becomes a drag</web-test>
        [Test]
        public void ノードの押下からパンしドラッグになった回は選ばない()
        {
            var start = _card.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);
            simulate.MouseMove(start + new Vector2(20f, 10f));
            AssertViewport(20f, 10f, 1f, ReadViewport(_view));
            simulate.MouseUp(start + new Vector2(20f, 10f), MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.IsEmpty(_taps);
        }

        /// <web-test file="src/shared/treeView/TreeViewNodeGesture.test.ts">taps nothing when the press point is outside every node</web-test>
        [Test]
        public void どのノードの外で押しても何も選ばない()
        {
            var outside = ContentOrigin(_view) + new Vector2(10f, 10f);
            simulate.MouseDown(outside, MouseButton.LeftMouse);
            simulate.MouseUp(outside, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.IsEmpty(_taps);
        }

        /// <web-test file="src/shared/treeView/TreeViewNodeGesture.test.ts">treats a cancelled press as an aborted gesture, not a tap</web-test>
        [Test]
        public void 取り消された押下は中断でタップにしない()
        {
            var start = _card.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);

            // UITK の取消は捕捉の喪失と同じ中断に入る。別の要素に捕捉を奪わせて起こす（前例 UiPressGestureTest）
            // In UITK a cancel enters the same abort as losing capture; trigger it by letting another element steal the capture (precedent UiPressGestureTest)
            var thief = new VisualElement { name = "capture-thief" };
            rootVisualElement.Add(thief);
            thief.CapturePointer(PointerId.mousePointerId);
            simulate.MouseMove(start);
            simulate.FrameUpdate();
            thief.ReleasePointer(PointerId.mousePointerId);
            simulate.MouseUp(start, MouseButton.LeftMouse);
            simulate.FrameUpdate();

            CollectionAssert.IsEmpty(_taps);
            AssertViewport(0f, 0f, 1f, ReadViewport(_view));
        }

        /// <web-test file="src/shared/treeView/TreeViewNodeGesture.test.ts">ignores a secondary button release and keeps the press alive</web-test>
        [Test]
        public void 副ボタンの解放は無視し押下を続ける()
        {
            var start = _card.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);
            simulate.MouseDown(start, MouseButton.RightMouse);
            simulate.MouseUp(start, MouseButton.RightMouse);
            simulate.FrameUpdate();
            CollectionAssert.IsEmpty(_taps, "the secondary release does not finish the press");

            simulate.MouseUp(start, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "a" }, _taps);
        }

        /// <web-test file="src/shared/treeView/TreeViewNodeGesture.test.ts">lets no second pointer hijack the live press</web-test>
        [Test]
        public void 第2のポインタは進行中の押下を乗っ取らない()
        {
            var start = _card.worldBound.center;
            var secondDowns = 0;
            _view.RegisterCallback<PointerDownEvent>(evt => { if (evt.pointerId == PointerId.touchPointerIdBase) secondDowns++; });
            simulate.MouseDown(start, MouseButton.LeftMouse);

            // マウスは 1 本なので、タッチの指 0（pointerId 1）の押下と解放をビューポートへ直接送る（D-9）
            // There is only one mouse, so send a touch finger-0 (pointerId 1) press and release straight to the viewport (D-9)
            using (var down = PointerDownEvent.GetPooled(SecondTouch(TouchPhase.Began, start), EventModifiers.None)) _view.SendEvent(down);
            using (var up = PointerUpEvent.GetPooled(SecondTouch(TouchPhase.Ended, start), EventModifiers.None)) _view.SendEvent(up);
            simulate.FrameUpdate();
            Assert.AreEqual(1, secondDowns, "the second pointer's press reached the viewport");
            CollectionAssert.IsEmpty(_taps, "the second pointer neither taps nor ends the press");

            simulate.MouseUp(start, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "a" }, _taps);
            AssertViewport(0f, 0f, 1f, ReadViewport(_view));
        }

        private static Touch SecondTouch(TouchPhase phase, Vector2 position)
        {
            return new Touch { fingerId = 0, phase = phase, position = position, tapCount = 1 };
        }
    }
}
