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

            // コマンド名・失敗コード・診断を 1 行のログへ
            // Log command name, failure code and diagnostic in one line
            var wireCode = rejected.ToWireCode();
            var diagnostic = rejected.Diagnostic.Length == 0 ? string.Empty : $" ({rejected.Diagnostic})";
            Debug.LogWarning($"[UiCommandResultToaster] {commandName} failed: {wireCode}{diagnostic}");
            if (UiCommandRejectReasonTraits.IsBenign(rejected.Reason)) return;

            // 失敗コードは {code} として常に渡す。枠のある文言だけが Web と同じくコードを見せる（A-261）
            // Always pass the failure code as {code}; only texts with the slot show it, as the Web does (A-261)
            var key = UiCommandRejectReasonTraits.ToLocalizationKey(rejected.Reason);
            var text = Localize.GetFormatted(key, new[] { new LocalizationTextParam(CodeParamName, wireCode) });
            _toasts.Show(text, GameUiToastVariant.Error);
        }
    }
}
