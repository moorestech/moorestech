using Client.Localization;
using Client.Ui.State.Commands;
using UnityEngine;

namespace Client.Ui.State.Toast
{
    /// <summary>
    /// 型付きコマンドの結果をトーストへ振り分ける唯一の口。受理は何もせず、拒否はログへ残し、良性でなければ翻訳文を error で出す
    /// The single place that routes typed command results to toasts: accepted does nothing, rejected is logged and, unless benign, shown as a translated error
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
            // 受理の案内（送信完了など）は画面ごとの文なので、ここでは出さない
            // Acceptance notices (such as "sent") are per-screen sentences, so none is shown here
            if (result is not UiCommandRejected rejected) return;

            // 開発者向けにコマンド名と失敗コードを必ず残す。良性の拒否はログだけで終える
            // Always log the command name and failure code for developers; benign rejections stop at the log
            Debug.LogWarning($"[UiCommandResultToaster] {commandName} failed: {rejected.ToWireCode()}");
            if (UiCommandRejectReasonTraits.IsBenign(rejected.Reason)) return;
            _toasts.Show(Localize.Get(UiCommandRejectReasonTraits.ToLocalizationKey(rejected.Reason)), GameUiToastVariant.Error);
        }
    }
}
