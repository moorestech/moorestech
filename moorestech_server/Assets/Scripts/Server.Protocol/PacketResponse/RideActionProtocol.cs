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
            var seatIndex = GetSeatIndexWhenRideSucceeded(result, data.PlayerId);
            return new ResponseRideActionMessagePack((byte)result, seatIndex);

            #region Internal

            RideActionResult ResolveAction(int playerId)
            {
                if (playerId < 0)
                {
                    return RideActionResult.InvalidPlayer;
                }

                var action = data.Action;
                
                // 修正 switchに書き換える
                if (action == RideActionType.Dismount)
                {
                    return _playerRidingDatastore.TryDismount(playerId);
                }

                if (action != RideActionType.Ride || !TryCreateIdentifier(data.Target, out var identifier))
                {
                    return RideActionResult.RidableNotFound;
                }

                return _playerRidingDatastore.TryRide(playerId, identifier, out _);
            }
            
            // 修正 ResolveActionと同時に実行する
            int GetSeatIndexWhenRideSucceeded(RideActionResult result,int playerId)
            {
                if (data.Action != RideActionType.Ride || result != RideActionResult.Success)
                {
                    return -1;
                }

                // 成功後の確定状態から seatIndex を返す。
                // Return the seat index from the confirmed state after success.
                if (_playerRidingDatastore.TryGetRidingState(playerId, out var state))
                {
                    return state.SeatIndex;
                }

                return -1;
            }

            // 修正 RidableIdentifierConverter.FromMessagePackを使えばいいだろバカか
            bool TryCreateIdentifier(RidableIdentifierMessagePack target, out IRidableIdentifier identifier)
            {
                identifier = null;
                if (target == null || target.RidableType != RidableType.TrainCar.AsPrimitive())
                {
                    return false;
                }

                // TrainCarInstanceId は外部データなので parse 失敗を通常の失敗結果に変換する。
                // TrainCarInstanceId is external data, so parse failure becomes a normal failure result.
                if (!long.TryParse(target.TrainCarInstanceId, out var trainCarInstanceId))
                {
                    return false;
                }

                identifier = new TrainCarRidableIdentifier(trainCarInstanceId);
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
