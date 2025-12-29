using Game.Train.Common;
using Game.Train.Train;
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
        public const string ProtocolTag = "va:getTrainUnitSnapshots";

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            // 現在登録済みの列車からスナップショットを構築
            // Build snapshots for every registered train unit
            var snapshots = new List<TrainUnitSnapshotBundleMessagePack>();
            foreach (var train in TrainUpdateService.Instance.GetRegisteredTrains())
            {
                var bundle = TrainUnitSnapshotFactory.CreateSnapshot(train);
                snapshots.Add(new TrainUnitSnapshotBundleMessagePack(bundle));
            }

            return new ResponseMessagePack(snapshots, TrainUpdateService.CurrentTick);
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
            [Key(3)] public long ServerTick { get; set; }

            [Obsolete("MessagePack用のコンストラクタです。")]
            public ResponseMessagePack()
            {
                Tag = ProtocolTag;
            }

            public ResponseMessagePack(List<TrainUnitSnapshotBundleMessagePack> snapshots, long serverTick)
            {
                Tag = ProtocolTag;
                Snapshots = snapshots;
                ServerTick = serverTick;
            }
        }
    }
}
