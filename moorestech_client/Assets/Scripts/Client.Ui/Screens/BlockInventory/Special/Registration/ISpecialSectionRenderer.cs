using System.Collections.Generic;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 特殊族の区画 View の描画の口。受ける詳細の型で selector と結ぶ。購読しない
    // Special section view render port; tied to its selector by the detail type; no subscribing
    public interface ISpecialSectionRenderer<in TDetail>
    {
        void Render(IReadOnlyList<SlotItem> itemSlots, TDetail detail);
    }
}
