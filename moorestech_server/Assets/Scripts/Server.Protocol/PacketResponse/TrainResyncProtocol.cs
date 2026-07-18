using System;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     train/rail再同期の引き金プロトコル。snapshot本体はイベント経路でpushされる
    ///     Trigger protocol for train/rail resync; snapshots are pushed over the event stream
    /// </summary>
    public sealed class TrainResyncProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:trainResync";

        private readonly TrainFullSnapshotEventPacket _trainFullSnapshotEventPacket;

        public TrainResyncProtocol(ServiceProvider serviceProvider)
        {
            _trainFullSnapshotEventPacket = serviceProvider.GetService<TrainFullSnapshotEventPacket>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<RequestMessagePack>(payload);

            // snapshotはイベント経路でpushし、応答はackのみ返す（適用経路を1本に保つ）
            // Push snapshots via the event stream; the response is a bare ack to keep one apply path
            _trainFullSnapshotEventPacket.PushFullSnapshots(context.PlayerId.Value, data.IncludeRailGraph);

            return new ResponseMessagePack(true);
        }

        [MessagePackObject]
        public class RequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool IncludeRailGraph { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestMessagePack() { }

            public RequestMessagePack(bool includeRailGraph)
            {
                Tag = ProtocolTag;
                IncludeRailGraph = includeRailGraph;
            }
        }

        [MessagePackObject]
        public class ResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool Accepted { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseMessagePack() { }

            public ResponseMessagePack(bool accepted)
            {
                Tag = ProtocolTag;
                Accepted = accepted;
            }
        }
    }
}
