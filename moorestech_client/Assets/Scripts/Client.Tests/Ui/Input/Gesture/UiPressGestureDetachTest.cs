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
    }
}
