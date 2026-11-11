using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.Screens.BlockInventory.Special.FilterSplitter;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Ui.Screens.BlockInventory.Special.Space;
using Client.Ui.State.BlockInventory.Logic.Panel;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // S4-alpha の拡張口 (1)・(2) へ差し込む特殊族の本文 5 つと題の操作 1 つ
    // The five special bodies and one title action plugged into S4-alpha's extension ports (1) and (2)
    public sealed class FilterSplitterBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public FilterSplitterBodyFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.FilterSplitter;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => _presenters.Of(new FilterSplitterBodyView(_kit));
    }

    public sealed class ElectricToGearBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public ElectricToGearBodyFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.ElectricToGear;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => _presenters.Of(new ElectricToGearBodyView(_kit));
    }

    public sealed class ElectricPoleBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public ElectricPoleBodyFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.ElectricPole;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => _presenters.Of(new ElectricPoleBodyView());
    }

    public sealed class SpaceElevatorBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public SpaceElevatorBodyFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.SpaceElevator;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => _presenters.Of(new SpaceElevatorBodyView(_kit));
    }

    public sealed class RocketLaunchPadBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public RocketLaunchPadBodyFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.RocketLaunchPad;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => _presenters.Of(new RocketLaunchPadBodyView(_kit));
    }

    public sealed class RocketLaunchPadTitleActionFactory : IBlockInventoryTitleActionFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public RocketLaunchPadTitleActionFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryTitleActionKind Kind => BlockInventoryTitleActionKind.RocketLaunchPadCommand;
        public IBlockInventoryPart Create() => _presenters.Of(new RocketLaunchPadTitleActionView(_kit));
    }
}
