using System.Collections.Generic;
using Client.Ui.Foundation.Motion.Core;
using Client.Ui.Foundation.Motion.Tween;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Ui.Motion
{
    /// <summary>
    /// 手で進める時計で UiMotionTimeline と tween の進み方を確かめる（L0）
    /// Verifies UiMotionTimeline and tween progression with a manually advanced clock (L0)
    /// </summary>
    public class UiMotionTimelineTest
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void OnceLinearAppliesProgressAndCompletesOnce()
        {
            var (clock, step, timeline) = Build(new UiMotionSpec(200f, 0f, UiEasing.Linear, UiMotionLoop.Once));
            var completedCount = 0;
            timeline.Completed.Subscribe(_ => completedCount++);

            timeline.Start();
            Assert.IsFalse(timeline.Tick());
            clock.Advance(100);
            Assert.IsFalse(timeline.Tick());
            clock.Advance(100);
            Assert.IsTrue(timeline.Tick());

            CollectionAssert.AreEqual(new[] { 0f, 0.5f, 1f }, step.Applied);
            Assert.AreEqual(1, completedCount);
            Assert.IsFalse(timeline.IsRunning);
        }

        [Test]
        public void DelayWritesNothingUntilItElapses()
        {
            var (clock, step, timeline) = Build(new UiMotionSpec(200f, 100f, UiEasing.Linear, UiMotionLoop.Once));
            timeline.Start();
            timeline.Tick();
            clock.Advance(99);
            timeline.Tick();
            Assert.IsEmpty(step.Applied);

            clock.Advance(51);
            timeline.Tick();
            Assert.AreEqual(1, step.Applied.Count);
            Assert.AreEqual(0.25f, step.Applied[0], Tolerance);
        }

        [Test]
        public void RestartRewindsAndReschedulesTheEnd()
        {
            var (clock, step, timeline) = Build(new UiMotionSpec(200f, 0f, UiEasing.Linear, UiMotionLoop.Once));
            timeline.Start();
            clock.Advance(100);
            timeline.Tick();

            // 寿命の張り直し: 再生し直した時刻から 200ms で終わる
            // Lifetime renewal: ends 200ms after the restart
            timeline.Restart();
            Assert.IsFalse(timeline.Tick());
            Assert.AreEqual(0f, step.Applied[step.Applied.Count - 1], Tolerance);
            clock.Advance(150);
            Assert.IsFalse(timeline.Tick());
            clock.Advance(50);
            Assert.IsTrue(timeline.Tick());
        }

        [Test]
        public void CancelDuringDelayStopsApplyAndCompletion()
        {
            var (clock, step, timeline) = Build(new UiMotionSpec(200f, 100f, UiEasing.Linear, UiMotionLoop.Once));
            var completedCount = 0;
            timeline.Completed.Subscribe(_ => completedCount++);
            timeline.Start();
            clock.Advance(50);
            timeline.Tick();

            timeline.Cancel();
            clock.Advance(500);
            Assert.IsFalse(timeline.Tick());
            Assert.IsEmpty(step.Applied);
            Assert.AreEqual(0, completedCount);
        }

        [Test]
        public void RepeatNeverEndsAndWrapsAround()
        {
            var (clock, step, timeline) = Build(new UiMotionSpec(200f, 0f, UiEasing.Linear, UiMotionLoop.Repeat));
            timeline.Start();
            clock.Advance(250);
            Assert.IsFalse(timeline.Tick());
            Assert.AreEqual(0.25f, step.Applied[0], Tolerance);
            Assert.IsTrue(timeline.IsRunning);
        }

        [Test]
        public void PingPongGoesBackOnOddCycles()
        {
            var (clock, step, timeline) = Build(new UiMotionSpec(200f, 0f, UiEasing.Linear, UiMotionLoop.PingPong));
            timeline.Start();
            clock.Advance(250);
            timeline.Tick();
            clock.Advance(50);
            Assert.IsFalse(timeline.Tick());
            Assert.AreEqual(0.75f, step.Applied[0], Tolerance);
            Assert.AreEqual(0.5f, step.Applied[1], Tolerance);
        }

        [Test]
        public void EaseOutCubicAtHalfIsSevenEighths()
        {
            var (clock, step, timeline) = Build(new UiMotionSpec(200f, 0f, UiEasing.EaseOutCubic, UiMotionLoop.Once));
            timeline.Start();
            clock.Advance(100);
            timeline.Tick();
            Assert.AreEqual(1f - 0.5f * 0.5f * 0.5f, step.Applied[0], Tolerance);
        }

        private static (ManualClock, RecordingStep, UiMotionTimeline) Build(UiMotionSpec spec)
        {
            var clock = new ManualClock();
            var step = new RecordingStep();
            return (clock, step, new UiMotionTimeline(clock, new UiTweenAnimation(spec, step)));
        }

        private sealed class ManualClock : IUiMotionClock
        {
            private long _nowMilliseconds = 1000;

            public void Advance(long milliseconds)
            {
                _nowMilliseconds += milliseconds;
            }

            public long GetNowMilliseconds()
            {
                return _nowMilliseconds;
            }
        }

        private sealed class RecordingStep : IUiTweenStep
        {
            public readonly List<float> Applied = new();

            public void Apply(float easedProgress)
            {
                Applied.Add(easedProgress);
            }
        }
    }
}
