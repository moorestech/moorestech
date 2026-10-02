using System;
using System.Collections.Generic;
using Game.Context;
using Game.Block.Blocks.BeltConveyor.Transport;
using Server.Util.MessagePack.BeltTransport;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 接続登録時にtrain/railの初期full snapshotをイベント経路でpushする
    // Pushes initial full train/rail snapshots over the event stream on connection registration
    public sealed class TrainFullSnapshotEventPacket : IBootInitializable
    {
        public const string RailGraphFullSnapshotEventTag = "va:event:railGraphFullSnapshot";
        public const string TrainUnitFullSnapshotEventTag = "va:event:trainUnitFullSnapshot";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly TrainUpdateService _trainUpdateService;
        private readonly BeltWorldTransport _beltTransport;
        private readonly List<int> _pendingPlayers = new();
        public const string BeltFullSnapshotEventTag = "va:event:beltFullSnapshot";

        public TrainFullSnapshotEventPacket(
            EventProtocolProvider eventProtocolProvider,
            IRailGraphDatastore railGraphDatastore,
            ITrainUnitLookupDatastore trainUnitLookupDatastore,
            TrainUpdateService trainUpdateService, BeltWorldTransport beltTransport)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _railGraphDatastore = railGraphDatastore;
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _trainUpdateService = trainUpdateService;
            _beltTransport = beltTransport;
        }

        public void Load()
        {
            // 接続登録を記録し、確定境界で初期snapshotを送る。
            // Record registrations and send initial snapshots at the committed boundary.
            _eventProtocolProvider.OnPlayerEventStreamRegistered.Subscribe(playerId => _pendingPlayers.Add(playerId));
        }

        public void SendPendingInitialSnapshots()
        {
            // 搬送と配置変更の確定後に全初期状態を同じ境界で送る。
            // Send all initial states at the same boundary after transport and placement mutations commit.
            foreach (var playerId in _pendingPlayers) PushFullSnapshots(playerId);
            _pendingPlayers.Clear();
        }

        // rail→belt→trainの順で対象プレイヤーへ初期full snapshotをpushする
        // Push initial full snapshots to the player: rail, belt, then train
        private void PushFullSnapshots(int playerId)
        {
            PushRailGraphFullSnapshot(playerId);
            var beltPayload = MessagePackSerializer.Serialize(new BeltSnapshotMessagePack(_beltTransport.CaptureCommittedSnapshot()));
            _eventProtocolProvider.AddEvent(playerId, BeltFullSnapshotEventTag, beltPayload);
            PushTrainUnitFullSnapshot(playerId);

            #region Internal

            void PushRailGraphFullSnapshot(int targetPlayerId)
            {
                var snapshot = _railGraphDatastore.CaptureSnapshot(_trainUpdateService.GetCurrentTick());

                // watermarkは発行済み最新IDを使い、新規採番しない（他クライアントにseq穴を作らない）
                // Use the latest issued id as watermark without consuming a new one (no seq gaps for others)
                var message = new RailGraphSnapshotMessagePack(snapshot, _trainUpdateService.GetCurrentTickSequenceId());
                var payload = MessagePackSerializer.Serialize(new RailGraphFullSnapshotEventMessagePack(message));
                _eventProtocolProvider.AddEvent(targetPlayerId, RailGraphFullSnapshotEventTag, payload);
            }

            void PushTrainUnitFullSnapshot(int targetPlayerId)
            {
                var bundles = new List<TrainUnitSnapshotBundle>();
                var snapshots = new List<TrainUnitSnapshotBundleMessagePack>();
                foreach (var train in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    var bundle = TrainUnitSnapshotFactory.CreateSnapshot(train);
                    bundles.Add(bundle);
                    snapshots.Add(new TrainUnitSnapshotBundleMessagePack(bundle));
                }

                var unitsHash = TrainUnitSnapshotHashCalculator.Compute(bundles);
                var payload = MessagePackSerializer.Serialize(new TrainUnitFullSnapshotEventMessagePack(
                    snapshots,
                    _trainUpdateService.GetCurrentTick(),
                    unitsHash,
                    _trainUpdateService.GetCurrentTickSequenceId()));
                _eventProtocolProvider.AddEvent(targetPlayerId, TrainUnitFullSnapshotEventTag, payload);
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class RailGraphFullSnapshotEventMessagePack
        {
            [Key(0)] public RailGraphSnapshotMessagePack Snapshot { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailGraphFullSnapshotEventMessagePack() { }

            public RailGraphFullSnapshotEventMessagePack(RailGraphSnapshotMessagePack snapshot)
            {
                Snapshot = snapshot;
            }
        }

        [MessagePackObject]
        public class TrainUnitFullSnapshotEventMessagePack
        {
            [Key(0)] public List<TrainUnitSnapshotBundleMessagePack> Snapshots { get; set; }
            [Key(1)] public uint ServerTick { get; set; }
            [Key(2)] public uint UnitsHash { get; set; }
            [Key(3)] public uint WatermarkTickSequenceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainUnitFullSnapshotEventMessagePack() { }

            public TrainUnitFullSnapshotEventMessagePack(List<TrainUnitSnapshotBundleMessagePack> snapshots, uint serverTick, uint unitsHash, uint watermarkTickSequenceId)
            {
                Snapshots = snapshots;
                ServerTick = serverTick;
                UnitsHash = unitsHash;
                WatermarkTickSequenceId = watermarkTickSequenceId;
            }
        }

        #endregion
    }
}
