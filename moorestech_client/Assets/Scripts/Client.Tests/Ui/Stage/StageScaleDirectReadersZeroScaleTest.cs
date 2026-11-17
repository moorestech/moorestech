using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Filter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Stage
{
    /// <summary>
    /// 倍率が読めない間、PixelFilterGroup は入口で LogError 1 回だけ出して等倍で描き、連打しない（L1）
    /// While the scale is unreadable, PixelFilterGroup logs once on entry and draws at unit scale without repeating (L1)
    /// </summary>
    public class StageScaleDirectReadersZeroScaleTest : UiTestHost
    {
        [TearDown]
        public void RestoreScale()
        {
            pixelsPerPoint = 1f;
        }

        [Test]
        public void 倍率0の間は入口で1回だけLogErrorし等倍で描く()
        {
            var group = new PixelFilterGroup(PixelFilterGroupFlow.Fill);
            rootVisualElement.Add(group);
            simulate.FrameUpdate();

            pixelsPerPoint = 0f;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[PixelFilterGroup\] stage scale is unreadable"));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            // 2 回目以降の解決では LogError が増えない（未処理の LogError が残れば Unity が落とす）
            // Later resolutions add no LogError (Unity fails the test on any unexpected one)
            Assert.AreEqual(1f, group.style.scale.value.value.x, 0.001f, "unit scale");
        }
    }
}
