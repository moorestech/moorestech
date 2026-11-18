using Client.Ui.Components.NodeGraph;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components.NodeGraph.Logic
{
    /// <summary>
    /// ドラッグ中の速度の追跡と滑走の可否（Web usePanInertia の trackMove・release の規則）
    /// Velocity tracking during a drag and the fling decision (rules of the Web usePanInertia trackMove and release)
    /// </summary>
    public class NodeGraphPanInertiaTrackerTest
    {
        [Test]
        public void 最初の移動は速度を作らず等間隔の移動で滑走が始まる()
        {
            var tracker = new NodeGraphPanInertiaTracker();
            tracker.TrackMove(new Vector2(16f, 0f), 16);
            Assert.IsFalse(tracker.TryStartFling(16, out _), "a single sample has no velocity");

            // 16ms ごとに 16px（1px/ms）を 3 回足すと 0.15px/ms を超える
            // Three more 16 px moves every 16 ms (1 px/ms) exceed 0.15 px/ms
            tracker.TrackMove(new Vector2(16f, 0f), 32);
            tracker.TrackMove(new Vector2(16f, 0f), 48);
            tracker.TrackMove(new Vector2(16f, 0f), 64);
            Assert.IsTrue(tracker.TryStartFling(70, out var velocity));
            Assert.That(velocity.x, Is.GreaterThan(NodeGraphPanVelocity.MinFlingSpeed).And.LessThan(1f));
            Assert.AreEqual(0f, velocity.y, 1e-5f);
        }

        [Test]
        public void 離す前に80msを超えて止まっていれば滑走しない()
        {
            var tracker = MovingTracker();
            Assert.IsFalse(tracker.TryStartFling(64 + NodeGraphPanVelocity.ReleaseStallMs + 1, out _));
            Assert.IsTrue(MovingTracker().TryStartFling(64 + NodeGraphPanVelocity.ReleaseStallMs, out _));
        }

        [Test]
        public void 間隔が80msを超えた移動は速度を0に戻す()
        {
            var tracker = MovingTracker();
            tracker.TrackMove(new Vector2(16f, 0f), 64 + NodeGraphPanVelocity.SampleMaxGapMs + 1);
            Assert.IsFalse(tracker.TryStartFling(64 + NodeGraphPanVelocity.SampleMaxGapMs + 1, out _));
        }

        [Test]
        public void 同じ時刻の移動は速度を変えずリセットで消える()
        {
            // 同じフレームに届いた 2 つ目の移動は間隔 0 で、速度を壊さない（D-7）
            // A second move in the same frame has a zero gap and leaves the velocity intact (D-7)
            var tracker = MovingTracker();
            tracker.TryStartFling(64, out var before);
            tracker.TrackMove(new Vector2(16f, 0f), 64);
            Assert.IsTrue(tracker.TryStartFling(64, out var after));
            Assert.AreEqual(before, after);

            tracker.Reset();
            Assert.IsFalse(tracker.TryStartFling(64, out _), "reset clears the samples");
        }

        private static NodeGraphPanInertiaTracker MovingTracker()
        {
            var tracker = new NodeGraphPanInertiaTracker();
            for (var time = 16; time <= 64; time += 16) tracker.TrackMove(new Vector2(16f, 0f), time);
            return tracker;
        }
    }
}
