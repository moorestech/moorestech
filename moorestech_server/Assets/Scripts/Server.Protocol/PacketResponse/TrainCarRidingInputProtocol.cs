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
                _trainUpdateService.GetCurrentTick(),
                input.MoveForward,
                input.SelectPreviousBranch,
                input.MoveBackward,
                input.SelectNextBranch));
            return null;
        }

        [MessagePackObject]
        public class TrainCarRidingInputMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public bool MoveForward { get; set; }
            [Key(4)] public bool MoveBackward { get; set; }
            [Key(5)] public bool SelectPreviousBranch { get; set; }
            [Key(6)] public bool SelectNextBranch { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainCarRidingInputMessagePack()
            {
                Tag = ProtocolTag;
            }

            public TrainCarRidingInputMessagePack(int playerId, bool moveForward, bool moveBackward, bool selectPreviousBranch, bool selectNextBranch)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                MoveForward = moveForward;
                MoveBackward = moveBackward;
                SelectPreviousBranch = selectPreviousBranch;
                SelectNextBranch = selectNextBranch;
            }
        }
    }
}
