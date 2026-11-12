using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UniRx;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 同じパネルの枠と特殊族の詳細の流れ。本文・題の操作・区画の SpecialSectionPresenter が共有する
    // The same panel's slots joined with the special detail; shared by SpecialSectionPresenter for bodies, title actions and sections
    public static class SpecialDetailStream
    {
        public static IObservable<(IReadOnlyList<SlotItem> slots, SpecialBlockDetailViewModel detail)> Of(IBlockInventoryPanelSource panels, ISpecialBlockDetailModel model, OpenBlockInventoryPanel attached)
        {
            // 同じパネルの枠の変化と、パネルの組み直しの外で届く詳細（網の集約・ベルトの統計）の両方で描き直す
            // Redraw on both slot changes of the same panel and details arriving outside panel rebuilds (network totals, belt stats)
            return panels.Current
                .Where(current => IsSame(current, attached))
                .Select(current => ((OpenBlockInventoryPanel)current).ItemSlots)
                .CombineLatest(model.Current, (slots, detail) => (slots, detail));

            #region Internal

            bool IsSame(BlockInventoryPanelState current, OpenBlockInventoryPanel panel)
            {
                // silent-ok: 別のパネルへ替わる途中の値は描かない / alpha の BodyHost が同じフレームで Detach して新しい部分を作る（前例 alpha の SectionStackItemGridSection）
                return current is OpenBlockInventoryPanel open && open.IsSamePanel(panel);
            }

            #endregion
        }
    }
}
