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
    /// 倍率の見張りの検知と通知を確かめる（L1）
    /// Verifies scale watch detection and notification (L1)
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

        [Test]
        public void 読めない状態のErrorは入ったとき1回で読めるようになるとまた出る()
        {
            var watch = new StageScaleWatch(rootVisualElement);
            watch.Start();
            simulate.FrameUpdate();

            // 倍率 0 は読めない状態。続くフレームでは連打しない
            // Scale 0 is unreadable; following frames do not repeat the error
            LogAssert.Expect(LogType.Error, new Regex(@"^\[StageScale\] stage scale is unreadable"));
            pixelsPerPoint = 0f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();

            // 読めたら旗が戻り、次の読めない状態はまた知らせる
            // Once readable the flag clears, so the next unreadable spell is reported again
            LogAssert.Expect(LogType.Log, new Regex(@"^\[StageScale\] stage scale changed"));
            pixelsPerPoint = 1.5f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.Expect(LogType.Error, new Regex(@"^\[StageScale\] stage scale is unreadable"));
            pixelsPerPoint = 0f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }
    }
}
