using System;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 通知の 1 行。アイテムのアイコン（任意）と文を横に並べ、拒否は警告色にする。入力は奪わない
    /// One notification row: an optional item icon beside the text, with denied rows in the warning color; never takes input
    /// </summary>
    public sealed class NotificationRowView : VisualElement
    {
        private const string DeniedClassName = "notification-row--operation-denied";

        private readonly VisualElement _iconSlot;
        private readonly GameIcon _icon;
        private readonly LocalizedLabel _text;

        public NotificationRow Row { get; private set; }

        public NotificationRowView()
        {
            name = "notification-row";
            AddToClassList("notification-row");
            pickingMode = PickingMode.Ignore;

            // アイコンは 22px の枠に入れ、テクスチャが無ければ代わりの文字 #<id> を出す（Web の ItemIcon と同じ）
            // The icon sits in a 22px slot and shows the #<id> fallback without a texture (same as Web ItemIcon)
            _iconSlot = new VisualElement { pickingMode = PickingMode.Ignore };
            _iconSlot.AddToClassList("notification-row__icon");
            _icon = new GameIcon();
            _icon.AddToClassList("notification-row__icon-content");
            _iconSlot.Add(_icon);
            Add(_iconSlot);

            _text = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            _text.AddToClassList("notification-row__text");
            _text.AddToClassList(UiTypographyClass.TextMedium);
            Add(_text);
        }

        public void Show(NotificationRow row, UiText text, INotificationRowIcons icons)
        {
            Row = row;
            EnableInClassList(DeniedClassName, row.Content is OperationDeniedNotificationContent);

            // アイコンの型で枠の有無を決める（アイテムだけ枠を出す）
            // The icon type decides the slot (only items show it)
            switch (row.Content.Icon)
            {
                case ItemNotificationIcon itemIcon:
                    UiDisplay.SetShown(_iconSlot, true);
                    _icon.SetIcon(icons.ItemIcon(itemIcon.ItemId), GameIconFallback.IdText);
                    break;
                case NoNotificationIcon:
                    UiDisplay.SetShown(_iconSlot, false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(row), row.Content.Icon.GetType().Name, null);
            }
            _text.SetText(text);
        }

        public void SetText(UiText text)
        {
            _text.SetText(text);
        }
    }
}
