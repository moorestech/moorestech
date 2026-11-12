using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 特殊族 View の描画の口。購読しない
    // Special view render port; presenter decides showing; no subscribing
    public interface ISpecialDetailRenderer
    {
        void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail);
    }
}
