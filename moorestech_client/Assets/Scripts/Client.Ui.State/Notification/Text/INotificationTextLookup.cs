using Mooresmaster.Localization.Generated;
using Core.Master;

namespace Client.Ui.State.Notification.Text
{
    /// <summary>
    /// 通知の文で使う内容名（研究名・チャレンジ題）とアイテム名を、いまの言語で引く口
    /// Looks up content names (research names, challenge titles) and item names in the current language for notification text
    /// </summary>
    public interface INotificationTextLookup
    {
        string ContentText(ContentLocalizationKey key);
        string ItemDisplayName(ItemId itemId);
    }
}
