using Client.Ui.Components;
using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Motion.Steps;
using Client.Ui.Foundation.Motion.Tween;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Toast;
using System.Collections.Generic;
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

        private readonly StyleSheet _toastSheet;
        private readonly List<GameUiToast> _toastsWaitingForStyle = new();
        private float _enterMilliseconds;
        private float _exitMilliseconds;
        private bool _hasDurations;

        public ToastHostView(StyleSheet toastSheet)
        {
            _toastSheet = toastSheet;
            name = "toast-host";
            styleSheets.Add(toastSheet);
            AddToClassList("toast-host");
            pickingMode = PickingMode.Ignore;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        public void Push(GameUiToast toast)
        {
            // 出入り時間は USS の通知トークンから読むため、解決前の Push は解決後まで預かる
            // Fade durations come from the USS notification tokens, so a Push before resolution is held until it resolves
            if (!_hasDurations)
            {
                _toastsWaitingForStyle.Add(toast);
                return;
            }

            // 透明で足してから濃くし、表示時間のあとに薄れて外す
            // Add it transparent and fade it in, then fade it out and remove it after the display time
            var item = new ToastItem(toast, _toastSheet);
            item.style.opacity = 0f;
            Add(item);
            Fade(item, 0f, 1f, _enterMilliseconds);
            item.schedule.Execute(() => FadeOutThenRemove(item)).StartingIn(VisibleMilliseconds);

            #region Internal

            void FadeOutThenRemove(ToastItem target)
            {
                var handle = Fade(target, 1f, 0f, _exitMilliseconds);
                handle.Completed.Subscribe(_ => target.RemoveFromHierarchy());
            }

            #endregion
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // 読めなければ Reader が LogError を出し、預かったトーストは出せないまま残る
            // If unreadable the reader logs an error and the held toasts stay unshown
            var hasEnter = UssCustomStyleReader.TryReadMilliseconds(customStyle, "--toast-host-enter-duration", out _enterMilliseconds);
            var hasExit = UssCustomStyleReader.TryReadMilliseconds(customStyle, "--toast-host-exit-duration", out _exitMilliseconds);
            _hasDurations = hasEnter && hasExit;
            if (!_hasDurations) return;

            var waiting = _toastsWaitingForStyle.ToArray();
            _toastsWaitingForStyle.Clear();
            foreach (var toast in waiting) Push(toast);
        }

        private static UiMotionHandle Fade(VisualElement target, float from, float to, float milliseconds)
        {
            var spec = new UiMotionSpec(milliseconds, 0f, UiEasing.Linear, UiMotionLoop.Once);
            return UiMotionPlayer.Play(target, new UiTweenAnimation(spec, new OpacityTweenStep(target, from, to)));
        }
    }
}
