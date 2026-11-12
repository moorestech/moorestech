using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Motion.Tween;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Notification.Parts
{
    /// <summary>
    /// 行の生存を 2 本の tween で組む: 入場（左のずれと透明から）と、生存尺の終端に終わるよう遅延を逆算した退場。トーストの前例と同じく UiTweenAnimation と Step だけで書く
    /// Builds a row's lifetime from two tweens: the enter (from the left shift and transparent) and the exit delayed so it ends at the lifetime end; written only with UiTweenAnimation and steps like the toast precedent
    /// </summary>
    public static class NotificationRowLifetime
    {
        internal static UiTweenAnimation CreateEnter(VisualElement row, NotificationRowTiming timing)
        {
            var spec = new UiMotionSpec(timing.EnterMilliseconds, 0f, UiEasing.CssEaseOut, UiMotionLoop.Once);
            return new UiTweenAnimation(spec, EnterStep(row, timing));
        }

        internal static UiTweenAnimation CreateExit(VisualElement row, NotificationRowTiming timing)
        {
            // 退場の頭は生存尺から退場の尺を引いた時刻。2 本は同じフレームで始まるので遅延で順を作る
            // The exit starts at the lifetime minus the exit duration; both start on the same frame, so the delay makes the order
            var spec = new UiMotionSpec(timing.ExitMilliseconds, timing.LifetimeMilliseconds - timing.ExitMilliseconds, UiEasing.CssEaseIn, UiMotionLoop.Once);
            return new UiTweenAnimation(spec, new NotificationRowFadeSlideStep(row, 1f, 0f, 0f, -timing.ShiftPixels));
        }

        public static NotificationRowLifetimeHandle Play(VisualElement row, NotificationRowTiming timing)
        {
            // 最初のスケジューラ呼び出しまでの 1 コマに表示の姿が出ないよう、入場の頭の姿を先に書く
            // Write the enter's head pose first so the shown pose never flashes before the first scheduler callback
            EnterStep(row, timing).Apply(0f);
            var enter = UiMotionPlayer.Play(row, CreateEnter(row, timing));
            var exit = UiMotionPlayer.Play(row, CreateExit(row, timing));
            return new NotificationRowLifetimeHandle(enter, exit);
        }

        // 生存の最終の姿（退場の終わり）を書く。動きが止まったあとの行を揃える口
        // Writes the lifetime's final pose (the exit end); the port that settles a row after its motion stopped
        public static void ApplyFinalPose(VisualElement row, NotificationRowTiming timing)
        {
            new NotificationRowFadeSlideStep(row, 1f, 0f, 0f, -timing.ShiftPixels).Apply(1f);
        }

        private static NotificationRowFadeSlideStep EnterStep(VisualElement row, NotificationRowTiming timing)
        {
            return new NotificationRowFadeSlideStep(row, 0f, 1f, -timing.ShiftPixels, 0f);
        }
    }
}
