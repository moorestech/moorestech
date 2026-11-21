using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Core.Master;
using UniRx;

namespace Client.Ui.Screens.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 積荷を選ぶ本文（発射台・拠点）の View の口。行・見出しの押下を出し、一覧を出すかは Presenter から受けて描く。購読の源は持たない（前例 MachineSectionView）
    /// Port of a cargo-selecting body view (launch pad, elevator base); emits row and headline presses and draws with the mode the presenter passes; holds no subscription sources (precedent MachineSectionView)
    /// </summary>
    public interface IOrbitalCargoBodyView
    {
        IObservable<ItemId> OnCargoRowPressed { get; }
        IObservable<Unit> OnChangeCargoRequested { get; }
        void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail, bool showsSelection);
    }
}
