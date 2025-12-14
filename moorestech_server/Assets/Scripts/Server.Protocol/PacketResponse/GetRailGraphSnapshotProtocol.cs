using System.Collections.Generic;
using Game.Train.RailGraph;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     RailGraphスナップショットを提供するプロトコル
    ///     Protocol that returns the current rail graph snapshot
    /// </summary>
    public sealed class GetRailGraphSnapshotProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getRailGraphSnapshot";

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var snapshot = RailGraphDatastore.CaptureSnapshot();
            var message = new RailGraphSnapshotMessagePack(snapshot);
            return new ResponseMessagePack(message);
        }

        [MessagePackObject]
        public class RequestMessagePack : ProtocolMessagePackBase
        {
            [System.Obsolete("デシリアライズ用のコンストラクタです。")]
            public RequestMessagePack()
            {
                Tag = ProtocolTag;
            }
        }

        [MessagePackObject]
        public class ResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public RailGraphSnapshotMessagePack Snapshot { get; set; }

            [System.Obsolete("デシリアライズ用のコンストラクタです。")]
            public ResponseMessagePack()
            {
                Tag = ProtocolTag;
            }

            public ResponseMessagePack(RailGraphSnapshotMessagePack snapshot)
            {
                Tag = ProtocolTag;
                Snapshot = snapshot;
            }
        }
    }
}
