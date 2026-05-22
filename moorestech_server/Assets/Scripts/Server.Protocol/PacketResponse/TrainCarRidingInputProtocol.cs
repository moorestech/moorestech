using System;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class TrainCarRidingInputProtocol : IPacketResponse
    {
        private readonly TrainCarRidingInputBuffer _inputBuffer;
        private readonly TrainUpdateService _trainUpdateService;

        public const string ProtocolTag = "va:trainCarRidingInput";

        public TrainCarRidingInputProtocol(TrainCarRidingInputBuffer inputBuffer, TrainUpdateService trainUpdateService)
        {
            _inputBuffer = inputBuffer;
            _trainUpdateService = trainUpdateService;
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var input = MessagePackSerializer.Deserialize<TrainCarRidingInputMessagePack>(payload);
            _inputBuffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(
                input.PlayerId,
                new TrainCarInstanceId(input.RidingTrainCarInstanceId),
                _trainUpdateService.GetCurrentTick(),
                input.W,
                input.A,
                input.S,
                input.D));
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
