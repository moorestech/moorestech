using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Family.Models;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// ポンプの区画の工場
    /// Factory of the pump section (SectionStackSlot.Pump); shown when the model is present
    /// </summary>
    public sealed class PumpSectionFactory : ISectionStackSectionFactory
    {
        private readonly GameContentPresentation _content;
        private readonly IPumpDetailModel _pump;

        public SectionStackSlot Slot => SectionStackSlot.Pump;

        public PumpSectionFactory(GameContentPresentation content, IPumpDetailModel pump)
        {
            _content = content;
            _pump = pump;
        }

        public IBlockInventorySection Create(SectionStackSectionMount mount)
        {
            return new DetailSectionPresenter<PumpDetail>(_pump.Current, new PumpSectionView(_content));
        }
    }
}
