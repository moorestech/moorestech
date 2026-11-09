using Client.Ui.Foundation.Input.Gesture;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Input.Gesture
{
    /// <summary>
    /// 押下の閾値 5px 判定を確かめる（L0）
    /// Verifies the 5px press threshold (L0)
    /// </summary>
    public class UiPressGestureThresholdTest
    {
        [TestCase(4f, 0f, 1f, false)]
        [TestCase(5f, 0f, 1f, true)]
        [TestCase(3f, 4f, 1f, true)]
        [TestCase(3.3f, 0f, 1.5f, false)]
        [TestCase(3.4f, 0f, 1.5f, true)]
        [TestCase(2.5f, 0f, 2f, true)]
        public void 移動量は倍率を掛けた実画面pxで5px以上ならドラッグ(float dx, float dy, float stageScale, bool expected)
        {
            Assert.AreEqual(expected, UiPressGesture.ExceedsOnScreen(new Vector2(dx, dy), stageScale));
        }
    }
}
