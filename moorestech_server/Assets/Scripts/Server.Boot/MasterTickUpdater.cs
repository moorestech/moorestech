using System.Collections.Generic;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Blocks.Fluid;
using Game.EnergySystem;
using Game.Gear.Common;
using Game.Train.Unit;
using Game.World.Interface.DataStore;

namespace Server.Boot
{
    // tick順序を1箇所で明示する（仕様2.1①〜④＋拡張。電力網→歯車網→流体網→ベルト搬送再構築→電力→歯車→流体→鉄道tick→ブロック更新→ベルト搬送。丸数字の⑤以降は仕様側でブロック更新・セーブを指すためここでは使わない）
    // Declares the tick order in one place: spec 2.1 ①-④ plus extensions — rebuild electric, gear, fluid and belt transport topologies, then settle electric, gear, fluid, train, update blocks, then belt transport. Circled numbers ⑤+ are reserved by the spec for block updates and save
    public class MasterTickUpdater
    {
        private readonly ElectricWireNetworkDatastore _electricWireNetworkDatastore;
        private readonly GearNetworkDatastore _gearNetworkDatastore;
        private readonly FluidNetworkDatastore _fluidNetworkDatastore;
        private readonly ElectricTickUpdater _electricTickUpdater;
        private readonly GearTickUpdater _gearTickUpdater;
        private readonly FluidTickUpdater _fluidTickUpdater;
        private readonly TrainUpdateService _trainUpdateService;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly BeltTransportDatastore _beltTransportDatastore;
        private readonly BeltTransportTickUpdater _beltTransportTickUpdater;

        // 正準順の反復に使う再利用バッファ。毎tickの確保を避ける
        // Reusable buffer for the canonical-order iteration, avoiding a per-tick allocation
        private readonly List<WorldBlockData> _tickOrderedBlocks = new();

        public MasterTickUpdater(
            ElectricWireNetworkDatastore electricWireNetworkDatastore,
            GearNetworkDatastore gearNetworkDatastore,
            FluidNetworkDatastore fluidNetworkDatastore,
            ElectricTickUpdater electricTickUpdater,
            GearTickUpdater gearTickUpdater,
            FluidTickUpdater fluidTickUpdater,
            TrainUpdateService trainUpdateService,
            IWorldBlockDatastore worldBlockDatastore,
            BeltTransportDatastore beltTransportDatastore,
            BeltTransportTickUpdater beltTransportTickUpdater)
        {
            _electricWireNetworkDatastore = electricWireNetworkDatastore;
            _gearNetworkDatastore = gearNetworkDatastore;
            _fluidNetworkDatastore = fluidNetworkDatastore;
            _electricTickUpdater = electricTickUpdater;
            _gearTickUpdater = gearTickUpdater;
            _fluidTickUpdater = fluidTickUpdater;
            _trainUpdateService = trainUpdateService;
            _worldBlockDatastore = worldBlockDatastore;
            _beltTransportDatastore = beltTransportDatastore;
            _beltTransportTickUpdater = beltTransportTickUpdater;
        }

        public void Update()
        {
            // トポロジ反映は全網とも需給計算より先（tick途中でセグメント所属を変えないため）
            // Apply every topology before any settlement so segment membership never changes mid tick
            _electricWireNetworkDatastore.RebuildIfDirty();
            _gearNetworkDatastore.RebuildIfDirty();
            _fluidNetworkDatastore.RebuildIfDirty();
            _beltTransportDatastore.RebuildIfDirty();
            _electricTickUpdater.Update();
            _gearTickUpdater.Update();
            _fluidTickUpdater.Update();
            _trainUpdateService.UpdateTrains();

            // ブロック更新を中央から一括駆動する（自走宣言した搬送系コンポーネントは対象外）
            // Drive block updates from one place; self-driven transport components are excluded
            // Dictionaryの列挙順は設置・破壊の履歴で変わる。セーブがBlockInstanceId昇順で並ぶため更新順もそれに揃え、ロード後も同じ順序で回す
            // Dictionary order follows placement and removal history; saves are ordered by BlockInstanceId, so update in that same order to keep a loaded world identical to a live one
            _tickOrderedBlocks.Clear();
            _tickOrderedBlocks.AddRange(_worldBlockDatastore.BlockMasterDictionary.Values);
            _tickOrderedBlocks.Sort((left, right) => left.Block.BlockInstanceId.CompareTo(right.Block.BlockInstanceId));

            // 設置・破壊はtick末尾で確定するため、この反復中に増減は起きない
            // Placement and removal settle at tick end, so the collection never mutates during this iteration
            foreach (var blockData in _tickOrderedBlocks) blockData.Block.TickUpdate();

            // ベルト搬送は全blockの後に進める。機械からの押し込みがこのtickのblock更新で済んでから、ベルコン上のアイテムをまとめて動かす
            // Belt transport advances after every block, so machine pushes made during this tick's block updates move with the belts in one batch
            _beltTransportTickUpdater.Update();
        }
    }
}
