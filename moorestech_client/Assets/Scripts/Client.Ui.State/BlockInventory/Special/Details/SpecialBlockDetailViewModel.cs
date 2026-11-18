using Client.Ui.State.BlockInventory.Logic.Detail;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 開いたブロックの特殊族の詳細。どれも届くまで不在（Web の DTO のキー省略と同じ）。読み手は TryGet* で在否を分ける
    /// Special-family details of the open block; each is absent until it arrives (like the omitted Web DTO keys); readers split presence with TryGet*
    /// </summary>
    public sealed class SpecialBlockDetailViewModel
    {
        public static readonly SpecialBlockDetailViewModel Empty = new(null, null, null, null, null, null, null, null, null);

        private readonly ElectricToGearDetail _electricToGear;
        private readonly GearToElectricDetail _gearToElectric;
        private readonly GearDetail _gear;
        private readonly BeltThroughputDetail _belt;
        private readonly ElectricNetworkDetail _electricNetwork;
        private readonly GearNetworkDetail _gearNetwork;
        private readonly FilterSplitterDetail _filterSplitter;
        private readonly RocketLaunchPadDetail _rocketLaunchPad;
        private readonly SpaceElevatorDetail _spaceElevator;

        public SpecialBlockDetailViewModel(ElectricToGearDetail electricToGear, GearToElectricDetail gearToElectric, GearDetail gear, BeltThroughputDetail belt, ElectricNetworkDetail electricNetwork, GearNetworkDetail gearNetwork, FilterSplitterDetail filterSplitter, RocketLaunchPadDetail rocketLaunchPad, SpaceElevatorDetail spaceElevator)
        {
            _electricToGear = electricToGear;
            _gearToElectric = gearToElectric;
            _gear = gear;
            _belt = belt;
            _electricNetwork = electricNetwork;
            _gearNetwork = gearNetwork;
            _filterSplitter = filterSplitter;
            _rocketLaunchPad = rocketLaunchPad;
            _spaceElevator = spaceElevator;
        }

        public bool TryGetElectricToGear(out ElectricToGearDetail detail) => Present(_electricToGear, out detail);
        public bool TryGetGearToElectric(out GearToElectricDetail detail) => Present(_gearToElectric, out detail);
        public bool TryGetGear(out GearDetail detail) => Present(_gear, out detail);
        public bool TryGetBelt(out BeltThroughputDetail detail) => Present(_belt, out detail);
        public bool TryGetElectricNetwork(out ElectricNetworkDetail detail) => Present(_electricNetwork, out detail);
        public bool TryGetGearNetwork(out GearNetworkDetail detail) => Present(_gearNetwork, out detail);
        public bool TryGetFilterSplitter(out FilterSplitterDetail detail) => Present(_filterSplitter, out detail);
        public bool TryGetRocketLaunchPad(out RocketLaunchPadDetail detail) => Present(_rocketLaunchPad, out detail);
        public bool TryGetSpaceElevator(out SpaceElevatorDetail detail) => Present(_spaceElevator, out detail);

        private static bool Present<T>(T value, out T detail) where T : class
        {
            detail = value;
            return value != null;
        }
    }
}
