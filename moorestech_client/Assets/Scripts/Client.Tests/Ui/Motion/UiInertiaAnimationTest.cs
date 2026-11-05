using System.Collections.Generic;
using Client.Ui.Foundation.Motion.Inertia;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Motion
{
    /// <summary>
    /// 慣性の変位が単調で上限 v0/k を超えず、速度が閾値を下回る時刻で終わることを確かめる（L0）
    /// Verifies inertia displacement is monotonic, bounded by v0/k, and ends when speed drops below the threshold (L0)
    /// </summary>
    public class UiInertiaAnimationTest
    {
        private const float InitialVelocity = 2f;
        private const float Decay = 0.005f;
        private const float StopVelocity = 0.01f;

        [Test]
        public void DisplacementIsMonotonicAndBounded()
        {
            var step = new RecordingInertiaStep();
            var animation = new UiInertiaAnimation(InitialVelocity, Decay, StopVelocity, step);
            animation.Begin();
            for (var t = 0; t <= 1100; t += 10) animation.Advance(t);

            var limit = InitialVelocity / Decay;
            for (var i = 1; i < step.Applied.Count; i++) Assert.Greater(step.Applied[i], step.Applied[i - 1]);
            Assert.LessOrEqual(step.Applied[step.Applied.Count - 1], limit);
        }

        [Test]
        public void EndsWhenSpeedFallsBelowStop()
        {
            // ln(v0/stop)/k ≈ 1059.7ms
            // ln(v0/stop)/k ≈ 1059.7ms
            var expectedEnd = Mathf.Log(InitialVelocity / StopVelocity) / Decay;
            Assert.AreEqual(1059.7f, expectedEnd, 0.1f);

            var animation = new UiInertiaAnimation(InitialVelocity, Decay, StopVelocity, new RecordingInertiaStep());
            animation.Begin();
            Assert.IsFalse(animation.Advance(1059f));
            Assert.IsTrue(animation.Advance(1060f));
        }

        private sealed class RecordingInertiaStep : IUiInertiaStep
        {
            public readonly List<float> Applied = new();

            public void Apply(float offset)
            {
                Applied.Add(offset);
            }
        }
    }
}
