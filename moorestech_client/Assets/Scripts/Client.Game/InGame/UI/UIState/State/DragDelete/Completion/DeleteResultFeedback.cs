using Client.Localization;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using Server.Event.Notification;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Completion
{
    // カーソルが離れても要求対象と結果を保持する
    // Retain request targets and results independently of the cursor
    // 一括撤去は対象が数百件になるので、行は対象ごとでなく「結果の種類＋理由」ごとの件数にまとめる（対象ごとの内訳は DeleteCommitBatch のログに残る）
    // A bulk removal has hundreds of targets, so lines are counts per result kind and reason rather than per target (per-target detail stays in the DeleteCommitBatch log)
    internal sealed class DeleteResultFeedback
    {
        private readonly Dictionary<object, DeleteCompletion> _results = new();
        // 最初に現れた順に並ぶ、種類＋理由ごとの件数。結果の受信時に増減し、表示側は毎フレーム数え直さない
        // Counts per kind and reason in first-seen order; adjusted on each received result so the display never recounts per frame
        private readonly List<ResultGroup> _groups = new();

        internal void Receive(DeleteCompletion result)
        {
            if (_results.Remove(result.TargetKey, out var previous)) Decrement(previous);
            if (result.Kind == DeleteCompletionKind.Succeeded) return;

            _results.Add(result.TargetKey, result);
            if (!Increment(result) || result.Kind == DeleteCompletionKind.Pending) return;

            // モード離脱後にも既存通知で失敗と未確認を届ける。同じ種類＋理由は1通だけ
            // Deliver failures and unconfirmed outcomes through notifications even after leaving the mode, once per kind and reason
            var message = result.Kind == DeleteCompletionKind.Unconfirmed ? "denied.deleteUnconfirmed" : "denied.deleteRejected";
            ClientDIContext.ClientLocalNotificationSource.Notify(NotificationMessagePack.CreateOperationDenied(message, new Dictionary<string, string>()));
        }

        // 次の確定・離脱で拒否と未確認の行を消し、処理中だけ残す
        // On next commit or exit drop rejected/unconfirmed lines, keep pending
        internal void ClearSettled()
        {
            var settledKeys = new List<object>();
            foreach (var (key, result) in _results)
            {
                if (result.Kind != DeleteCompletionKind.Pending) settledKeys.Add(key);
            }
            foreach (var key in settledKeys) _results.Remove(key);
            for (var i = _groups.Count - 1; 0 <= i; i--)
            {
                if (_groups[i].Kind != DeleteCompletionKind.Pending) _groups.RemoveAt(i);
            }
        }

        internal void AppendTo(List<TooltipLine> lines)
        {
            foreach (var group in _groups)
            {
                var key = group.Kind == DeleteCompletionKind.Pending ? LocalizationKeys.Ui.Tooltip.DeleteResultPending
                    : group.Kind == DeleteCompletionKind.Unconfirmed ? LocalizationKeys.Ui.Tooltip.DeleteResultUnconfirmed
                    : LocalizationKeys.Ui.Tooltip.DeleteResultRejected;
                lines.Add(new TooltipLine(key, new[] { new LocalizationTextParam("count", group.Count.ToString()) }));
                if (group.Reason.HasValue) lines.Add(new TooltipLine(group.Reason.Value));
            }
        }

        // 件数を足す。新しい種類＋理由の組が増えたら true
        // Add one to the count; true when a new kind-and-reason group appeared
        private bool Increment(DeleteCompletion result)
        {
            var group = FindGroup(result);
            if (group != null)
            {
                group.Count++;
                return false;
            }
            _groups.Add(new ResultGroup(result.Kind, GroupReason(result)));
            return true;
        }

        private void Decrement(DeleteCompletion result)
        {
            var group = FindGroup(result);
            group.Count--;
            if (group.Count == 0) _groups.Remove(group);
        }

        // 組は種類×理由で高々十数個なので線形に探す
        // Groups number at most a dozen (kinds times reasons), so a linear search suffices
        private ResultGroup FindGroup(DeleteCompletion result)
        {
            var reasonKey = GroupReason(result)?.Key;
            foreach (var group in _groups)
            {
                if (group.Kind == result.Kind && group.Reason?.Key == reasonKey) return group;
            }
            return null;
        }

        // 処理中は理由を持たない1組にまとめる
        // Pending results collapse into one reasonless group
        private static LocalizationKey? GroupReason(DeleteCompletion result)
        {
            return result.Kind == DeleteCompletionKind.Pending ? null : result.DenyReason;
        }

        private sealed class ResultGroup
        {
            internal readonly DeleteCompletionKind Kind;
            internal readonly LocalizationKey? Reason;
            internal int Count = 1;

            internal ResultGroup(DeleteCompletionKind kind, LocalizationKey? reason)
            {
                Kind = kind;
                Reason = reason;
            }
        }
    }
}
