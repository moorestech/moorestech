using System;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class TrainManualInputProtocol : IPacketResponse
    {
        // クライアントから列車の手動 raw input を受け取る protocol
        // Protocol for receiving train manual raw input from the client
        public const string ProtocolTag = "va:trainManualInput";

        private readonly TrainManualControlService _trainManualControlService;

        public TrainManualInputProtocol(ServiceProvider serviceProvider)
        {
            _trainManualControlService = serviceProvider.GetService<TrainManualControlService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            // packet で届いた最新入力を service の状態へそのまま反映する
            // Push the received raw input directly into the server-side control service
            var request = MessagePackSerializer.Deserialize<TrainManualInputRequestMessagePack>(payload);
            _trainManualControlService.SetOperatingTarget(request.PlayerId, request.TrainInstanceId, default);
            _trainManualControlService.SetLatestInput(request.TrainInstanceId, request.ToRawInputState());
            return null;
        }

        [MessagePackObject]
        public class TrainManualInputRequestMessagePack : ProtocolMessagePackBase
        {
            // server 解決に必要な最小限の入力だけを運ぶ
            // Keep only the minimum fields required for server-side resolution
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public TrainInstanceId TrainInstanceId { get; set; }
            [Key(4)] public bool Forward { get; set; }
            [Key(5)] public bool Backward { get; set; }
            [Key(6)] public bool Left { get; set; }
            [Key(7)] public bool Right { get; set; }

            public TrainManualInputRequestMessagePack(
                int playerId,
                TrainInstanceId trainInstanceId,
                bool forward,
                bool backward,
                bool left,
                bool right)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                TrainInstanceId = trainInstanceId;
                Forward = forward;
                Backward = backward;
                Left = left;
                Right = right;
            }

            [Obsolete("For MessagePack deserialization only.")]
            public TrainManualInputRequestMessagePack()
            {
            }

            public TrainManualRawInputState ToRawInputState()
            {
                // packet payload を共有の生入力型へ変換する
                // Convert the packet payload into the shared raw input type
                return new TrainManualRawInputState(Forward, Backward, Left, Right);
            }
        }
    }
}
