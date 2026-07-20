using Game.EnergySystem;
using Game.Gear.Common;

namespace Server.Boot
{
    // 仕様2.1のtick順序を1箇所で明示する（①電力網再構築→②歯車網再構築→③電力tick→④歯車tick）
    // Declares the spec 2.1 tick order in one place: rebuild electric, rebuild gear, settle electric, settle gear
    public class MasterTickUpdater
    {
        private readonly ElectricWireNetworkDatastore _electricWireNetworkDatastore;
        private readonly GearNetworkDatastore _gearNetworkDatastore;
        private readonly ElectricTickUpdater _electricTickUpdater;
        private readonly GearTickUpdater _gearTickUpdater;

        public MasterTickUpdater(
            ElectricWireNetworkDatastore electricWireNetworkDatastore,
            GearNetworkDatastore gearNetworkDatastore,
            ElectricTickUpdater electricTickUpdater,
            GearTickUpdater gearTickUpdater)
        {
            _electricWireNetworkDatastore = electricWireNetworkDatastore;
            _gearNetworkDatastore = gearNetworkDatastore;
            _electricTickUpdater = electricTickUpdater;
            _gearTickUpdater = gearTickUpdater;
        }

        public void Update()
        {
            // トポロジ反映は両網とも需給計算より先（tick途中でセグメント所属を変えないため）
            // Apply both topologies before any settlement so segment membership never changes mid tick
            _electricWireNetworkDatastore.RebuildIfDirty();
            _gearNetworkDatastore.RebuildIfDirty();
            _electricTickUpdater.Update();
            _gearTickUpdater.Update();
            // 将来のFluid/Train等のtickはここに追記する
            // Future ticks such as fluid and train are appended here
        }
    }
}
