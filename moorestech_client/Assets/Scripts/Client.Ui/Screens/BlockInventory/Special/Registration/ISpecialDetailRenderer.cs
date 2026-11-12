using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 特殊族の View が実装する描画の口（スロットの中身・特殊族の詳細で描くだけ）。出すかは Presenter が SpecialDetailVisibility で先に決め、出すときだけ呼ぶ。View は購読しない
    // Rendering port the special views implement (draws from slot contents and special details only); the presenter decides showing first with SpecialDetailVisibility and calls this only when shown; views never subscribe
    public interface ISpecialDetailRenderer
    {
        void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail);
    }
}
