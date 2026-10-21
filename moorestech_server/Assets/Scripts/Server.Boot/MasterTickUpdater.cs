using System.Collections.Generic;
using Core.Update;
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
using UniRx;

namespace Server.Boot
{
    // 1tickの全順序を1箇所で明示する。本体(仕様2.1①〜④＋拡張: 電力→歯車→流体→鉄道tick→ブロック更新→ベルト搬送)→チャレンジ・クリーンルーム→末尾(操作の一括適用→別スレッド処理→派生網の再構築)→境界(ハッシュ・tick++)→セーブ→スナップショット
    // Declares the whole tick order in one place: the body (spec 2.1 ①-④ plus extensions: settle electric, gear, fluid, train, update blocks, belt transport), then challenge and clean room, the tick end (apply input, drain other-thread work, rebuild derived networks), the boundary (hash, tick++), save, snapshot
    // 処理順を購読や登録順に頼る箇所はここには無い(設置・撤去の購読は正準順の作り直し予約だけ)。GameUpdaterにはこのUpdateを1本だけ登録する
    // Nothing here derives its order from subscriptions or registration order (the place/remove subscription only schedules the canonical-order rebuild); GameUpdater registers only this Update
    public class MasterTickUpdater : IMasterTick
    {
        private readonly IWorldTopologyRebuilder _worldTopologyRebuilder;
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

        // 正準順(BlockInstanceId昇順)の block 列。block の増減があった tick の次の先頭でだけ作り直す
        // Blocks in canonical order (ascending BlockInstanceId); rebuilt only at the head of the tick after the block set changed
        private readonly List<WorldBlockData> _tickOrderedBlocks = new();
        private bool _isTickOrderDirty = true;

        public MasterTickUpdater(
            IWorldTopologyRebuilder worldTopologyRebuilder,
            ElectricTickUpdater electricTickUpdater,
            GearTickUpdater gearTickUpdater,
            FluidTickUpdater fluidTickUpdater,
            TrainUpdateService trainUpdateService,
            IWorldBlockDatastore worldBlockDatastore,
            IWorldBlockUpdateEvent worldBlockUpdateEvent,
            IBeltTransportMutation beltTransportMutation,
            BeltTransportTickUpdater beltTransportTickUpdater,
            ChallengeDatastore challengeDatastore,
            CleanRoomDatastore cleanRoomDatastore,
            WorldMutationTickEndUpdater worldMutationTickEndUpdater,
            ServerThreadActionQueue serverThreadActionQueue,
            WorldSaveCoordinator worldSaveCoordinator,
            WorldSnapshotRing worldSnapshotRing)
        {
            _worldTopologyRebuilder = worldTopologyRebuilder;
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

            // 設置・撤去は全てこの2イベントを通るので、購読で正準順の作り直しを予約する(毎tickの全件sortを避ける)
            // Every placement and removal passes through these two events, so subscribe to schedule the canonical-order rebuild (avoids sorting everything every tick)
            worldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(_ => _isTickOrderDirty = true);
            worldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(_ => _isTickOrderDirty = true);
        }

        public void Tick()
        {
            // 網は前tickの末尾で作り直し済み。ここから搬送tickまでが機械からベルトへの押し込みを受け付ける区間
            // Networks were rebuilt at the previous tick end; from here until the transport tick, machine pushes into belts are accepted
            _beltTransportMutation.BeginMachinePushWindow();
            _electricTickUpdater.Update();
            _gearTickUpdater.Update();
            _fluidTickUpdater.Update();
            _trainUpdateService.UpdateTrains();

            // ブロック更新を中央から一括駆動する（自走宣言した搬送系コンポーネントは対象外）
            // Drive block updates from one place; self-driven transport components are excluded
            // Dictionaryの列挙順は設置・破壊の履歴で変わる。セーブがBlockInstanceId昇順で並ぶため更新順もそれに揃え、ロード後も同じ順序で回す
            // Dictionary order follows placement and removal history; saves are ordered by BlockInstanceId, so update in that same order to keep a loaded world identical to a live one
            // block 更新フェーズは隣のインベントリへ直接押し込むので更新順で結果が変わる(機械→チェスト→機械の段数、同じ空きの取り合い)。順を履歴から切り離すのがこの並びの役目
            // The block update phase pushes straight into neighbouring inventories, so its order changes results (hops of machine→chest→machine, contention for one free slot); this ordering detaches it from history
            // 並べ替えは block の増減があったときだけ。毎tickの全件sortは block 数の n log n で、ベルト搬送より重かった(ADR 0084 §サーバー1tickの内訳)
            // Re-sort only after the block set changed; sorting everything every tick cost n log n in the block count and outweighed belt transport (ADR 0084, tick breakdown)
            if (_isTickOrderDirty)
            {
                _tickOrderedBlocks.Clear();
                _tickOrderedBlocks.AddRange(_worldBlockDatastore.BlockMasterDictionary.Values);
                _tickOrderedBlocks.Sort((left, right) => left.Block.BlockInstanceId.CompareTo(right.Block.BlockInstanceId));
                _isTickOrderDirty = false;
            }

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

            // tick末尾: 固定した入力と予約破壊を一つの更新器で確定し、他スレッドから積まれた処理を流す。その変更で崩れた網はここで1回だけ作り直す（tick途中でセグメント所属を変えないため）
            // Tick end: commit frozen input and reserved removals through one updater, then drain other-thread work; networks invalidated by them are rebuilt here exactly once (segment membership never changes mid tick)
            _worldMutationTickEndUpdater.Update();
            _serverThreadActionQueue.Drain();
            _worldTopologyRebuilder.RebuildTopologiesIfDirty();

            // 境界: 操作の適用と再構築まで終えた状態で列車・レール・ベルトのハッシュを出し、tick番号を進める。再構築したベルトの全量はハッシュより前に送られている
            // Boundary: emit the train, rail and belt hashes from the state after input and rebuilds, and advance the tick; a rebuilt belt assembly's full state was already sent before the hash
            _trainUpdateService.CompleteTick();

            // 全世界変更の確定後が唯一のセーブ可能な安定点（仕様2.1⑦）。常時記録のスナップショットも同じ安定点で取る（Startされるまで何もしない）
            // The point after every world mutation commits is the only save-stable boundary (spec 2.1-7); always-on snapshots are captured at the same point (inert until Start)
            _worldSaveCoordinator.SaveIfRequested();
            _worldSnapshotRing.Update();
        }

        public void RebuildTopologiesIfDirty()
        {
            _worldTopologyRebuilder.RebuildTopologiesIfDirty();
        }
    }
}
