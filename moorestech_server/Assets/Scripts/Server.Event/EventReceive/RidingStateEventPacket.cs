using System;
using Game.Context;
using Game.PlayerRiding.Interface;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 乗車状態変化を全クライアントへ broadcast するイベントパケット。
    // Broadcasts riding-state changes to all clients.
    public class RidingStateEventPacket : IDisposable, IBootInitializable
    {
        public const string EventTag = "va:event:ridingState";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IDisposable _ridingStateChangedSubscription;

        public RidingStateEventPacket(EventProtocolProvider eventProtocolProvider, IPlayerRidingDatastore playerRidingDatastore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _ridingStateChangedSubscription = playerRidingDatastore.OnRidingStateChanged.Subscribe(OnRidingStateChanged);
        }

        public void Dispose()
        {
            _ridingStateChangedSubscription.Dispose();
        }

        private void OnRidingStateChanged(RidingStateChange change)
        {
            var target = GetTarget(change);
            var seatIndex = change.IsDismount ? -1 : change.State.SeatIndex;
            var stateType = change.IsDismount ? RidingStateEventType.Dismount : RidingStateEventType.Ride;
            var messagePack = new RidingStateEventMessagePack(change.PlayerId, stateType, target, seatIndex);

            // 状態変化ペイロードを broadcast キューへ積む。
            // Enqueue the state-change payload into the broadcast queue.
            _eventProtocolProvider.AddBroadcastEvent(EventTag, MessagePackSerializer.Serialize(messagePack));
            
            #region Internal
            
            RidableIdentifierMessagePack GetTarget(RidingStateChange state)
            {
                return state.IsDismount ? 
                    null : 
                    change.State.Identifier.ToMessagePack();
            }
            
        #endregion
        }

    }

    [MessagePackObject]
    public class RidingStateEventMessagePack
    {
        [Key(0)] public int PlayerId { get; set; }
        [Key(1)] public RidingStateEventType StateType { get; set; }
        [Key(2)] public RidableIdentifierMessagePack Target { get; set; }
        [Key(3)] public int SeatIndex { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RidingStateEventMessagePack() { }

        public RidingStateEventMessagePack(int playerId, RidingStateEventType stateType, RidableIdentifierMessagePack target, int seatIndex)
        {
            PlayerId = playerId;
            StateType = stateType;
            Target = target;
            SeatIndex = seatIndex;
        }
    }

    public enum RidingStateEventType : byte
    {
        Ride,
        Dismount,
    }
}
