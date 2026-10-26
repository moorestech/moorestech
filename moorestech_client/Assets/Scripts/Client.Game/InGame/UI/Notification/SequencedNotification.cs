using Server.Event.Notification;

namespace Client.Game.InGame.UI.Notification
{
    /// <summary>
    /// 連番を振った通知。描く側は同じ連番の再表示を弾く
    /// A notification with its sequence number; presenters drop a repeat of the same number
    /// </summary>
    public sealed class SequencedNotification
    {
        public readonly long Seq;
        public readonly NotificationMessagePack Message;

        public SequencedNotification(long seq, NotificationMessagePack message)
        {
            Seq = seq;
            Message = message;
        }
    }
}
