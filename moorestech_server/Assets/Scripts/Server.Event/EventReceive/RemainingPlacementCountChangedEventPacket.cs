using System;
using Game.Construction;
using Game.Context;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// 残り設置数の変更を該当プレイヤーへ通知する。財布1件の最新値だけを送る
    /// Notifies the owning player of a remaining-placement change; carries the latest value of one wallet
    /// </summary>
    public class RemainingPlacementCountChangedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:remainingPlacementCountChanged";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IRemainingPlacementCountLookup _remainingPlacementCountLookup;

        public RemainingPlacementCountChangedEventPacket(EventProtocolProvider eventProtocolProvider, IRemainingPlacementCountLookup remainingPlacementCountLookup)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _remainingPlacementCountLookup = remainingPlacementCountLookup;
        }

        public void Load()
        {
            _remainingPlacementCountLookup.OnRemainingCountChanged.Subscribe(OnRemainingCountChanged);
        }

        private void OnRemainingCountChanged(RemainingPlacementCountChange change)
        {
            var payload = MessagePackSerializer.Serialize(new RemainingPlacementCountMessagePack(change.WalletBlockId.AsPrimitive(), change.RemainingCount));
            _eventProtocolProvider.AddEvent(change.PlayerId, EventTag, payload);
        }

        #region MessagePack

        // handshake同梱にも使う共通型
        // Shared shape also bundled into the initial handshake
        [MessagePackObject]
        public class RemainingPlacementCountMessagePack
        {
            [Key(0)] public int WalletBlockId { get; set; }
            [Key(1)] public int RemainingCount { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RemainingPlacementCountMessagePack() { }

            public RemainingPlacementCountMessagePack(int walletBlockId, int remainingCount)
            {
                WalletBlockId = walletBlockId;
                RemainingCount = remainingCount;
            }
        }

        #endregion
    }
}
