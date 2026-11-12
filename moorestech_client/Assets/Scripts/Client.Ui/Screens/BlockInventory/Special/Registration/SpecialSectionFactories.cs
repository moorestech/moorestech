using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.Screens.BlockInventory.Special.Belt;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Ui.State.BlockInventory.Special;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // S4-alpha の拡張口 (3) の特殊族の区画 5 つ（Slot は alpha の SectionStackSlot。並びは alpha の列挙の順）
    // The five special sections for S4-alpha's extension port (3) (slots from alpha's SectionStackSlot; order follows alpha's enum)
    public sealed class GearToElectricSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialPresenters _presenters;
        public GearToElectricSectionFactory(SpecialPresenters presenters) { _presenters = presenters; }
        public SectionStackSlot Slot => SectionStackSlot.GearToElectric;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => _presenters.Of(new GearToElectricSectionView(), SpecialDetailSection.GearToElectric);
    }

    public sealed class BeltThroughputSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialPresenters _presenters;
        public BeltThroughputSectionFactory(SpecialPresenters presenters) { _presenters = presenters; }
        public SectionStackSlot Slot => SectionStackSlot.BeltThroughput;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => _presenters.Belt(new BeltThroughputSectionView());
    }

    public sealed class GearSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialPresenters _presenters;
        public GearSectionFactory(SpecialPresenters presenters) { _presenters = presenters; }
        public SectionStackSlot Slot => SectionStackSlot.Gear;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => _presenters.Of(new GearSectionView(), SpecialDetailSection.Gear);
    }

    public sealed class ElectricNetworkSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialPresenters _presenters;
        public ElectricNetworkSectionFactory(SpecialPresenters presenters) { _presenters = presenters; }
        public SectionStackSlot Slot => SectionStackSlot.ElectricNetwork;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => _presenters.Of(new ElectricNetworkSectionView(), SpecialDetailSection.ElectricNetwork);
    }

    public sealed class GearNetworkSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialPresenters _presenters;
        public GearNetworkSectionFactory(SpecialPresenters presenters) { _presenters = presenters; }
        public SectionStackSlot Slot => SectionStackSlot.GearNetwork;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => _presenters.Of(new GearNetworkSectionView(), SpecialDetailSection.GearNetwork);
    }
}
