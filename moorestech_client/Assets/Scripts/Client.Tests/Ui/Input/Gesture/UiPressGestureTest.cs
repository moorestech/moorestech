using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input.Gesture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input.Gesture
{
    /// <summary>
    /// 押下の状態機械を確かめる（L1）
    /// Verifies the press state machine (L1)
    /// </summary>
    public class UiPressGestureTest : UiTestHost
    {
        private VisualElement _pad;
        private VisualElement _child;
        private RecordingPressHandler _handler;

        [SetUp]
        public void MountPad()
        {
            _pad = new VisualElement { name = "press-pad" };
            _pad.style.width = 300;
            _pad.style.height = 200;
            _child = new VisualElement { name = "press-pad-child" };
            _child.style.width = 60;
            _child.style.height = 60;
            _pad.Add(_child);
            rootVisualElement.Add(_pad);
            _handler = new RecordingPressHandler();
            new UiPressGesture(_pad, _handler);
            simulate.FrameUpdate();
        }

        [Test]
        public void 閾値未満で離すとタップで押下点の要素を返し捕捉を外す()
        {
            var start = _child.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);
            Assert.IsTrue(_pad.HasPointerCapture(PointerId.mousePointerId), "captured at press");
            simulate.MouseMove(start + new Vector2(4f, 0f));
            simulate.MouseUp(start + new Vector2(4f, 0f), MouseButton.LeftMouse);
            simulate.FrameUpdate();

            CollectionAssert.AreEqual(new[] { "start", "tap:press-pad-child" }, _handler.Calls);
            Assert.IsFalse(_pad.HasPointerCapture(PointerId.mousePointerId));
        }

        [Test]
        public void 閾値を超えた回でドラッグになり押下点からの全量を渡しタップしない()
        {
            var start = _child.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);
            simulate.MouseMove(start + new Vector2(6f, 0f));

            // ドラッグになったことは受け手への最初の移動の知らせで分かる
            // The receiver learns the drag started from its first move notification
            CollectionAssert.AreEqual(new[] { "start", "move:6,0|6,0" }, _handler.Calls);
            simulate.MouseMove(start + new Vector2(10f, 2f));
            simulate.MouseUp(start + new Vector2(10f, 2f), MouseButton.LeftMouse);
            simulate.FrameUpdate();

            CollectionAssert.AreEqual(new[] { "start", "move:6,0|6,0", "move:10,2|4,2", "end" }, _handler.Calls);
        }

        [Test]
        public void 右ボタンの押下は無視し捕捉もしない()
        {
            simulate.MouseDown(_child.worldBound.center, MouseButton.RightMouse);
            Assert.IsFalse(_pad.HasPointerCapture(PointerId.mousePointerId));
            simulate.MouseUp(_child.worldBound.center, MouseButton.RightMouse);
            simulate.FrameUpdate();
            CollectionAssert.IsEmpty(_handler.Calls);
        }

        [Test]
        public void 主ボタン以外の解放は進行中の押下を終わらせない()
        {
            var start = _child.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);
            simulate.MouseDown(start, MouseButton.RightMouse);
            simulate.MouseUp(start, MouseButton.RightMouse);
            CollectionAssert.AreEqual(new[] { "start" }, _handler.Calls);

            simulate.MouseUp(start, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "start", "tap:press-pad-child" }, _handler.Calls);
        }

        [Test]
        public void 捕捉を失うと中断でタップにもドロップにもしない()
        {
            var start = _child.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);

            // 別の要素が捕捉を奪う（捕捉の移り変わりは次の配送で処理される）
            // Another element steals the capture (capture changes are processed on the next dispatch)
            var thief = new VisualElement { name = "capture-thief" };
            rootVisualElement.Add(thief);
            thief.CapturePointer(PointerId.mousePointerId);
            simulate.MouseMove(start);
            simulate.FrameUpdate();
            thief.ReleasePointer(PointerId.mousePointerId);
            simulate.MouseUp(start, MouseButton.LeftMouse);
            simulate.FrameUpdate();

            CollectionAssert.AreEqual(new[] { "start", "abort:False" }, _handler.Calls);
        }
    }
}
