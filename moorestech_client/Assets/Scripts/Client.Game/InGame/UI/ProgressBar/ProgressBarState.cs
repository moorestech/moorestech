using System;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.ProgressBar
{
    /// <summary>
    ///     画面固定の進捗バーの論理状態。表示は Web UI が ui.progress topic 経由で描く
    ///     Logical state of the screen progress bar; the Web UI renders it through the ui.progress topic
    /// </summary>
    public class ProgressBarState
    {
        public bool IsShown { get; private set; }
        // 旧Scrollbar.sizeの既定値1・Clamp01挙動を維持する
        // Preserves the old Scrollbar.size default of 1 and its Clamp01 behavior
        public float CurrentProgress { get; private set; } = 1f;

        // 状態変化時に発火する
        // Fires whenever the state changes
        public IObservable<Unit> OnProgressChanged => _onProgressChanged;
        private readonly Subject<Unit> _onProgressChanged = new();

        internal void Show()
        {
            IsShown = true;
            _onProgressChanged.OnNext(Unit.Default);
        }

        internal void Hide()
        {
            IsShown = false;
            _onProgressChanged.OnNext(Unit.Default);
        }

        internal void SetProgress(float progress)
        {
            CurrentProgress = Mathf.Clamp01(progress);
            _onProgressChanged.OnNext(Unit.Default);
        }
    }
}
