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
    public class ItemStackLevelUnlockEventPacket : IEventReceiver
    {
        public const string EventTag = "va:event:itemStackLevelUnlock";

        public ItemStackLevelUnlockEventPacket(EventProtocolProvider eventProtocolProvider, IItemStackLevelLookup itemStackLevelLookup)
        {
            // スタックレベル解放を購読し全プレイヤーへ配信
            // Subscribe to stack level unlocks and broadcast them to all players
            itemStackLevelLookup.OnStackLevelUnlocked.Subscribe(data =>
            {
                var payload = MessagePackSerializer.Serialize(new ItemStackLevelMessagePack(data.itemGuid, data.level));
                eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
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
