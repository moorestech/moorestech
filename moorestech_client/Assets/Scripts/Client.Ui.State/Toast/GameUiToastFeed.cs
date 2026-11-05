using System;
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

        // 良性の拒否はログだけ。そうでなければ Web と同じ「<コマンド名> failed: <コード>」を error で出す
        // A benign rejection is only logged; otherwise show "<command> failed: <code>" as an error, matching the Web
        public void ShowRejected(string commandName, UiCommandRejected rejected)
        {
            var text = $"{commandName} failed: {rejected.ToWireCode()}";
            Debug.LogWarning($"[GameUiToastFeed] {text}");
            if (UiCommandRejectReasonTraits.IsBenign(rejected.Reason)) return;

            Show(text, GameUiToastVariant.Error);
        }
    }
}
