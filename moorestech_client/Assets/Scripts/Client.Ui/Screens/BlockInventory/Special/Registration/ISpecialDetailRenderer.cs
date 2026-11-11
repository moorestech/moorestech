using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 特殊族の View が実装する描画の口（スロットの中身・特殊族の詳細で描き、描いたら true。偽なら呼び手が隠す）。View は購読せず Presenter から呼ばれる
    // Rendering port the special views implement: draws from slot contents and special details and returns true when drawn (the caller hides it on false); views never subscribe and are called by the presenter
    public interface ISpecialDetailRenderer
    {
        bool Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail);
    }
}
