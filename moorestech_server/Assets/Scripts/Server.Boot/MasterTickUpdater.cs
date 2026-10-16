using System.Collections.Generic;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Blocks.Fluid;
using Game.EnergySystem;
using Game.Gear.Common;
using Game.Challenge;
using Game.CleanRoom;
using Game.SaveLoad;
using Game.SaveLoad.Snapshot;
using Game.Train.Unit;
using Game.World.Interface.DataStore;
using Server.Boot.Loop;
using Server.Boot.Loop.PacketProcessing;

namespace Server.Boot
{
    // 1tickの全順序を1箇所で明示する。本体(仕様2.1①〜④＋拡張: 電力網→歯車網→流体網→ベルト搬送再構築→電力→歯車→流体→鉄道tick→ブロック更新→ベルト搬送)→チャレンジ・クリーンルーム→末尾(操作の一括適用→別スレッド処理)→境界(ハッシュ・tick++)→セーブ→スナップショット
    // Declares the whole tick order in one place: the body (spec 2.1 ①-④ plus extensions: rebuild electric, gear, fluid and belt transport topologies, then settle electric, gear, fluid, train, update blocks, belt transport), then challenge and clean room, the tick end (apply input, drain other-thread work), the boundary (hash, tick++), save, snapshot
    // 購読や登録順に頼る処理はここには無い。GameUpdaterにはこのUpdateを1本だけ登録する
    // Nothing here relies on subscriptions or registration order; GameUpdater registers only this Update
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
        private readonly IBeltTransportMutation _beltTransportMutation;
        private readonly BeltTransportTickUpdater _beltTransportTickUpdater;
        private readonly ChallengeDatastore _challengeDatastore;
        private readonly CleanRoomDatastore _cleanRoomDatastore;
        private readonly WorldMutationTickEndUpdater _worldMutationTickEndUpdater;
        private readonly ServerThreadActionQueue _serverThreadActionQueue;
        private readonly WorldSaveCoordinator _worldSaveCoordinator;
        private readonly WorldSnapshotRing _worldSnapshotRing;

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
            IBeltTransportMutation beltTransportMutation,
            BeltTransportTickUpdater beltTransportTickUpdater,
            ChallengeDatastore challengeDatastore,
            CleanRoomDatastore cleanRoomDatastore,
            WorldMutationTickEndUpdater worldMutationTickEndUpdater,
            ServerThreadActionQueue serverThreadActionQueue,
            WorldSaveCoordinator worldSaveCoordinator,
            WorldSnapshotRing worldSnapshotRing)
        {
            _electricWireNetworkDatastore = electricWireNetworkDatastore;
            _gearNetworkDatastore = gearNetworkDatastore;
            _fluidNetworkDatastore = fluidNetworkDatastore;
            _electricTickUpdater = electricTickUpdater;
            _gearTickUpdater = gearTickUpdater;
            _fluidTickUpdater = fluidTickUpdater;
            _trainUpdateService = trainUpdateService;
            _worldBlockDatastore = worldBlockDatastore;
            _beltTransportMutation = beltTransportMutation;
            _beltTransportTickUpdater = beltTransportTickUpdater;
            _challengeDatastore = challengeDatastore;
            _cleanRoomDatastore = cleanRoomDatastore;
            _worldMutationTickEndUpdater = worldMutationTickEndUpdater;
            _serverThreadActionQueue = serverThreadActionQueue;
            _worldSaveCoordinator = worldSaveCoordinator;
            _worldSnapshotRing = worldSnapshotRing;
        }

        public void Update()
        {
            // トポロジ反映は全網とも需給計算より先（tick途中でセグメント所属を変えないため）
            // Apply every topology before any settlement so segment membership never changes mid tick
            _electricWireNetworkDatastore.RebuildIfDirty();
            _gearNetworkDatastore.RebuildIfDirty();
            _fluidNetworkDatastore.RebuildIfDirty();
            _beltTransportMutation.RebuildIfDirty();
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

            // チャレンジ判定とクリーンルームは本体の結果を見る。世界は変えない
            // Challenge checks and the clean room read the body's results; neither mutates the world
            _challengeDatastore.Update();
            _cleanRoomDatastore.Update();

            // tick末尾: 固定した入力と予約破壊を一つの更新器で確定し、他スレッドから積まれた処理を流す。派生する網の再構築は次tick先頭のRebuildIfDirtyに委ねる
            // Tick end: commit frozen input and reserved removals through one updater, then drain other-thread work; derived network rebuilding is deferred to RebuildIfDirty at the next tick head
            _worldMutationTickEndUpdater.Update();
            _serverThreadActionQueue.Drain();

            // 境界: 操作の適用まで終えた状態で列車・レール・ベルトのハッシュを出し、tick番号を進める。ベルトの組はまだ作り直されていないので搬送直後と同じハッシュになる
            // Boundary: emit the train, rail and belt hashes from the state after input was applied and advance the tick; the belt assembly is not rebuilt yet, so its hash equals the post-transport one
            _trainUpdateService.CompleteTick();

            // 全世界変更の確定後が唯一のセーブ可能な安定点（仕様2.1⑦）。常時記録のスナップショットも同じ安定点で取る（Startされるまで何もしない）
            // The point after every world mutation commits is the only save-stable boundary (spec 2.1-7); always-on snapshots are captured at the same point (inert until Start)
            _worldSaveCoordinator.SaveIfRequested();
            _worldSnapshotRing.Update();
        }
    }
}
