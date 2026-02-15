using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using System;
using System.Collections.Generic;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// 列車スナップショットを応答するプロトコル
    /// Protocol that returns the current train unit snapshots
    /// </summary>
    public sealed class GetTrainUnitSnapshotsProtocol : IPacketResponse
    {
        private readonly TrainUpdateService _trainUpdateService;
        public const string ProtocolTag = "va:getTrainUnitSnapshots";

        public GetTrainUnitSnapshotsProtocol(TrainUpdateService trainUpdateService)
        {
            _trainUpdateService = trainUpdateService;
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            // 全TrainUnitのスナップショットとハッシュを生成する
            // Build snapshots and hash for every registered train unit
            var bundles = new List<TrainUnitSnapshotBundle>();
            var snapshots = new List<TrainUnitSnapshotBundleMessagePack>();
            foreach (var train in _trainUpdateService.GetRegisteredTrains())
            {
                var bundle = TrainUnitSnapshotFactory.CreateSnapshot(train);
                bundles.Add(bundle);
                snapshots.Add(new TrainUnitSnapshotBundleMessagePack(bundle));
            }

            var unitsHash = TrainUnitSnapshotHashCalculator.Compute(bundles);
            return new ResponseMessagePack(
                snapshots,
                _trainUpdateService.GetCurrentTick(),
                unitsHash,
                _trainUpdateService.NextTickSequenceId());
        }

        [MessagePackObject]
        public class RequestMessagePack : ProtocolMessagePackBase
        {
            [Obsolete("MessagePack用のコンストラクタです。")]
            public RequestMessagePack()
            {
                Tag = ProtocolTag;
            }
        }

        [MessagePackObject]
        public class ResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<TrainUnitSnapshotBundleMessagePack> Snapshots { get; set; }
            [Key(3)] public uint ServerTick { get; set; }
            [Key(4)] public uint UnitsHash { get; set; }
            [Key(5)] public uint TickSequenceId { get; set; }

            [Obsolete("MessagePack用のコンストラクタです。")]
            public ResponseMessagePack()
            {
                Tag = ProtocolTag;
            }

            public ResponseMessagePack(List<TrainUnitSnapshotBundleMessagePack> snapshots, uint serverTick, uint unitsHash, uint tickSequenceId)
            {
                Tag = ProtocolTag;
                Snapshots = snapshots;
                ServerTick = serverTick;
                UnitsHash = unitsHash;
                TickSequenceId = tickSequenceId;
            }
        }
    }
}

