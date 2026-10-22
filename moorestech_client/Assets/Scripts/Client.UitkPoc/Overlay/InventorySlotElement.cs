using Client.UitkPoc.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Overlay
{
    // インベントリの1マス。中身の表示とツールチップの中身だけを持つ
    // One inventory slot; only renders its content and builds its tooltip
    public class InventorySlotElement : VisualElement, ITooltipSource
    {
        public readonly int Index;
        public string ItemName { get; private set; }
        public int Count { get; private set; }

        private readonly UitkPocContext _context;
        private readonly VisualElement _icon = new();
        private readonly Label _count = new();

        public InventorySlotElement(UitkPocContext context, int index)
        {
            _context = context;
            Index = index;
            AddToClassList("ov-slot");
            _icon.AddToClassList("ov-slot__icon");
            _icon.pickingMode = PickingMode.Ignore;
            _count.AddToClassList("ov-slot__count");
            _count.pickingMode = PickingMode.Ignore;
            Add(_icon);
            Add(_count);
        }

        public void SetContent(string itemName, int count)
        {
            ItemName = count > 0 ? itemName : null;
            Count = count;
            _icon.style.backgroundImage = ItemName == null ? null : _context.LoadItemIcon(ItemName);
            _count.text = count.ToString();
            _count.EnableInClassList("ov-slot__count--empty", count <= 1);
        }

        public VisualElement CreateTooltipContent()
        {
            var content = new VisualElement();
            if (ItemName == null)
            {
                content.Add(new Label("空きスロット"));
                return content;
            }
            var title = new Label($"<sprite name=\"{ItemName}\"> <b>{ItemName}</b>");
            title.AddToClassList("ov-tooltip__title");
            content.Add(title);
            content.Add(new Label($"所持数: {Count}\n<color=#9aa3ad>右クリックでメニュー / ドラッグで移動</color>"));
            return content;
        }
    }
}
