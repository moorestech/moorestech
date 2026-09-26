using System;
using System.Collections.Generic;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.Unit;
using Game.Train.Unit.TickSynchronization;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 接続登録時にtrain/railのfull snapshotをイベント経路でpushする
    // Pushes full train/rail snapshots over the event stream on connection
    public sealed class TrainFullSnapshotEventPacket : IBootInitializable
    {
        public const string RailGraphFullSnapshotEventTag = "va:event:railGraphFullSnapshot";
        public const string TrainUnitFullSnapshotEventTag = "va:event:trainUnitFullSnapshot";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly TrainTickSequenceSource _trainTickSequenceSource;

        public TrainFullSnapshotEventPacket(
            EventProtocolProvider eventProtocolProvider,
            IRailGraphDatastore railGraphDatastore,
            ITrainUnitLookupDatastore trainUnitLookupDatastore,
            TrainTickSequenceSource trainTickSequenceSource)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _railGraphDatastore = railGraphDatastore;
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _trainTickSequenceSource = trainTickSequenceSource;
        }

        public void Load()
        {
            // 新規接続の登録完了を購読し、同期的に初期snapshotをpushする（順序契約）
            // Subscribe registration completion and push initial snapshots synchronously (ordering contract)
            _eventProtocolProvider.OnPlayerEventStreamRegistered.Subscribe(PushInitialSnapshots);
        }

        // rail→trainの順で対象プレイヤーへfull snapshotをpushする
        // Push full snapshots (rail first, then train) to the player
        private void PushInitialSnapshots(int playerId)
        {
            PushRailGraphFullSnapshot(playerId);
            PushTrainUnitFullSnapshot(playerId);

            #region Internal

            void PushRailGraphFullSnapshot(int targetPlayerId)
            {
                var snapshot = _railGraphDatastore.CaptureSnapshot(_trainTickSequenceSource.Sequence.Tick);

                // watermarkは発行済み最新IDを使い、新規採番しない（他クライアントにseq穴を作らない）
                // Use the latest issued id as watermark without consuming a new one (no seq gaps for others)
                var message = new RailGraphSnapshotMessagePack(snapshot, _trainTickSequenceSource.Sequence.SequenceId);
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
                    _trainTickSequenceSource.Sequence.Tick,
                    unitsHash,
                    _trainTickSequenceSource.Sequence.SequenceId));
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
