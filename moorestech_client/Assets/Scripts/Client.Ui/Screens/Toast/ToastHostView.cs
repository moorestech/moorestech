using Client.Ui.Components;
using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Motion.Steps;
using Client.Ui.Foundation.Motion.Tween;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Toast
{
    /// <summary>
    /// 右下に縦積みでトーストを出す常駐の入れ物。入力は奪わず、3 秒で薄れて消える
    /// Resident bottom-right stack of toasts; it never takes input and each fades out after 3 seconds
    /// </summary>
    public sealed class ToastHostView : VisualElement
    {
        private const int VisibleMilliseconds = 3000;
        private const float FadeMilliseconds = 150f;

        private readonly StyleSheet _toastSheet;

        public ToastHostView(StyleSheet toastSheet)
        {
            _toastSheet = toastSheet;
            name = "toast-host";
            styleSheets.Add(toastSheet);
            AddToClassList("toast-host");
            pickingMode = PickingMode.Ignore;
        }

        public void Push(GameUiToast toast)
        {
            // 透明で足してから濃くし、表示時間のあとに薄れて外す
            // Add it transparent and fade it in, then fade it out and remove it after the display time
            var item = new ToastItem(toast, _toastSheet);
            item.style.opacity = 0f;
            Add(item);
            Fade(item, 0f, 1f);
            item.schedule.Execute(() => FadeOutThenRemove(item)).StartingIn(VisibleMilliseconds);

            #region Internal

            void FadeOutThenRemove(ToastItem target)
            {
                var handle = Fade(target, 1f, 0f);
                handle.Completed.Subscribe(_ => target.RemoveFromHierarchy());
            }

            #endregion
        }

        private static UiMotionHandle Fade(VisualElement target, float from, float to)
        {
            var spec = new UiMotionSpec(FadeMilliseconds, 0f, UiEasing.Linear, UiMotionLoop.Once);
            return UiMotionPlayer.Play(target, new UiTweenAnimation(spec, new OpacityTweenStep(target, from, to)));
        }
    }
}
