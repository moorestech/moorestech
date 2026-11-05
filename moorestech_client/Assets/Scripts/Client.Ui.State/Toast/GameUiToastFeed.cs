using System;
using Client.Localization;
using Client.Ui.State.Commands;
using UniRx;
using UnityEngine;

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

        // 開発者向けにはコマンド名と失敗コードをログへ残し、利用者には理由ごとの翻訳文を error で出す。良性の拒否はログだけ
        // The command name and failure code go to the log for developers; players get the translated per-reason message as an error. Benign rejections are only logged
        public void ShowRejected(string commandName, UiCommandRejected rejected)
        {
            Debug.LogWarning($"[GameUiToastFeed] {commandName} failed: {rejected.ToWireCode()}");
            if (UiCommandRejectReasonTraits.IsBenign(rejected.Reason)) return;

            Show(Localize.Get(UiCommandRejectReasonTraits.ToLocalizationKey(rejected.Reason)), GameUiToastVariant.Error);
        }
    }
}
