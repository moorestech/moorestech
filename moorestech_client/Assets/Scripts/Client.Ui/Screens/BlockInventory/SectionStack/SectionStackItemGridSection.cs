using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SectionStack;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の項目グリッドの Presenter（Web の showItemGrid）。同じパネルのあいだ枠の変化を BlockItemGrid へ流す
    /// Presenter of the standard body's item grid (Web showItemGrid); streams slot changes into BlockItemGrid while the panel stays the same
    /// </summary>
    public sealed class SectionStackItemGridSection : BlockInventorySectionBase
    {
        private readonly SectionStackItemGrid _config;
        private readonly IBlockInventoryPanelSource _panels;
        private readonly BlockItemGrid _grid;
        private readonly IObservable<IReadOnlyList<int>> _machineSlots;

        public override VisualElement Root { get; }

        public SectionStackItemGridSection(SectionStackItemGrid config, BlockItemGridFactory grids, IBlockInventoryPanelSource panels, IObservable<IReadOnlyList<int>> machineSlots)
        {
            _config = config;
            _panels = panels;
            _machineSlots = machineSlots;
            Root = new VisualElement { name = "section-stack-item-grid" };
            // 出さない設定でも器は作り、IsApplicable が偽のまま
            // Build the holder even when hidden by settings; IsApplicable stays false
            _grid = grids.Create(config is ShownSectionStackItemGrid shown ? shown.GridId : "section-stack-no-item-grid");
            Root.Add(_grid);
        }

        public override void Attach(OpenBlockInventoryPanel panel)
        {
            AddSubscription(_panels.Current.CombineLatest(_machineSlots, (current, excluded) => (current, excluded)).Subscribe(value =>
            {
                // silent-ok: 別のパネルへ替わる途中の値は描かず偽へ倒す / BodyHost が同じフレームで Detach して新しい区画を作る
                if (value.current is not OpenBlockInventoryPanel open || !open.IsSamePanel(panel))
                {
                    SetApplicable(false);
                    return;
                }
                // 選択中は機械欄から在庫が外れ、同じ実枠をグリッドで操作できる
                // Recipe selection takes inventory out of the machine section, freeing the same slots for the grid
                var indices = BlockDetailMath.AllSlotIndices(open.ItemSlots.Count).Except(value.excluded).ToArray();
                var applicable = _config is ShownSectionStackItemGrid shown
                    && (indices.Length > 0 || (open.ItemSlots.Count == 0 && shown.RenderEmpty));
                if (applicable) _grid.Render(open.ItemSlots, indices);
                SetApplicable(applicable);
            }));
        }
    }
}
