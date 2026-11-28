using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UniRx;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    /// <summary>
    /// 同じパネルの枠と特殊族の詳細の流れ。特殊族の Presenter が Attach で購読する（SpecialSectionPresenter と OrbitalCargoBodyPresenter で共有）
    /// Stream of the same panel's slots joined with the special detail; special presenters subscribe to it in Attach (shared by SpecialSectionPresenter and OrbitalCargoBodyPresenter)
    /// </summary>
    public static class SpecialPanelDetailStream
    {
        public static IObservable<(IReadOnlyList<SlotItem> slots, SpecialBlockDetailViewModel detail)> Of(IBlockInventoryPanelSource panels, ISpecialBlockDetailModel model, OpenBlockInventoryPanel panel)
        {
            // 同じパネルの枠の変化と、パネルの組み直しの外で届く詳細（網の集約・ベルトの統計）の両方で流す
            // Emit on both slot changes of the same panel and details arriving outside panel rebuilds (network totals, belt stats)
            return panels.Current
                .Where(IsSamePanel)
                .Select(current => ((OpenBlockInventoryPanel)current).ItemSlots)
                .CombineLatest(model.Current, (slots, detail) => (slots, detail.IsFor(panel.BlockInstanceId) ? detail : SpecialBlockDetailViewModel.Empty));

            #region Internal

            bool IsSamePanel(BlockInventoryPanelState current)
            {
                // silent-ok: 別のパネルへ替わる途中の値は描かない / alpha の BodyHost が同じフレームで Detach して新しい部分を作る（前例 alpha の SectionStackItemGridSection）
                return current is OpenBlockInventoryPanel open && open.IsSamePanel(panel);
            }

            #endregion
        }
    }
}
