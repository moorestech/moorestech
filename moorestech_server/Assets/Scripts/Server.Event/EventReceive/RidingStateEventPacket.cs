using System;
using Game.PlayerRiding.Interface;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 乗車状態変化を全クライアントへ broadcast するイベントパケット。
    // Broadcasts riding-state changes to all clients.
    public class RidingStateEventPacket
    {
        public const string EventTag = "va:event:ridingState";

        private readonly EventProtocolProvider _eventProtocolProvider;

        public RidingStateEventPacket(EventProtocolProvider eventProtocolProvider, IPlayerRidingDatastore playerRidingDatastore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            playerRidingDatastore.OnRidingStateChanged.Subscribe(OnRidingStateChanged);
        }

        private void OnRidingStateChanged(RidingStateChange change)
        {
            var target = GetTarget(change);
            var seatIndex = change.IsDismount ? -1 : change.State.SeatIndex;
            var messagePack = new RidingStateEventMessagePack(change.PlayerId, target, seatIndex);

            // 状態変化ペイロードを broadcast キューへ積む。
            // Enqueue the state-change payload into the broadcast queue.
            var payload = MessagePackSerializer.Serialize(messagePack);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        private static RidableIdentifierMessagePack GetTarget(RidingStateChange change)
        {
            if (change.IsDismount)
            {
                return null;
            }

            return change.State.Identifier.ToMessagePack();
        }
    }

    [MessagePackObject]
    public class RidingStateEventMessagePack
    {
        [Key(0)] public int PlayerId { get; set; }
        [Key(1)] public RidableIdentifierMessagePack Target { get; set; }
        [Key(2)] public int SeatIndex { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RidingStateEventMessagePack() { }

        public RidingStateEventMessagePack(int playerId, RidableIdentifierMessagePack target, int seatIndex)
        {
            PlayerId = playerId;
            Target = target;
            SeatIndex = seatIndex;
        }

        [IgnoreMember] public bool IsDismount => Target == null;
    }
}
