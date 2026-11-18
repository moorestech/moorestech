using Client.Ui.Screens.Tutorial.Pulse;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 本番の TutorialOverlay.uss の画面ルートが受け直した周期で、脈動が載っている子へだけ流れることを確かめる（L1）
    /// Verifies the pulse runs only on attached children with the period re-declared at the real TutorialOverlay.uss root (L1)
    /// </summary>
    public class TutorialPulseBindingTest : TutorialOverlayTestHost
    {
        [Test]
        public void 周期の解決より後に足した子も脈動する()
        {
            // 画面は空で常駐し、チュートリアルは後から来る（ルートの解決はもう終わっている）
            // The screen is resident and empty; the tutorial arrives later, after the root has resolved
            var (host, binding) = MountPeriodHost();
            var late = new VisualElement();
            binding.Attach(late, TutorialAttentionPulse.StrongScale);
            host.Add(late);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs((long)(Ms("--tutorial-pulse-duration") / 2f));
            Assert.AreEqual(1.08f, late.resolvedStyle.scale.value.x, 0.005f);
        }

        [Test]
        public void 外した子へは周期が変わっても流さず載せ直すと再び流す()
        {
            var (host, binding) = MountPeriodHost();
            var child = new VisualElement();
            binding.Attach(child, TutorialAttentionPulse.SubtleScale);
            host.Add(child);
            simulate.FrameUpdate();
            child.RemoveFromHierarchy();

            // ルートの再解決（クラスの付け外し）で、外した子へ Play して UiMotionPlayer の LogError を出さない
            // A root re-resolution must not Play on the removed child (no UiMotionPlayer LogError)
            host.AddToClassList("tutorial-pulse-binding-test--restyle");
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();

            host.Add(child);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs((long)(Ms("--tutorial-pulse-duration") / 2f));
            Assert.AreEqual(1.03f, child.resolvedStyle.scale.value.x, 0.005f);
        }

        [Test]
        public void 同じ要素へAttachを2回しても脈動は1本で後の振幅に揃う()
        {
            var (host, binding) = MountPeriodHost();
            var child = new VisualElement();
            host.Add(child);
            var halfPeriod = (long)(Ms("--tutorial-pulse-duration") / 2f);

            // 4 分の 1 周期ずれた時点で振幅を替えて付け直すと、その時点から 1 本の脈動として流れ直す
            // Re-attaching with a new amplitude a quarter period in restarts it as a single pulse from that point
            binding.Attach(child, TutorialAttentionPulse.StrongScale);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(halfPeriod / 2);
            binding.Attach(child, TutorialAttentionPulse.SubtleScale);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(halfPeriod);
            Assert.AreEqual(TutorialAttentionPulse.SubtleScale, child.resolvedStyle.scale.value.x, 0.005f);
            simulate.FrameUpdateMs(halfPeriod);
            Assert.AreEqual(1f, child.resolvedStyle.scale.value.x, 0.005f);

            // 外して載せ直しても流れ出すのは 1 本だけで、LogError も出ない
            // Detaching and re-attaching starts only one pulse and logs no error
            child.RemoveFromHierarchy();
            host.Add(child);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(halfPeriod);
            Assert.AreEqual(TutorialAttentionPulse.SubtleScale, child.resolvedStyle.scale.value.x, 0.005f);
            LogAssert.NoUnexpectedReceived();
        }

        private (VisualElement host, TutorialPulseBinding binding) MountPeriodHost()
        {
            // 本番の View と同じく、載せる前に binding を作ってからルートを解決させる
            // As in the real View, create the binding before mounting, then let the root resolve
            var host = new VisualElement();
            host.AddToClassList("tutorial-overlay");
            var binding = new TutorialPulseBinding(host);
            rootVisualElement.Add(host);
            simulate.FrameUpdate();
            return (host, binding);
        }
    }
}
