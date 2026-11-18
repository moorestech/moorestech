using System;
using UniRx;

namespace Client.Ui.State.Toast
{
    /// <summary>
    /// GameUi のトーストの流れ。コマンドの結果を受けた View がここへ出す
    /// Stream of GameUi toasts; views that receive command results push into it
    /// </summary>
    public sealed class GameUiToastFeed
    {
        private readonly Subject<GameUiToast> _onToast = new();

        public IObservable<GameUiToast> OnToast => _onToast;

        public void Show(string text, GameUiToastVariant variant)
        {
            _onToast.OnNext(new GameUiToast(text, variant));
        }
    }
}
