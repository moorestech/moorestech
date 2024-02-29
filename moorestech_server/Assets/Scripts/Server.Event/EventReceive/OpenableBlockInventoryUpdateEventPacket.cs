using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Game.Block.Interface.Event;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// TODO これいる？どうせステートの変更を送るんだから、そこに入れたらいいんじゃないの？
    /// </summary>
    public class OpenableBlockInventoryUpdateEventPacket
    {
        public const string EventTag = "va:event:blockInvUpdate";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IBlockInventoryOpenStateDataStore _inventoryOpenStateDataStore;
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        private DateTime _now = DateTime.Now;

        public OpenableBlockInventoryUpdateEventPacket(
            EventProtocolProvider eventProtocolProvider, IBlockInventoryOpenStateDataStore inventoryOpenStateDataStore,
            IBlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, IWorldBlockDatastore worldBlockDatastore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _inventoryOpenStateDataStore = inventoryOpenStateDataStore;
            _worldBlockDatastore = worldBlockDatastore;
            blockInventoryUpdateEvent.Subscribe(InventoryUpdateEvent);
        }


        private void InventoryUpdateEvent(BlockOpenableInventoryUpdateEventProperties properties)
        {
            //そのブロックを開いているプレイヤーをリストアップ
            var playerIds = _inventoryOpenStateDataStore.GetBlockInventoryOpenPlayers(properties.EntityId);
            if (playerIds.Count == 0) return;

            var (x, y) = _worldBlockDatastore.GetBlockPosition(properties.EntityId);
            var messagePack = new OpenableBlockInventoryUpdateEventMessagePack(x, y, properties.Slot, properties.ItemStack);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            //プレイヤーごとにイベントを送信
            foreach (var id in playerIds)
            {
                _eventProtocolProvider.AddEvent(id,EventTag, payload);
            }
        }
    }


    [MessagePackObject]
    public class OpenableBlockInventoryUpdateEventMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public OpenableBlockInventoryUpdateEventMessagePack()
        {
        }

        public OpenableBlockInventoryUpdateEventMessagePack(int x, int y, int slot, IItemStack item)
        {
            X = x;
            Y = y;
            Slot = slot;
            Item = new ItemMessagePack(item.Id, item.Count);
        }

        [Key(0)]
        public int X { get; set; }
        [Key(1)]
        public int Y { get; set; }
        [Key(2)]
        public int Slot { get; set; }
        [Key(3)]
        public ItemMessagePack Item { get; set; }
    }
}