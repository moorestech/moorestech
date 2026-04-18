using System;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class TrainCarRidingInputProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:trainCarRidingInput";

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            MessagePackSerializer.Deserialize<TrainCarRidingInputMessagePack>(payload);
            return null;
        }

        [MessagePackObject]
        public class TrainCarRidingInputMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public long RidingTrainCarInstanceId { get; set; }
            [Key(4)] public bool W { get; set; }
            [Key(5)] public bool A { get; set; }
            [Key(6)] public bool S { get; set; }
            [Key(7)] public bool D { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainCarRidingInputMessagePack()
            {
                Tag = ProtocolTag;
            }

            public TrainCarRidingInputMessagePack(int playerId, long ridingTrainCarInstanceId, bool w, bool a, bool s, bool d)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                RidingTrainCarInstanceId = ridingTrainCarInstanceId;
                W = w;
                A = a;
                S = s;
                D = d;
            }
        }
    }
}
