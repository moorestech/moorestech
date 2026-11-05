using System.Collections.Generic;
using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Motion
{
    /// <summary>
    /// 本番テーマを当てたパネルで、シミュレートした時間だけ transition と schedule が進むことを確かめる（最初の L1）
    /// Verifies on a panel with the production theme that transitions and schedules advance only with simulated time (first L1)
    /// </summary>
    /// <remarks>
    /// 拡張点: 演出（UiMotion）の L1 はこの形に倣い、実時間を待たず simulate.FrameUpdateMs で時間を進めて途中値と終値を見る
    /// Extension: motion (UiMotion) L1 tests follow this shape, advancing time with simulate.FrameUpdateMs instead of waiting real time and checking mid and end values
    /// </remarks>
    public class PanelTimeAdvanceTest : UiTestHost
    {
        [Test]
        public void OpacityTransitionIsMidwayAt100msAndDoneAfterDuration()
        {
            var element = new VisualElement { name = "panel-time-advance-fade" };
            element.style.width = 100;
            element.style.height = 100;
            element.style.opacity = 1f;
            element.style.transitionProperty = new List<StylePropertyName> { new("opacity") };
            element.style.transitionDuration = new List<TimeValue> { new(200, TimeUnit.Millisecond) };
            rootVisualElement.Add(element);
            simulate.FrameUpdate();
            Assert.AreEqual(1f, element.resolvedStyle.opacity);

            // 切り替えたフレームで transition が始まり、100ms 後は途中値
            // The transition starts on the frame of the switch and is midway 100 ms later
            element.style.opacity = 0f;
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(100);
            var midway = element.resolvedStyle.opacity;
            Assert.That(midway, Is.GreaterThan(0f).And.LessThan(1f), $"opacity at 100ms was {midway}");

            // 長さ（200ms）を過ぎれば終値
            // Past the duration (200 ms) it reaches the end value
            simulate.FrameUpdateMs(150);
            Assert.AreEqual(0f, element.resolvedStyle.opacity);
        }

        [Test]
        public void ScheduledItemFiresOnlyAfterItsDelay()
        {
            var element = new VisualElement { name = "panel-time-advance-schedule" };
            rootVisualElement.Add(element);
            simulate.FrameUpdate();

            var fired = 0;
            element.schedule.Execute(() => fired++).StartingIn(300);

            // 300ms 待ちは 200ms では発火せず、400ms で 1 回発火する
            // A 300 ms delay has not fired at 200 ms and has fired once by 400 ms
            simulate.FrameUpdateMs(200);
            Assert.AreEqual(0, fired, "fired before its 300 ms delay");
            simulate.FrameUpdateMs(200);
            Assert.AreEqual(1, fired, "did not fire once by 400 ms");
        }
    }
}
