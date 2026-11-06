using System;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using UniRx;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    // 入れ子ポーズを持つ画面の代役。表示変化だけを起こせる
    // Stand-in for a screen with a nested pause; it can only raise presentation changes
    internal sealed class PresentationChangingNestedScreen : INestedPauseScreenState
    {
        private readonly Subject<Unit> _onPresentationChanged = new();

        public NestedPauseSubStateEnum SubState => NestedPauseSubStateEnum.GameScreen;
        public IObservable<Unit> OnPresentationChanged => _onPresentationChanged;
        public IObservable<NestedPauseSubStateEnum> OnSubStateChanged => Observable.Never<NestedPauseSubStateEnum>();

        public void RaisePresentationChanged()
        {
            _onPresentationChanged.OnNext(Unit.Default);
        }

        public bool RequestClosePauseMenu()
        {
            return false;
        }
    }
}
