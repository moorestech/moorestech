using Client.Ui.Content;
using Client.Ui.Foundation.Icons;
using Core.Master;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 本番の口。ID からアイコンへの解決は Content 層（GameContentPresentation）の 1 か所に任せる
    /// Production lookup; resolving an id into an icon is left to the single place in the Content layer (GameContentPresentation)
    /// </summary>
    public sealed class GameContentNotificationRowIcons : INotificationRowIcons
    {
        private readonly GameContentPresentation _content;

        public GameContentNotificationRowIcons(GameContentPresentation content)
        {
            _content = content;
        }

        public UiIcon ItemIcon(ItemId itemId)
        {
            return _content.Item(itemId);
        }
    }
}
