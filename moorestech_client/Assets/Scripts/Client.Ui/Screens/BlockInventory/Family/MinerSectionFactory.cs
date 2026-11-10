using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Family.Models;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 採掘機の区画の工場
    /// Factory of the miner section (SectionStackSlot.Miner); shown when the model is present
    /// </summary>
    public sealed class MinerSectionFactory : ISectionStackSectionFactory
    {
        private readonly GameContentPresentation _content;
        private readonly IMinerDetailModel _miner;

        public SectionStackSlot Slot => SectionStackSlot.Miner;

        public MinerSectionFactory(GameContentPresentation content, IMinerDetailModel miner)
        {
            _content = content;
            _miner = miner;
        }

        public IBlockInventorySection Create(SectionStackSectionMount mount)
        {
            return new DetailSectionPresenter<MinerDetail>(_miner.Current, new MinerSectionView(_content));
        }
    }
}
