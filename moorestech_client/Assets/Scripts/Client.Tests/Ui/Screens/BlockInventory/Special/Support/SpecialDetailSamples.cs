using System;
using Client.Tests.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.BlockInventory.Special.Details;
using Core.Master;
using Game.Block.Interface.State;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    /// <summary>
    /// Web の fixture と同じ値の詳細の見本。VM は Only* で 1 種類だけを持たせる
    /// Detail samples with the same values as the Web fixtures; Only* builds a VM carrying just one kind
    /// </summary>
    public static class SpecialDetailSamples
    {
        public static ElectricNetworkDetail PoleNetwork => new(240, 180, 3, 1);

        public static ElectricToGearDetail ElectricToGear => new(new[] { new ElectricToGearOutputMode(10, 10, 10), new ElectricToGearOutputMode(20, 20, 10) }, 1, 0.75, 10);

        public static FilterSplitterDetail FilterSplitter => new(new[]
        {
            new FilterSplitterDirectionDetail(FilterSplitterOutputSide.Left, FilterSplitterMode.Whitelist, new[] { new ItemId(4), ItemMaster.EmptyItemId }),
            new FilterSplitterDirectionDetail(FilterSplitterOutputSide.Front, FilterSplitterMode.Default, new[] { ItemMaster.EmptyItemId, ItemMaster.EmptyItemId }),
            new FilterSplitterDirectionDetail(FilterSplitterOutputSide.Right, FilterSplitterMode.Blacklist, new[] { new ItemId(7), new ItemId(8) }),
        });

        public static BeltThroughputDetail Belt => new(new[] { BeltThroughputWindow.Measured(90, 4, 4.5), BeltThroughputWindow.Measured(180, 9, 9), BeltThroughputWindow.Measuring(360, 18) }, new[] { 1, 0, 1 }, 1);

        // 発射台の見本: 資材 1 枠（0）・積荷 1 枠（1=ICチップLv1）・出力は 2 から。積荷は選んだ状態
        // Launch pad sample: one material slot (0), one payload slot (1 = IC chip Lv1), outputs from 2; cargo selected
        public static RocketLaunchPadDetail Rocket(RocketLaunchPadPhase phase) => RocketWith(phase, OrbitalCargoSamples.Pad(OrbitalCargoSamples.ChipLv1, null, OrbitalCargoSamples.ChipLv1, OrbitalCargoSamples.ChipLv2));
        public static RocketLaunchPadDetail RocketWith(RocketLaunchPadPhase phase, OrbitalCargoDetail cargo) => new(phase, RocketLaunchPadWaitReason.None, 0f, new[] { new RequiredItemSlot(0, new ItemId(1), 20) }, new[] { new RequiredItemSlot(1, OrbitalCargoSamples.ChipLv1, 20) }, Array.Empty<InletSlotBinding>(), 2, cargo);
        public static SpaceElevatorDetail SpaceElevator(SpaceElevatorBaseStatus status, bool connected, int pool, float progress) => SpaceElevatorWith(status, connected, pool, progress, new OrbitalCargoDetail(OrbitalCargoSamples.ChipLv1, null, new[] { OrbitalCargoSamples.ChipLv1Choice, OrbitalCargoSamples.ChipLv2Choice }, new[] { OrbitalCargoSamples.ChipLv1, OrbitalCargoSamples.ChipLv2 }, 240));
        public static SpaceElevatorDetail SpaceElevatorWith(SpaceElevatorBaseStatus status, bool connected, int pool, float progress, OrbitalCargoDetail cargo) => new(new[] { 0 }, new[] { 1, 2 }, Array.Empty<InletSlotBinding>(), connected, pool, 1000, progress, status, cargo);

        public static GearDetail GearMachineGear => GearDetail.Consumer(12.5, 3, 20);
        public static GearNetworkDetail GearMachineNetwork => new(100, 60, GearNetworkStopReasonKind.None);
        public static SpecialBlockDetailViewModel GearMachine => new(null, null, GearMachineGear, null, null, GearMachineNetwork, null, null, null);
        public static SpecialBlockDetailViewModel GearGenerator => new(null, null, GearDetail.Generator(20, 5), null, null, new GearNetworkDetail(100, 60, GearNetworkStopReasonKind.None), null, null, null);
        public static GearToElectricDetail GearToElectricGenerating => new(GearToElectricDisplayState.Generating, 120, 200);

        public static SpecialBlockDetailViewModel Only(ElectricToGearDetail detail) => new(detail, null, null, null, null, null, null, null, null);
        public static SpecialBlockDetailViewModel Only(GearToElectricDetail detail) => new(null, detail, null, null, null, null, null, null, null);
        public static SpecialBlockDetailViewModel Only(GearDetail detail) => new(null, null, detail, null, null, null, null, null, null);
        public static SpecialBlockDetailViewModel Only(BeltThroughputDetail detail) => new(null, null, null, detail, null, null, null, null, null);
        public static SpecialBlockDetailViewModel Only(ElectricNetworkDetail detail) => new(null, null, null, null, detail, null, null, null, null);
        public static SpecialBlockDetailViewModel Only(GearNetworkDetail detail) => new(null, null, null, null, null, detail, null, null, null);
        public static SpecialBlockDetailViewModel Only(FilterSplitterDetail detail) => new(null, null, null, null, null, null, detail, null, null);
        public static SpecialBlockDetailViewModel Only(RocketLaunchPadDetail detail) => new(null, null, null, null, null, null, null, detail, null);
        public static SpecialBlockDetailViewModel ElevatorWithNetwork(SpaceElevatorDetail detail) => new(null, null, null, null, new ElectricNetworkDetail(4000, 2000, 1, 1), null, null, null, detail);
    }
}
