using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 1 マスの中身（アイコンと右下の個数）。Web の slotContent.module.css の .icon・.count
    /// One cell's content (icon and bottom-right count); the Web slotContent.module.css .icon and .count
    /// </summary>
    public sealed class SlotContent : VisualElement
    {
        private const string CountedStateClass = "slot-content--counted";

        private readonly GameIcon _icon;
        private readonly Label _count;

        public SlotContent()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("slot-content");
            _icon = new GameIcon();
            _icon.AddToClassList("slot-content__icon");
            Add(_icon);

            // 個数は数字だけで訳さないので素の Label にする
            // The count is digits only and never translated, so a plain Label
            _count = new Label { pickingMode = PickingMode.Ignore };
            _count.AddToClassList("slot-content__count");
            _count.AddToClassList(UiTypographyClass.TextMedium);
            Add(_count);
        }

        public void SetIcon(UiIcon icon, GameIconFallback fallback)
        {
            _icon.SetIcon(icon, fallback);
        }

        public void ClearIcon()
        {
            _icon.ClearIcon();
        }

        public void SetCount(int count)
        {
            _count.text = SlotAmountFormat.Format(count);
            EnableInClassList(CountedStateClass, true);
        }

        public void ClearCount()
        {
            _count.text = string.Empty;
            EnableInClassList(CountedStateClass, false);
        }
    }
}
