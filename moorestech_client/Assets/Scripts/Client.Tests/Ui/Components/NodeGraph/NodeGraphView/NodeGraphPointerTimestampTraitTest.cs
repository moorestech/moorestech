using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// 前提: シミュレートした時間がポインタ事象の時刻を進める（NodeGraphView の速度の推定がこの時刻を使う）
    /// Premise: simulated time advances pointer event timestamps (NodeGraphView estimates velocity from them)
    /// </summary>
    public class NodeGraphPointerTimestampTraitTest : UiTestHost
    {
        [TearDown]
        public void ResetSimulatedFrameTime()
        {
            // 固定値はフィクスチャで自動復帰しないので戻す
            // The fixed value is not reset by the fixture, so restore it
            simulate.ResetTimePerSimulatedFrameToDefault();
        }

        [Test]
        public void FrameUpdateMsで進めた分だけポインタ移動の時刻が進む()
        {
            var pad = new VisualElement { name = "timestamp-pad" };
            pad.style.width = 200;
            pad.style.height = 200;
            rootVisualElement.Add(pad);
            var stamps = new System.Collections.Generic.List<long>();
            pad.RegisterCallback<PointerMoveEvent>(evt => stamps.Add(evt.timestamp));
            simulate.FrameUpdate();

            // 事象ごとの自動加算（既定 200ms）を 0 にして、FrameUpdateMs の分だけを測る
            // Zero the per-event auto increment (default 200 ms) so only FrameUpdateMs advances time
            simulate.timePerSimulatedFrameMs = 0;

            // 16ms ずつ進めて 2 回動かすと、時刻の差が 16ms
            // Two moves 16 ms apart differ by 16 ms in timestamp
            var start = pad.worldBound.center;
            simulate.MouseMove(start);
            simulate.FrameUpdateMs(16);
            simulate.MouseMove(start + new Vector2(10f, 0f));
            simulate.FrameUpdateMs(16);
            simulate.MouseMove(start + new Vector2(20f, 0f));

            Assert.AreEqual(3, stamps.Count, "pointer moves received");
            Assert.AreEqual(16L, stamps[1] - stamps[0], "first gap");
            Assert.AreEqual(16L, stamps[2] - stamps[1], "second gap");
        }
    }
}
