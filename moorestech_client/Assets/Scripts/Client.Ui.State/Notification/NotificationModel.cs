using System.Collections.Generic;
using Client.Ui.State.Notification.Content;
using UniRx;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 表示中の通知の行を持ち、獲得だけを合流させる。時間は持たず、除去は View の退場の終わりが書き口から呼ぶ（Web の notificationStore。保険タイマーは D-2 で持たない）
    /// Holds the visible notification rows and merges only earns; it owns no time, and the View's exit end calls removal through the write side (Web notificationStore; no fallback timer, D-2)
    /// </summary>
    public sealed class NotificationModel
    {
        // 生存尺は出入りを含めて 7 秒。View の動きの長さはこの定数だけから決まる（Web NOTIFICATION_DISPLAY_MS）
        // The 7s lifetime includes enter and exit; the View's motion length comes only from this constant (Web NOTIFICATION_DISPLAY_MS)
        public const int DisplayMilliseconds = 7000;

        private readonly ReactiveProperty<NotificationListViewModel> _current = new(NotificationListViewModel.Empty);
        private long _nextId = 1;

        public IReadOnlyReactiveProperty<NotificationListViewModel> Current => _current;

        public void AddNotification(NotificationContent content)
        {
            var rows = _current.Value.Rows;

            // 合流するのは獲得だけ。合流先は同じアイテム・同じ messageId の表示中の行で、id と位置を保ち epoch を進める
            // Only earns merge; the target is a visible row with the same item and messageId, keeping its id and position and advancing the epoch
            if (content is EarnedNotificationContent earned && TryFindMergeTarget(earned, out var index, out var merged))
            {
                var mergedRow = new NotificationRow(rows[index].Id, rows[index].LifetimeEpoch + 1, merged);
                _current.Value = new NotificationListViewModel(new List<NotificationRow>(rows) { [index] = mergedRow });
                return;
            }

            // 新しい行は末尾（新着）に足す
            // A new row goes to the end (newest)
            var row = new NotificationRow(_nextId++, 0, content);
            _current.Value = new NotificationListViewModel(new List<NotificationRow>(rows) { row });

            #region Internal

            bool TryFindMergeTarget(EarnedNotificationContent newer, out int foundIndex, out EarnedNotificationContent mergedContent)
            {
                for (foundIndex = 0; foundIndex < rows.Count; foundIndex++)
                {
                    if (rows[foundIndex].Content is EarnedNotificationContent candidate && candidate.TryMergeWith(newer, out mergedContent)) return true;
                }
                foundIndex = -1;
                mergedContent = null;
                return false;
            }

            #endregion
        }

        public void RemoveNotification(long id)
        {
            var rows = _current.Value.Rows;
            var kept = new List<NotificationRow>(rows.Count);
            foreach (var row in rows)
            {
                if (row.Id != id) kept.Add(row);
            }

            // silent-ok: 同じ行の除去が 2 度来るのは、退場の終わりと差分描画の片付けが重なる正常な場合 / 消し漏れは NotificationModelTest と L1 の行数の検査が気づく
            if (kept.Count == rows.Count) return;
            _current.Value = new NotificationListViewModel(kept);
        }
    }
}
