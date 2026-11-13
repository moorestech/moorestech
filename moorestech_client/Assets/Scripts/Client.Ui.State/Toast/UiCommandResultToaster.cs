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
        private const string CodeParamName = "code";

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

            // 名前・コード・診断を 1 行でログへ
            // Log name, code and diagnostic in one line
            var wireCode = rejected.ToWireCode();
            Debug.LogWarning($"[UiCommandResultToaster] {commandName} failed: {rejected.ToLogText()}");
            if (UiCommandRejectReasonTraits.IsBenign(rejected.Reason)) return;

            // 失敗コードは常に {code} へ
            // Always pass the failure code as {code} (A-261)
            var key = UiCommandRejectReasonTraits.ToLocalizationKey(rejected.Reason);
            var text = Localize.GetFormatted(key, new[] { new LocalizationTextParam(CodeParamName, wireCode) });
            _toasts.Show(text, GameUiToastVariant.Error);
        }
    }
}
