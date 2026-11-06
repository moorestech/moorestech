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
        private readonly Label _countStroke;
        private readonly Label _count;

        public SlotContent()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("slot-content");
            _icon = new GameIcon();
            _icon.AddToClassList("slot-content__icon");
            Add(_icon);

            // 個数は数字だけで訳さないので素の Label にする。白い縁の層を先に足し、字を上に描く
            // The count is digits only and never translated, so plain Labels; the white stroke layer goes first so the glyph draws on top
            _countStroke = CreateCountLayer("slot-content__count-stroke");
            _count = CreateCountLayer("slot-content__count");

            #region Internal

            Label CreateCountLayer(string layerClass)
            {
                var layer = new Label { pickingMode = PickingMode.Ignore };
                layer.AddToClassList("slot-content__count-layer");
                layer.AddToClassList(layerClass);
                layer.AddToClassList(UiTypographyClass.TextMedium);
                Add(layer);
                return layer;
            }

            #endregion
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
            _countStroke.text = _count.text;
            EnableInClassList(CountedStateClass, true);
        }

        public void ClearCount()
        {
            _count.text = string.Empty;
            _countStroke.text = string.Empty;
            EnableInClassList(CountedStateClass, false);
        }
    }
}
