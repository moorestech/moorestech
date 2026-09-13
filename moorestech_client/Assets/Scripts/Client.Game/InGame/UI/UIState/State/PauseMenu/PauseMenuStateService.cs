using System;
using Client.Input;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.PauseMenu
{
    public class PauseMenuStateService
    {
        private readonly Subject<Unit> _onPauseMenuOpened = new();

        // 開いたことだけを知らせる。何を確保するかはバグ報告側の関心で、共有UIサービスは知らない
        // Announces only that the menu opened; what gets captured is the bug report's concern, not this shared UI service's
        public IObservable<Unit> OnPauseMenuOpened => _onPauseMenuOpened;

        public bool IsClosePause()
        {
            return InputManager.UI.CloseUI.GetKeyDown;
        }

        public void OnEnter()
        {
            InputManager.MouseCursorVisible(true);
            _onPauseMenuOpened.OnNext(Unit.Default);
        }
    }
}
