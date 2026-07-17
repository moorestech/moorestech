using Game.Block.Blocks.Fluid;
using Game.EnergySystem;
using Game.Gear.Common;
using Game.Train.Unit;

namespace Server.Boot
{
    // 仕様2.1のtick順序を1箇所で明示する（①電力網再構築→②歯車網再構築→③流体網再構築→④電力tick→⑤歯車tick→⑥流体tick→⑦鉄道tick）
    // Declares the spec 2.1 tick order in one place: rebuild electric, gear and fluid topologies, then settle electric, gear, fluid and train
    public class MasterTickUpdater
    {
        private readonly ElectricWireNetworkDatastore _electricWireNetworkDatastore;
        private readonly GearNetworkDatastore _gearNetworkDatastore;
        private readonly FluidNetworkDatastore _fluidNetworkDatastore;
        private readonly ElectricTickUpdater _electricTickUpdater;
        private readonly GearTickUpdater _gearTickUpdater;
        private readonly FluidTickUpdater _fluidTickUpdater;
        private readonly TrainUpdateService _trainUpdateService;

        public MasterTickUpdater(
            ElectricWireNetworkDatastore electricWireNetworkDatastore,
            GearNetworkDatastore gearNetworkDatastore,
            FluidNetworkDatastore fluidNetworkDatastore,
            ElectricTickUpdater electricTickUpdater,
            GearTickUpdater gearTickUpdater,
            FluidTickUpdater fluidTickUpdater,
            TrainUpdateService trainUpdateService)
        {
            _electricWireNetworkDatastore = electricWireNetworkDatastore;
            _gearNetworkDatastore = gearNetworkDatastore;
            _fluidNetworkDatastore = fluidNetworkDatastore;
            _electricTickUpdater = electricTickUpdater;
            _gearTickUpdater = gearTickUpdater;
            _fluidTickUpdater = fluidTickUpdater;
            _trainUpdateService = trainUpdateService;
        }

        public void Update()
        {
            // トポロジ反映は全網とも需給計算より先（tick途中でセグメント所属を変えないため）
            // Apply every topology before any settlement so segment membership never changes mid tick
            _electricWireNetworkDatastore.RebuildIfDirty();
            _gearNetworkDatastore.RebuildIfDirty();
            _fluidNetworkDatastore.RebuildIfDirty();
            _electricTickUpdater.Update();
            _gearTickUpdater.Update();
            _fluidTickUpdater.Update();
            _trainUpdateService.UpdateTrains();
        }
    }
}
