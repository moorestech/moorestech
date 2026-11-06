using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// ブロック 1 マス。暗い面の枠にブロックの絵と名前のツールチップ（Web の BlockSlot）
    /// One block cell: the block picture on a dark frame with a name tooltip (the Web BlockSlot)
    /// </summary>
    public sealed class BlockSlot : VisualElement
    {
        private readonly SlotFrame _frame;
        private readonly SlotContent _content;

        public BlockSlot(string frameTestid)
        {
            // 枠の testid は Web と同じく枠に付ける（DSL・e2e の照準は枠）
            // The testid goes on the frame as on the Web (DSL and e2e aim at the frame)
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
