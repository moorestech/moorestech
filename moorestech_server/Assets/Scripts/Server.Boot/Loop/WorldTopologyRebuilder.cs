using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Blocks.Fluid;
using Game.EnergySystem;
using Game.Gear.Common;
using Game.World.Interface.DataStore;

namespace Server.Boot.Loop
{
    // 派生網の再構築順を1箇所に置く。tick末尾(MasterTickUpdater)とロード完了(WorldSaveDataRestorer)が同じ順で作り直す
    // Keeps the derived-network rebuild order in one place; the tick end (MasterTickUpdater) and load completion (WorldSaveDataRestorer) rebuild in the same order
    public class WorldTopologyRebuilder : IWorldTopologyRebuilder
    {
        private readonly ElectricWireNetworkDatastore _electricWireNetworkDatastore;
        private readonly GearNetworkDatastore _gearNetworkDatastore;
        private readonly FluidNetworkDatastore _fluidNetworkDatastore;
        private readonly IBeltTransportMutation _beltTransportMutation;

        public WorldTopologyRebuilder(
            ElectricWireNetworkDatastore electricWireNetworkDatastore,
            GearNetworkDatastore gearNetworkDatastore,
            FluidNetworkDatastore fluidNetworkDatastore,
            IBeltTransportMutation beltTransportMutation)
        {
            _electricWireNetworkDatastore = electricWireNetworkDatastore;
            _gearNetworkDatastore = gearNetworkDatastore;
            _fluidNetworkDatastore = fluidNetworkDatastore;
            _beltTransportMutation = beltTransportMutation;
        }

        public void RebuildTopologiesIfDirty()
        {
            _electricWireNetworkDatastore.RebuildIfDirty();
            _gearNetworkDatastore.RebuildIfDirty();
            _fluidNetworkDatastore.RebuildIfDirty();
            _beltTransportMutation.RebuildIfDirty();
        }
    }
}
