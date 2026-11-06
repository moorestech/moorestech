using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 1 マスの中身（アイコンと個数）（Web の slotContent）
    /// One cell content: icon and count (the Web slotContent)
    /// </summary>
    public sealed class SlotContent : VisualElement
    {
        private const string CountedStateClass = "slot-content--counted";

        private readonly GameIcon _icon;
        private readonly OutlinedCountLabel _count;

        public SlotContent()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("slot-content");
            _icon = new GameIcon();
            _icon.AddToClassList("slot-content__icon");
            Add(_icon);

            // 個数は数字だけで訳さない。白い縁の作りは OutlinedCountLabel が持つ
            // The count is digits only and never translated; OutlinedCountLabel owns the white outline
            _count = new OutlinedCountLabel(this, "slot-content__count");
            _count.AddLayerClass(UiTypographyClass.TextMedium);
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
            _count.SetText(UiText.FromVerbatim(SlotAmountFormat.Format(count)));
            EnableInClassList(CountedStateClass, true);
        }

        public void ClearCount()
        {
            EnableInClassList(CountedStateClass, false);
        }
    }
}
