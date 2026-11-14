using System;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Motion.Steps;
using Client.Ui.Foundation.Motion.Tween;
using Client.Ui.Foundation.Theme;
using Client.Ui.Foundation.Theme.StyleResolution;
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

        private readonly List<GameUiToast> _toastsWaitingForStyle = new();
        private readonly UssStyleResolutionGate _gate;
        private float _enterMilliseconds;
        private float _exitMilliseconds;
        private bool _durationsUnreadable;

        public ToastHostView()
        {
            name = "toast-host";
            AddToClassList("toast-host");
            pickingMode = PickingMode.Ignore;
            _gate = new UssStyleResolutionGate(this, nameof(ToastHostView));
            _gate.OnResolved.Subscribe(ReadDurations);
        }

        public void Push(GameUiToast toast)
        {
            // 出入り時間は USS の通知トークンから読むため、解決前の Push は解決後まで預かる
            // Fade durations come from the USS notification tokens, so a Push before resolution is held until it resolves
            if (!_gate.IsResolved)
            {
                _toastsWaitingForStyle.Add(toast);
                _gate.ReportHeldWhileUnresolved($"toast {toast.Text}");
                return;
            }

            // トークンが読めなかったら黙って溜めず、薄れ無しで出して毎回 LogError で知らせる（送信結果などを利用者へ届けるのを優先）
            // With unreadable tokens never hoard silently: show without fades and LogError each time (delivering e.g. send results to the player comes first)
            if (_durationsUnreadable) _gate.ReportUnreadable(toast.Text);

            // 透明で足してから濃くし、表示時間のあとに薄れて外す
            // Add it transparent and fade it in, then fade it out and remove it after the display time
            var item = new ToastItem(toast.Text, ToneOf(toast.Variant));
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

        private void ReadDurations(ICustomStyle resolvedStyle)
        {
            // 「未解決」と「解決したが読めなかった」を分けて持つ。読めなければ Reader が LogError を出し、尺は 0（薄れ無し）にする
            // Keep "unresolved" apart from "resolved but unreadable"; when unreadable the reader logs an error and the durations become 0 (no fades)
            var hasEnter = UssCustomStyleReader.TryReadMilliseconds(resolvedStyle, "--toast-host-enter-duration", out _enterMilliseconds);
            var hasExit = UssCustomStyleReader.TryReadMilliseconds(resolvedStyle, "--toast-host-exit-duration", out _exitMilliseconds);
            _durationsUnreadable = !(hasEnter && hasExit);
            if (_durationsUnreadable)
            {
                _enterMilliseconds = 0f;
                _exitMilliseconds = 0f;
            }

            var waiting = _toastsWaitingForStyle.ToArray();
            _toastsWaitingForStyle.Clear();
            foreach (var toast in waiting) Push(toast);
        }

        private static ToastItemTone ToneOf(GameUiToastVariant variant)
        {
            // 全値を列挙・未知は停止
            // List every value; fail on unknown
            return variant switch
            {
                GameUiToastVariant.Info => ToastItemTone.Info,
                GameUiToastVariant.Error => ToastItemTone.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
            };
        }

        private static UiMotionHandle Fade(VisualElement target, float from, float to, float milliseconds)
        {
            var spec = new UiMotionSpec(milliseconds, 0f, UiEasing.Linear, UiMotionLoop.Once);
            return UiMotionPlayer.Play(target, new UiTweenAnimation(spec, new OpacityTweenStep(target, from, to)));
        }
    }
}
