using System;
using System.Collections.Generic;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 表示中の行の並び（古い順。新着が末尾）
    /// The visible rows, oldest first (newest last)
    /// </summary>
    public sealed class NotificationListViewModel
    {
        public static readonly NotificationListViewModel Empty = new(Array.Empty<NotificationRow>());

        public IReadOnlyList<NotificationRow> Rows { get; }

        public NotificationListViewModel(IReadOnlyList<NotificationRow> rows)
        {
            Rows = rows;
        }
    }
}
