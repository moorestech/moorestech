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
            var label = _results.TryGetValue(result.TargetKey, out var previous) ? previous.label : Describe(result.TargetKey);
            if (result.Kind == DeleteCompletionKind.Succeeded)
            {
                _results.Remove(result.TargetKey);
                return;
            }
            _results[result.TargetKey] = (label, result);
            // モード離脱後にも既存通知で失敗と未確認を届ける
            // Deliver failures and unconfirmed outcomes through notifications even after leaving the mode
            if (result.Kind != DeleteCompletionKind.Pending)
            {
                var message = result.Kind == DeleteCompletionKind.Unconfirmed ? "denied.deleteUnconfirmed" : "denied.deleteRejected";
                ClientDIContext.ClientLocalNotificationSource.Notify(NotificationMessagePack.CreateOperationDenied(message, new[] { label }));
            }
        }

        private static string Describe(object key)
        {
            return key is Component view ? $"{view.name} ({view.transform.position})" : key.ToString();
        }

        internal void AppendTo(List<TooltipLine> lines)
        {
            foreach (var item in _results.Values)
            {
                var key = item.result.Kind == DeleteCompletionKind.Pending ? LocalizationKeys.Ui.Tooltip.DeleteResultPending
                    : item.result.Kind == DeleteCompletionKind.Unconfirmed ? LocalizationKeys.Ui.Tooltip.DeleteResultUnconfirmed
                    : LocalizationKeys.Ui.Tooltip.DeleteResultRejected;
                lines.Add(new TooltipLine(key, new[] { item.label }));
                if (item.result.DenyReason.HasValue) lines.Add(new TooltipLine(item.result.DenyReason.Value));
            }
        }
    }
}
