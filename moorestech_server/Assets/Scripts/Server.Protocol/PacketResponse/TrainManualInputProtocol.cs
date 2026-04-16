using System;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public sealed class TrainManualInputProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:trainManualInput";

        private readonly TrainManualControlService _trainManualControlService;
        private readonly TrainUpdateService _trainUpdateService;

        public TrainManualInputProtocol(ServiceProvider serviceProvider)
        {
            _trainManualControlService = serviceProvider.GetService<TrainManualControlService>();
            _trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<TrainManualInputRequestMessagePack>(payload);
            _trainManualControlService.TryUpdateInput(
                data.PlayerId,
                data.TrainCarInstanceId,
                data.RawInputMask,
                _trainUpdateService.GetCurrentTick());
            return null;
        }

        [MessagePackObject]
        public class TrainManualInputRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public long TrainCarInstanceId { get; set; }
            [Key(4)] public int RawInputMask { get; set; }

            public TrainManualInputRequestMessagePack(int playerId, long trainCarInstanceId, int rawInputMask)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                TrainCarInstanceId = trainCarInstanceId;
                RawInputMask = rawInputMask;
            }

            [Obsolete("Reserved for MessagePack deserialization.")]
            public TrainManualInputRequestMessagePack()
            {
            }
        }
    }
}
