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
            var playerId = GetAuthorizedPlayerId();
            var result = ResolveAction(out var seatIndex);
            return new ResponseRideActionMessagePack((byte)result, seatIndex);

            #region Internal

            int GetAuthorizedPlayerId()
            {
                // 接続コンテキストに紐付いた playerId だけを状態変更対象にする。
                // Only the playerId bound to this connection may mutate riding state.
                if (!context.PlayerId.HasValue || context.PlayerId.Value != data.PlayerId)
                {
                    return -1;
                }

                return context.PlayerId.Value;
            }

            RideActionResult ResolveAction(out int seatIndex)
            {
                seatIndex = -1;
                if (playerId < 0)
                {
                    return RideActionResult.InvalidPlayer;
                }

                // Action ごとの状態変更を実行し、乗車成功時は割り当て seatIndex を同時に返す。
                // Execute each action and return the assigned seat index together on ride success.
                switch (data.Action)
                {
                    case RideActionType.Ride:
                        if (!TryCreateIdentifier(data.Target, out var identifier))
                        {
                            return RideActionResult.RidableNotFound;
                        }
                        var rideResult = _playerRidingDatastore.TryRide(playerId, identifier, out seatIndex);
                        if (rideResult != RideActionResult.Success)
                        {
                            seatIndex = -1;
                        }
                        return rideResult;
                    case RideActionType.Dismount:
                        return _playerRidingDatastore.TryDismount(playerId);
                    default:
                        return RideActionResult.RidableNotFound;
                }
            }

            bool TryCreateIdentifier(RidableIdentifierMessagePack target, out IRidableIdentifier identifier)
            {
                identifier = null;
                if (target == null || target.RidableType != RidableType.TrainCar.AsPrimitive())
                {
                    return false;
                }

                // 外部入力の値検証後、共通 converter で識別子へ変換する。
                // After validating external input, use the shared converter to create the identifier.
                if (!long.TryParse(target.TrainCarInstanceId, out var trainCarInstanceId))
                {
                    return false;
                }

                var validatedTarget = RidableIdentifierMessagePack.CreateTrainCarMessage(trainCarInstanceId);
                identifier = RidableIdentifierConverter.FromMessagePack(validatedTarget);
                return true;
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
            [Key(2)] public byte Result { get; set; }
            [Key(3)] public int SeatIndex { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseRideActionMessagePack() { }

            public ResponseRideActionMessagePack(byte result, int seatIndex)
            {
                Tag = ProtocolTag;
                Result = result;
                SeatIndex = seatIndex;
            }
        }
    }
    
    public enum RideActionType : byte
    {
        Ride,
        Dismount,
    }
}
