using Client.Ui.Screens.Tutorial.Pulse;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    public class TutorialAttentionPulseTest : TutorialOverlayTestHost
    {
        [Test]
        public void 脈動は周期の半分で頂点に達し元へ戻る()
        {
            var target = new VisualElement();
            rootVisualElement.Add(target);
            var period = Ms("--tutorial-pulse-duration");
            var handle = TutorialAttentionPulse.Start(target, TutorialAttentionPulse.StrongScale, period);
            // UiMotionHandle は Play の次のスケジューラ呼び出しを開始時刻にする（前例 UiMotionPlayerTest）
            // UiMotionHandle starts at the next scheduler call after Play (precedent UiMotionPlayerTest)
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs((long)(period / 2f));
            Assert.AreEqual(TutorialAttentionPulse.StrongScale, target.resolvedStyle.scale.value.x, 0.005f);
            simulate.FrameUpdateMs((long)(period / 2f));
            Assert.AreEqual(1f, target.resolvedStyle.scale.value.x, 0.005f);
            handle.Cancel();
        }
    }
}
