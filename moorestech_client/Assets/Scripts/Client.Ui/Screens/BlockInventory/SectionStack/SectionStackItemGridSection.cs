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
    public sealed class SectionStackItemGridSection : IBlockInventorySection
    {
        private readonly SectionStackItemGrid _config;
        private readonly IBlockInventoryPanelSource _panels;
        private readonly BlockItemGrid _grid;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root { get; private set; }
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;   // 型を狭める公開（前例 SettingsModel.Current）

        public SectionStackItemGridSection(SectionStackItemGrid config, BlockItemGridFactory grids, IBlockInventoryPanelSource panels)
        {
            _config = config;
            _panels = panels;
            Root = new VisualElement { name = "section-stack-item-grid" };
            // 出さない設定でも器は作り、IsApplicable が偽のまま
            // Build the holder even when hidden by settings; IsApplicable stays false
            _grid = grids.Create(config is ShownSectionStackItemGrid shown ? shown.GridId : "section-stack-no-item-grid");
            Root.Add(_grid);
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            _panels.Current.Subscribe(current =>
            {
                // silent-ok: 別のパネルへ替わる途中の値は描かない / BodyHost が同じフレームで Detach して新しい区画を作る
                if (!(current is OpenBlockInventoryPanel open) || !open.IsSamePanel(panel)) return;
                _isApplicable.Value = _config is ShownSectionStackItemGrid shown && (shown.RenderEmpty || 0 < open.ItemSlots.Count);
                if (_isApplicable.Value) _grid.Render(open.ItemSlots, BlockDetailMath.AllSlotIndices(open.ItemSlots.Count));
            }).AddTo(_subscriptions);
        }

        public void Detach()
        {
            _subscriptions.Clear();
        }
    }
}
