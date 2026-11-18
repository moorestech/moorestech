using System.Collections.Generic;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input.Gesture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input.Gesture
{
    /// <summary>
    /// 押下中に要素がパネルから外れたときの中断を実パネルで確かめる（L1）
    /// Verifies the abort when the element detaches from the panel mid-press (L1)
    /// </summary>
    public class UiPressGestureDetachTest : UiTestHost
    {
        [Test]
        public void 押下中に外れると中断し再追加後の押下は通常どおりタップになる()
        {
            var pad = new VisualElement { name = "detach-pad" };
            pad.style.width = 300;
            pad.style.height = 200;
            rootVisualElement.Add(pad);
            var handler = new RecordingPressHandler();
            new UiPressGesture(pad, handler);
            simulate.FrameUpdate();

            var start = pad.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);
            pad.RemoveFromHierarchy();
            CollectionAssert.AreEqual(new[] { "start", "abort:False" }, handler.Calls);

            // 再追加後に古い押下が残っていれば、この押下は捨てられてタップにならない
            // A stale press left behind would make this press get dropped and never tap
            rootVisualElement.Add(pad);
            simulate.FrameUpdate();
            simulate.MouseDown(start, MouseButton.LeftMouse);
            simulate.MouseUp(start, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "start", "abort:False", "start", "tap:detach-pad" }, handler.Calls);
        }

        [Test]
        public void 押下中に外れるとパネルの捕捉が残らない()
        {
            var pad = MountPad();
            new UiPressGesture(pad, new RecordingPressHandler());
            simulate.FrameUpdate();
            var panel = pad.panel;

            simulate.MouseDown(pad.worldBound.center, MouseButton.LeftMouse);
            pad.RemoveFromHierarchy();
            Assert.IsNull(panel.GetCapturingElement(PointerId.mousePointerId));
        }

        [Test]
        public void ドラッグの受け手の中で源を外しても例外が出ず中断になる()
        {
            var pad = MountPad();
            var handler = new DetachingPressHandler(pad, detachOnMove: true);
            new UiPressGesture(pad, handler);
            simulate.FrameUpdate();

            var start = pad.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);
            simulate.MouseMove(start + new Vector2(20f, 0f));
            CollectionAssert.AreEqual(new[] { "start", "move", "abort:True" }, handler.Calls);
        }

        [Test]
        public void 押下開始の受け手の中で源を外しても例外が出ず再追加後のタップが届く()
        {
            var pad = MountPad();
            var handler = new DetachingPressHandler(pad, detachOnMove: false);
            new UiPressGesture(pad, handler);
            simulate.FrameUpdate();

            var start = pad.worldBound.center;
            simulate.MouseDown(start, MouseButton.LeftMouse);
            CollectionAssert.AreEqual(new[] { "start", "abort:False" }, handler.Calls);

            rootVisualElement.Add(pad);
            simulate.FrameUpdate();
            simulate.MouseDown(start, MouseButton.LeftMouse);
            simulate.MouseUp(start, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "start", "abort:False", "start", "tap" }, handler.Calls);
        }

        private VisualElement MountPad()
        {
            var pad = new VisualElement { name = "detach-pad" };
            pad.style.width = 300;
            pad.style.height = 200;
            rootVisualElement.Add(pad);
            return pad;
        }

        // 受け手の呼び出しの中で源を木から外す（再入の故障シナリオ）
        // Detaches the source inside a handler call (the re-entrancy failure scenario)
        private sealed class DetachingPressHandler : IUiPressGestureHandler
        {
            public readonly List<string> Calls = new();
            private readonly VisualElement _source;
            private readonly bool _detachOnMove;

            public DetachingPressHandler(VisualElement source, bool detachOnMove)
            {
                _source = source;
                _detachOnMove = detachOnMove;
            }

            public void OnPressStarted(Vector2 panelPosition)
            {
                Calls.Add("start");
                if (!_detachOnMove && _source.panel != null && Calls.Count == 1) _source.RemoveFromHierarchy();
            }

            public void OnDragMoved(UiPressMove move)
            {
                Calls.Add("move");
                if (_detachOnMove) _source.RemoveFromHierarchy();
            }

            public void OnDragEnded(Vector2 panelPosition) => Calls.Add("end");
            public void OnTapped(VisualElement pressTarget) => Calls.Add("tap");
            public void OnAborted(bool dragged) => Calls.Add($"abort:{dragged}");
        }
    }
}
