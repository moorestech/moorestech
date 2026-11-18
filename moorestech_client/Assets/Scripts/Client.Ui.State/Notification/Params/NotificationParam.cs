using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Notification.Params
{
    /// <summary>
    /// 通知の文に補間する引数 1 つ。値が文字のままか、内容名のキーかを型で分ける
    /// One interpolation argument of a notification text; the type tells a plain-text value from a content-name key
    /// </summary>
    public abstract class NotificationParam
    {
        public string Name { get; }

        protected NotificationParam(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// 受信した文字をそのまま補間する引数（件数・Guid として読めなかった値など）
    /// An argument interpolated as the received text (counts, values that did not parse as a Guid, etc.)
    /// </summary>
    public sealed class TextNotificationParam : NotificationParam
    {
        public string Value { get; }

        public TextNotificationParam(string name, string value) : base(name)
        {
            Value = value;
        }
    }

    /// <summary>
    /// 内容名（研究名・チャレンジ題）のキーを持つ引数。表示の時にいまの言語で引く
    /// An argument holding a content-name key (research name, challenge title), looked up in the current language at display time
    /// </summary>
    public sealed class ContentNameNotificationParam : NotificationParam
    {
        public ContentLocalizationKey Key { get; }

        public ContentNameNotificationParam(string name, ContentLocalizationKey key) : base(name)
        {
            Key = key;
        }
    }
}
