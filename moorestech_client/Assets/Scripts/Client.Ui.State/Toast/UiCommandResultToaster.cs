using Client.Localization;
using Client.Ui.State.Commands;
using UnityEngine;

namespace Client.Ui.State.Toast
{
    /// <summary>
    /// コマンド結果のトースト振り分け口
    /// Routes command results to toasts
    /// </summary>
    public sealed class UiCommandResultToaster
    {
        private readonly GameUiToastFeed _toasts;

        public UiCommandResultToaster(GameUiToastFeed toasts)
        {
            _toasts = toasts;
        }

        public void Report(string commandName, UiCommandResult result)
        {
            // 受理の案内は画面側
            // Acceptance notices are per screen
            if (result is not UiCommandRejected rejected) return;

            // コマンド名と失敗コードをログへ
            // Log command name and failure code
            Debug.LogWarning($"[UiCommandResultToaster] {commandName} failed: {rejected.ToWireCode()}");
            if (UiCommandRejectReasonTraits.IsBenign(rejected.Reason)) return;
            _toasts.Show(Localize.Get(UiCommandRejectReasonTraits.ToLocalizationKey(rejected.Reason)), GameUiToastVariant.Error);
        }
    }
}
