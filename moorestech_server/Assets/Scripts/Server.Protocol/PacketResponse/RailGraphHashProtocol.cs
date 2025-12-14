using System.Collections.Generic;
using Game.Train.RailGraph;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     クライアントのRailGraphハッシュを検証し、差分があればスナップショットを返す
    ///     Validates the client rail graph hash and returns a snapshot when mismatch is detected
    /// </summary>
    public sealed class RailGraphHashProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:railGraphHash";

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RequestMessagePack>(payload.ToArray());
            var (serverHash, serverTick) = RailGraphDatastore.GetGraphHashWithTick();
            var needsResync = request?.ClientHash != serverHash;
            RailGraphSnapshotMessagePack snapshot = null;

            if (needsResync)
            {
                Debug.LogWarning($"[RailGraphHashProtocol] Hash mismatch detected. client={request?.ClientHash ?? 0}, server={serverHash}. Sending full snapshot.");
                snapshot = new RailGraphSnapshotMessagePack(RailGraphDatastore.CaptureSnapshot());
            }

            return new ResponseMessagePack(serverHash, serverTick, needsResync, snapshot);
        }

        [MessagePackObject]
        public class RequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public uint ClientHash { get; set; }

            [System.Obsolete("デシリアライズ用のコンストラクタです。")]
            public RequestMessagePack()
            {
                Tag = ProtocolTag;
            }

            public RequestMessagePack(uint clientHash)
            {
                Tag = ProtocolTag;
                ClientHash = clientHash;
            }
        }

        [MessagePackObject]
        public class ResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public uint ServerHash { get; set; }
            [Key(3)] public long ServerTick { get; set; }
            [Key(4)] public bool NeedsResync { get; set; }
            [Key(5)] public RailGraphSnapshotMessagePack Snapshot { get; set; }

            [System.Obsolete("デシリアライズ用のコンストラクタです。")]
            public ResponseMessagePack()
            {
                Tag = ProtocolTag;
            }

            public ResponseMessagePack(uint serverHash, long serverTick, bool needsResync, RailGraphSnapshotMessagePack snapshot)
            {
                Tag = ProtocolTag;
                ServerHash = serverHash;
                ServerTick = serverTick;
                NeedsResync = needsResync;
                Snapshot = snapshot;
            }
        }
    }
}
