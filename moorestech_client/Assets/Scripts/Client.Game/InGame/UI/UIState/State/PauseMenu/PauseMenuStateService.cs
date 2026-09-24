using System;
using Client.Input;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.PauseMenu
{
    public class PauseMenuStateService
    {
        private readonly Subject<Unit> _onPauseMenuOpened = new();
        private readonly ReactiveProperty<PauseMenuPage> _currentPage = new(PauseMenuPage.Top);

        // 開いたことだけを知らせる。何を確保するかはバグ報告側の関心で、共有UIサービスは知らない
        // Announces only that the menu opened; what gets captured is the bug report's concern, not this shared UI service's
        public IObservable<Unit> OnPauseMenuOpened => _onPauseMenuOpened;

        // 今どの画面にいるか。Escapeを判定するのがここなので、画面の持ち主もここに置く
        // The page currently shown; Escape is judged here, so the page is owned here too
        public IReadOnlyReactiveProperty<PauseMenuPage> CurrentPage => _currentPage;

        public void ShowPage(PauseMenuPage page)
        {
            _currentPage.Value = page;
        }

        // Escapeが押されたフレームだけ1段戻りを判定し、ポーズを閉じてよいかを返す
        // Judges the one-step back only on the frame Escape is pressed and returns whether the pause may close
        public bool HandleCloseKey()
        {
            return InputManager.UI.CloseUI.GetKeyDown && StepBackOnCloseKey();
        }

        // 子画面ならトップへ戻して閉じない。トップなら閉じてよい
        // On a sub-page go back to the top and stay open; on the top the pause may close
        public bool StepBackOnCloseKey()
        {
            if (_currentPage.Value == PauseMenuPage.Top) return true;

            _currentPage.Value = PauseMenuPage.Top;
            return false;
        }

        public void OnEnter()
        {
            // 開くたびにトップから始め、前回の子画面を持ち越さない
            // Every open starts from the top and never carries over the previous sub-page
            _currentPage.Value = PauseMenuPage.Top;
            InputManager.MouseCursorVisible(true);
            _onPauseMenuOpened.OnNext(Unit.Default);
        }
    }
}
