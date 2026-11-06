using Client.Ui.Foundation.Input.Gesture;
using NUnit.Framework;

namespace Client.Tests.Ui.Input.Gesture
{
    public class UiDragThresholdTest
    {
        /// <web-test file="src/shared/pointerGesture/dragThreshold.test.ts">閾値未満はfalse</web-test>
        [Test]
        public void 閾値未満はドラッグにならない()
        {
            Assert.IsFalse(UiDragThreshold.Exceeded(3f, 3f));
        }

        /// <web-test file="src/shared/pointerGesture/dragThreshold.test.ts">閾値ちょうどはtrue</web-test>
        [Test]
        public void 閾値ちょうどでドラッグになる()
        {
            Assert.IsTrue(UiDragThreshold.Exceeded(UiDragThreshold.ThresholdPx, 0f));
        }

        /// <web-test file="src/shared/pointerGesture/dragThreshold.test.ts">斜め移動もhypotで判定する</web-test>
        [Test]
        public void 斜めの移動は距離で判定する()
        {
            Assert.IsTrue(UiDragThreshold.Exceeded(4f, 4f));
        }

        /// <web-test file="src/shared/pointerGesture/dragThreshold.test.ts">上へドラッグでscrollTop増加</web-test>
        [Test]
        public void 上へドラッグするとスクロール位置が増える()
        {
            Assert.AreEqual(130f, UiDragThreshold.NextScrollTop(100f, -30f));
        }

        /// <web-test file="src/shared/pointerGesture/dragThreshold.test.ts">下へドラッグでscrollTop減少</web-test>
        [Test]
        public void 下へドラッグするとスクロール位置が減る()
        {
            Assert.AreEqual(60f, UiDragThreshold.NextScrollTop(100f, 40f));
        }
    }
}
