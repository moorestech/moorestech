using System;
using Game.PlayerRiding.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    // 乗車種別。Ride は乗車要求、Dismount は降車要求。
    // Riding action kind: Ride requests boarding, Dismount requests leaving.
    public enum RideActionType : byte
    {
        Ride,
        Dismount,
    }

    // 乗車/降車要求プロトコル（C -> S）。乗車状態の変更は PlayerRidingDatastore へ委譲する。
    // Ride/dismount request protocol (C -> S). Riding-state changes are delegated to PlayerRidingDatastore.
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
            var result = ResolveAction();
            var seatIndex = GetSeatIndexWhenRideSucceeded(result);
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

            RideActionResult ResolveAction()
            {
                if (playerId < 0)
                {
                    return RideActionResult.InvalidPlayer;
                }

                var action = (RideActionType)data.Action;
                if (action == RideActionType.Dismount)
                {
                    return _playerRidingDatastore.TryDismount(playerId);
                }

                // 外部入力の Target はプロトコル境界で検証し、不正値は RidableNotFound に倒す。
                // Validate external Target data at the protocol boundary and map invalid values to RidableNotFound.
                if (action != RideActionType.Ride || !TryCreateIdentifier(data.Target, out var identifier))
                {
                    return RideActionResult.RidableNotFound;
                }

                return _playerRidingDatastore.TryRide(playerId, identifier, out _);
            }

            int GetSeatIndexWhenRideSucceeded(RideActionResult result)
            {
                if ((RideActionType)data.Action != RideActionType.Ride || result != RideActionResult.Success)
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
            [Key(3)] public byte Action { get; set; }
            [Key(4)] public RidableIdentifierMessagePack Target { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestRideActionMessagePack() { }

            public RequestRideActionMessagePack(int playerId, byte action, RidableIdentifierMessagePack target)
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
}
