using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 特殊族の View が実装する描画の口（自分の詳細の有無と、スロットの中身・特殊族の詳細での描画）。View は購読せず Presenter から呼ばれる
    // Rendering port the special views implement: whether their detail exists, and drawing from slot contents and special details; views never subscribe and are called by the presenter
    public interface ISpecialDetailRenderer
    {
        bool HasDetail(SpecialBlockDetailViewModel detail);
        void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail);
    }
}
