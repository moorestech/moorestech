using System;
using Game.Context;
using Game.Crafting.Interface;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // ワンクリッククラフトの成立をクライアントへ通知する。送信要求ではなくサーバーで素材を消費し終えた事実だけを届ける
    // Notifies the client that a one-click craft went through; only the fact that the server consumed the materials is delivered, never the request
    public class CraftCompletedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:craftCompleted";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly CraftEvent _craftEvent;

        public CraftCompletedEventPacket(EventProtocolProvider eventProtocolProvider, CraftEvent craftEvent)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _craftEvent = craftEvent;
        }

        public void Load()
        {
            // クラフトはプレイヤー個人のインベントリ操作なので、作った本人にだけ届ける
            // A craft is an operation on one player's own inventory, so it reaches only the player who crafted
            _craftEvent.OnCraftItem.Subscribe(craft =>
            {
                var eventData = new CraftCompletedEventMessagePack(craft.playerId, craft.craftRecipe.CraftRecipeGuid);
                var payload = MessagePackSerializer.Serialize(eventData);
                _eventProtocolProvider.AddEvent(craft.playerId, EventTag, payload);
            });
        }

        [MessagePackObject]
        public class CraftCompletedEventMessagePack
        {
            [Key(0)] public int PlayerId { get; set; }
            [Key(1)] public string CraftRecipeGuidStr { get; set; }

            [IgnoreMember] public Guid CraftRecipeGuid => Guid.Parse(CraftRecipeGuidStr);

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public CraftCompletedEventMessagePack() { }

            public CraftCompletedEventMessagePack(int playerId, Guid craftRecipeGuid)
            {
                PlayerId = playerId;
                CraftRecipeGuidStr = craftRecipeGuid.ToString();
            }
        }
    }
}
