using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Motion.Steps;
using Client.Ui.Foundation.Motion.Tween;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Pulse
{
    /// <summary>
    /// チュートリアルの注意の脈動（1 → 頂点 → 1 の拡大を繰り返す）
    /// The tutorial attention pulse (repeats 1 to peak to 1 scaling)
    /// </summary>
    public static class TutorialAttentionPulse
    {
        // 振幅は Web tokens.css の @keyframes tutorial-attention-pulse-strong/subtle の 50% の値。ここが唯一の持ち主
        // Amplitudes are the 50% values of Web tokens.css keyframes; this is their only owner
        public const float StrongScale = 1.08f;
        public const float SubtleScale = 1.03f;
        public const string PeriodPropertyName = "--tutorial-attention-pulse-period";

        public static UiMotionHandle Start(VisualElement target, float peakScale, float periodMilliseconds)
        {
            // 0%・100% が 1、50% が頂点の ease-in-out を、半周期の往復（EaseInOutSine）で写す
            // Copy the ease-in-out 1 → peak → 1 keyframes as a half-period ping-pong with EaseInOutSine
            var spec = new UiMotionSpec(periodMilliseconds / 2f, 0f, UiEasing.EaseInOutSine, UiMotionLoop.PingPong); // look-exception: LX-37
            return UiMotionPlayer.Play(target, new UiTweenAnimation(spec, new ScaleTweenStep(target, 1f, peakScale)));
        }
    }

}
