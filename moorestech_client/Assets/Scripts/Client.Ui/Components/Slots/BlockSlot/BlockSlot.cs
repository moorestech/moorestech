using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// ブロック 1 マス。絵と名前のツールチップ（Web の BlockSlot）
    /// One block cell: picture and name tooltip (the Web BlockSlot)
    /// </summary>
    public sealed class BlockSlot : VisualElement
    {
        private readonly SlotFrame _frame;
        private readonly SlotContent _content;

        public BlockSlot(string frameTestid)
        {
            // testid は枠に付ける（照準は枠）
            // The testid goes on the frame (the aim target)
            pickingMode = PickingMode.Ignore;
            _frame = new SlotFrame(frameTestid);
            _content = new SlotContent();
            _frame.Add(_content);
            Add(_frame);
        }

        public void SetBlock(UiIcon icon, UiText name)
        {
            _content.SetIcon(icon, GameIconFallback.IdText);
            HoverTooltipMark.Mark(_frame, name);
        }
    }
}
