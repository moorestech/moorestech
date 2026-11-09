using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// 用途ラベル付きのブロックの枠の並び（Web BlockItemGridGroup の写し。Stack gap 4）
    /// A block slot grid with a purpose label (port of Web BlockItemGridGroup; Stack gap 4)
    /// </summary>
    public sealed class BlockItemGridGroup : VisualElement
    {
        public BlockItemGridGroup(LocalizationKey label, BlockItemGrid grid)
        {
            var stack = new UiStack(UiGap.Px4);
            var labelElement = new LocalizedLabel();
            labelElement.AddToClassList(UiTypographyClass.TextSm);
            labelElement.AddToClassList(UiTypographyClass.TextMuted);
            labelElement.SetKey(label);
            stack.AddItem(labelElement);
            stack.AddItem(grid);
            Add(stack);
        }
    }
}
