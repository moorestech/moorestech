using Client.Localization;
using UnityEngine;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using Server.Event.Notification;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Completion
{
    // カーソルが離れても要求対象と結果を保持する
    // Retain request targets and results independently of the cursor
    internal sealed class DeleteResultFeedback
    {
        private readonly Dictionary<object, (string label, DeleteCompletion result)> _results = new();

        internal void Receive(DeleteCompletion result)
        {
            if (result.Kind == DeleteCompletionKind.Succeeded)
            {
                _results.Remove(result.TargetKey);
                return;
            }
            var label = _results.TryGetValue(result.TargetKey, out var previous) ? previous.label : Describe(result.TargetKey);
            _results[result.TargetKey] = (label, result);
            // モード離脱後にも既存通知で失敗と未確認を届ける
            // Deliver failures and unconfirmed outcomes through notifications even after leaving the mode
            if (result.Kind != DeleteCompletionKind.Pending)
            {
                var message = result.Kind == DeleteCompletionKind.Unconfirmed ? "denied.deleteUnconfirmed" : "denied.deleteRejected";
                ClientDIContext.ClientLocalNotificationSource.Notify(NotificationMessagePack.CreateOperationDenied(message, new Dictionary<string, string> { ["target"] = label }));
            }
        }

        // 次の確定・離脱で拒否と未確認の行を消し、処理中だけ残す
        // On next commit or exit drop rejected/unconfirmed lines, keep pending
        internal void ClearSettled()
        {
            var settledKeys = new List<object>();
            foreach (var (key, item) in _results)
            {
                if (item.result.Kind != DeleteCompletionKind.Pending) settledKeys.Add(key);
            }
            foreach (var key in settledKeys) _results.Remove(key);
        }

        private static string Describe(object key)
        {
            if (key is not Component view) return key.ToString();
            // 破棄済みの対象は型名だけで表す
            // Describe destroyed targets by type name only
            return view == null ? view.GetType().Name : $"{view.name} ({view.transform.position})";
        }

        internal void AppendTo(List<TooltipLine> lines)
        {
            foreach (var item in _results.Values)
            {
                var key = item.result.Kind == DeleteCompletionKind.Pending ? LocalizationKeys.Ui.Tooltip.DeleteResultPending
                    : item.result.Kind == DeleteCompletionKind.Unconfirmed ? LocalizationKeys.Ui.Tooltip.DeleteResultUnconfirmed
                    : LocalizationKeys.Ui.Tooltip.DeleteResultRejected;
                lines.Add(new TooltipLine(key, new[] { new LocalizationTextParam("label", item.label) }));
                if (item.result.DenyReason.HasValue) lines.Add(new TooltipLine(item.result.DenyReason.Value));
            }
        }
    }
}
