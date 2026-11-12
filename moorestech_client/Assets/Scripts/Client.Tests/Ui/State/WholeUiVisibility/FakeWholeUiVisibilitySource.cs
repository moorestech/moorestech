using System;
using Client.Ui.State.WholeUiVisibility;
using UniRx;

namespace Client.Tests.Ui.State.WholeUiVisibility
{
    /// <summary>
    /// Ctrl+U とカットシーンの源の差し替え。どちらも購読時に今の値を流す（本番の UIRoot・GameStateController と同じ）
    /// Stand-in for the Ctrl+U and cutscene sources; both replay the current value on subscribe (same as the production UIRoot and GameStateController)
    /// </summary>
    public sealed class FakeWholeUiVisibilitySource : IWholeUiVisibilitySource
    {
        private readonly ReactiveProperty<bool> _uiRootVisible = new(true);
        private readonly ReactiveProperty<bool> _cutScenePlaying = new(false);

        public IObservable<bool> ObserveUiRootVisible() => _uiRootVisible;
        public IObservable<bool> ObserveCutScenePlaying() => _cutScenePlaying;

        public void SetUiRootVisible(bool visible)
        {
            _uiRootVisible.Value = visible;
        }

        public void SetCutScenePlaying(bool playing)
        {
            _cutScenePlaying.Value = playing;
        }
    }
}
