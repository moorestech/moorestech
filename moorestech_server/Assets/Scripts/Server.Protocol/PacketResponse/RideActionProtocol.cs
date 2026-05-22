using System;
using Game.PlayerRiding.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class RideActionProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:rideAction";

        private readonly IPlayerRidingDatastore _playerRidingDatastore;

        public RideActionProtocol(ServiceProvider serviceProvider)
        {
            _playerRidingDatastore = serviceProvider.GetService<IPlayerRidingDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<RequestRideActionMessagePack>(payload);
            
            var result = ResolveAction(data.PlayerId);
            
            return new ResponseRideActionMessagePack(result);

            #region Internal

            (RideActionResult result, int seatIndex) ResolveAction(int playerId)
            {
                // Action ごとの状態変更を実行し、乗車成功時は割り当て seatIndex を同時に返す。
                // Execute each action and return the assigned seat index together on ride success.
                switch (data.Action)
                {
                    case RideActionType.Ride:
                        var identifier = RidableIdentifierConverter.FromMessagePack(data.Target);
                        var rideResult = _playerRidingDatastore.TryRide(playerId, identifier, out var seatIndex);
                        if (rideResult != RideActionResult.Success)
                        {
                            seatIndex = -1;
                        }
                        
                        return (rideResult, seatIndex);
                    case RideActionType.Dismount:
                        return (_playerRidingDatastore.TryDismount(playerId), -1);
                    default:
                        return (RideActionResult.RidableNotFound, -1);
                }
            }
            
            #endregion
        }

        [MessagePackObject]
        public class RequestRideActionMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public RideActionType Action { get; set; }
            [Key(4)] public RidableIdentifierMessagePack Target { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestRideActionMessagePack() { }

            public RequestRideActionMessagePack(int playerId, RideActionType action, RidableIdentifierMessagePack target)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Action = action;
                Target = target;
            }
        }

        [MessagePackObject]
        public class ResponseRideActionMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public RideActionResult Result { get; set; }
            [Key(3)] public int SeatIndex { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseRideActionMessagePack() { }

            public ResponseRideActionMessagePack((RideActionResult result, int seatIndex) actionResult)
            {
                Tag = ProtocolTag;
                Result = actionResult.result;
                SeatIndex = actionResult.seatIndex;
            }
        }
    }
    
    public enum RideActionType : byte
    {
        Ride,
        Dismount,
    }
}
