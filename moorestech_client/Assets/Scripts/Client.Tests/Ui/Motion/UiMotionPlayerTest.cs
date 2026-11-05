using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Motion.Steps;
using Client.Ui.Foundation.Motion.Tween;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Motion
{
    /// <summary>
    /// パネルのスケジューラで UiMotion が進み、パネルから外れたら止まることを確かめる（L1）
    /// Verifies UiMotion advances on the panel scheduler and stops when detached from the panel (L1)
    /// </summary>
    public class UiMotionPlayerTest : UiTestHost
    {
        private VisualElement _target;
        private int _completedCount;

        [SetUp]
        public void AddTarget()
        {
            _target = new VisualElement();
            _target.style.opacity = 0f;
            rootVisualElement.Add(_target);
            _completedCount = 0;
        }

        [Test]
        public void OpacityTweenAdvancesWithFrameTimeAndCompletesOnce()
        {
            PlayFadeIn();

            simulate.FrameUpdateMs(0);
            Assert.AreEqual(0f, _target.resolvedStyle.opacity, 0.05f);
            simulate.FrameUpdateMs(100);
            Assert.AreEqual(0.5f, _target.resolvedStyle.opacity, 0.05f);
            simulate.FrameUpdateMs(250);
            Assert.AreEqual(1f, _target.resolvedStyle.opacity, 0.0001f);
            simulate.FrameUpdateMs(100);
            Assert.AreEqual(1, _completedCount);
        }

        [Test]
        public void DetachStopsWithoutCompletion()
        {
            PlayFadeIn();
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(100);
            var opacityAtDetach = _target.style.opacity.value;

            // 外した後に付け直してもフレームを進めて再開・完了しない
            // Re-attaching and advancing frames must not resume or complete
            _target.RemoveFromHierarchy();
            simulate.FrameUpdateMs(100);
            rootVisualElement.Add(_target);
            simulate.FrameUpdateMs(100);
            simulate.FrameUpdateMs(250);
            simulate.FrameUpdateMs(250);

            Assert.AreEqual(opacityAtDetach, _target.style.opacity.value);
            Assert.AreEqual(0, _completedCount);
        }

        [Test]
        public void RepeatedPlayAndCancelOnSameElementLeavesNothingToFireOnDetach()
        {
            // 同じ要素で再生と取り消しを繰り返した後に外しても通知も例外も出ない
            // Detaching after repeated play and cancel on one element raises no notification or exception
            for (var i = 0; i < 20; i++) PlayFadeIn().Cancel();
            simulate.FrameUpdateMs(0);

            Assert.DoesNotThrow(() =>
            {
                _target.RemoveFromHierarchy();
                simulate.FrameUpdateMs(100);
            });
            Assert.AreEqual(0, _completedCount);
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(200f, float.NaN)]
        public void NonFiniteDurationOrDelayIsRefused(float duration, float delay)
        {
            // 非有限の尺・遅延は Once でも終わらない tween になるので作れない
            // A non-finite duration or delay would never end even for Once, so the spec cannot be built
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new UiMotionSpec(duration, delay, UiEasing.Linear, UiMotionLoop.Once));
        }

        private UiMotionHandle PlayFadeIn()
        {
            var spec = new UiMotionSpec(200f, 0f, UiEasing.Linear, UiMotionLoop.Once);
            var handle = UiMotionPlayer.Play(_target, new UiTweenAnimation(spec, new OpacityTweenStep(_target, 0f, 1f)));
            handle.Completed.Subscribe(_ => _completedCount++);
            return handle;
        }
    }
}
