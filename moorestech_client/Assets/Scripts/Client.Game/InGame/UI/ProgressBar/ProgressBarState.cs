using System;
using UniRx;

namespace Client.Game.InGame.UI.ProgressBar
{
    /// <summary>
    ///     画面固定の進捗バーの論理状態。表示は Web UI が ui.progress topic 経由で描く
    ///     Logical state of the screen progress bar; the Web UI renders it through the ui.progress topic
    /// </summary>
    public class ProgressBarState
    {
        public bool IsShown { get; private set; }
        public float CurrentProgress { get; private set; }

        // Show/Hide/SetProgress いずれかで状態が変化したら発火する
        // Fires whenever Show/Hide/SetProgress changes the state
        public IObservable<Unit> OnProgressChanged => _onProgressChanged;
        private readonly Subject<Unit> _onProgressChanged = new();

        public void Show()
        {
            IsShown = true;
            _onProgressChanged.OnNext(Unit.Default);
        }

        public void Hide()
        {
            IsShown = false;
            _onProgressChanged.OnNext(Unit.Default);
        }

        public void SetProgress(float progress)
        {
            CurrentProgress = progress;
            _onProgressChanged.OnNext(Unit.Default);
        }
    }
}
