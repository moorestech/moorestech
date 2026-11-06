using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Stage.ScaleReading;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Stage
{
    /// <summary>
    /// 倍率の見張りが 16:9 のまま倍率だけ変わる変更を拾い、変わったときだけ流すことを確かめる（L1。S2-notification の停止条件 (b)）
    /// Verifies the scale watch catches a scale-only change at an unchanged 16:9 panel and publishes only on change (L1; S2-notification stop condition (b))
    /// </summary>
    public class StageScaleWatchTest : UiTestHost
    {
        [Test]
        public void パネルの倍率が変わったときだけ倍率を流す()
        {
            var received = new List<float>();
            var watch = new StageScaleWatch(rootVisualElement);
            using var subscription = watch.Scale.Subscribe(received.Add);
            watch.Start();
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            // パネルの論理寸法（16:9）はそのまま、1 パネル px の実画素だけを変える（1280×720 → 1920×1080 相当）
            // Keep the logical 16:9 panel size and change only real pixels per panel px (1280x720 -> 1920x1080 equivalent)
            LogAssert.Expect(LogType.Log, new Regex(@"^\[StageScale\] stage scale changed 1 -> 1\.5"));
            pixelsPerPoint = 1.5f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[StageScale\] stage scale changed 1\.5 -> 1"));
            pixelsPerPoint = 1f;
            simulate.FrameUpdate();

            CollectionAssert.AreEqual(new[] { 1f, 1.5f, 1f }, received);
        }
    }
}
