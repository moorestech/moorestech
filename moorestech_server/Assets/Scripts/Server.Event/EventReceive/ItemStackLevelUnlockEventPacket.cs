using System;
using Core.Item.Interface;
using Game.Context;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// アイテムスタックレベルの解放をクライアントに通知するイベントパケット
    /// </summary>
    public class ItemStackLevelUnlockEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:itemStackLevelUnlock";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IItemStackLevelLookup _itemStackLevelLookup;

        public ItemStackLevelUnlockEventPacket(EventProtocolProvider eventProtocolProvider, IItemStackLevelLookup itemStackLevelLookup)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _itemStackLevelLookup = itemStackLevelLookup;
        }

        public void Load()
        {
            // スタックレベル解放を購読し全プレイヤーへ配信
            // Subscribe to stack level unlocks and broadcast them to all players
            _itemStackLevelLookup.OnStackLevelUnlocked.Subscribe(data =>
            {
                var payload = MessagePackSerializer.Serialize(new ItemStackLevelMessagePack(data.itemGuid, data.level));
                _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
            });
        }

        [MessagePackObject]
        public class ItemStackLevelMessagePack
        {
            [Key(0)] public Guid ItemGuid { get; set; }
            [Key(1)] public int Level { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ItemStackLevelMessagePack() { }

            public ItemStackLevelMessagePack(Guid itemGuid, int level)
            {
                ItemGuid = itemGuid;
                Level = level;
            }
        }
    }
}
