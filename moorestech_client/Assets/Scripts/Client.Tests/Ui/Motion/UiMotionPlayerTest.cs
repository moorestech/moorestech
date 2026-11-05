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

            _target.RemoveFromHierarchy();
            simulate.FrameUpdateMs(100);
            simulate.FrameUpdateMs(250);

            Assert.AreEqual(opacityAtDetach, _target.style.opacity.value);
            Assert.AreEqual(0, _completedCount);
        }

        [Test]
        public void RestartRewindsFromTheNextFrameTime()
        {
            var handle = PlayFadeIn();
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(150);

            // 再生し直しは次のフレームの時刻を頭にして 0 から進む
            // Restart takes the next frame's time as the head and runs from 0
            handle.Restart();
            simulate.FrameUpdateMs(0);
            Assert.AreEqual(0f, _target.resolvedStyle.opacity, 0.0001f);
            simulate.FrameUpdateMs(100);
            Assert.AreEqual(0.5f, _target.resolvedStyle.opacity, 0.05f);
            Assert.AreEqual(0, _completedCount);
            simulate.FrameUpdateMs(150);
            Assert.AreEqual(1, _completedCount);
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
