using System;
using Core.Item.Interface;
using Game.Block.Interface.Event;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     TODO これいる？どうせステートの変更を送るんだから、そこに入れたらいいんじゃないの？
    /// </summary>
    public class OpenableBlockInventoryUpdateEventPacket
    {
        public const string EventTag = "va:event:blockInvUpdate";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IBlockInventoryOpenStateDataStore _inventoryOpenStateDataStore;
        
        private DateTime _now = DateTime.Now;
        
        public OpenableBlockInventoryUpdateEventPacket(EventProtocolProvider eventProtocolProvider, IBlockInventoryOpenStateDataStore inventoryOpenStateDataStore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _inventoryOpenStateDataStore = inventoryOpenStateDataStore;
            ServerContext.BlockOpenableInventoryUpdateEvent.Subscribe(InventoryUpdateEvent);
        }
        
        
        private void InventoryUpdateEvent(BlockOpenableInventoryUpdateEventProperties properties)
        {
            //そのブロックを開いているプレイヤーをリストアップ
            var playerIds = _inventoryOpenStateDataStore.GetBlockInventoryOpenPlayers(properties.BlockInstanceId);
            if (playerIds.Count == 0) return;
            
            var pos = ServerContext.WorldBlockDatastore.GetBlockPosition(properties.BlockInstanceId);
            var messagePack = new OpenableBlockInventoryUpdateEventMessagePack(pos, properties.Slot, properties.ItemStack);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            //プレイヤーごとにイベントを送信
            foreach (var id in playerIds) _eventProtocolProvider.AddEvent(id, EventTag, payload);
        }
    }
    
    
    [MessagePackObject]
    public class OpenableBlockInventoryUpdateEventMessagePack
    {
        [Key(0)] public Vector3IntMessagePack Position { get; set; }
        [Key(1)] public int Slot { get; set; }
        [Key(2)] public ItemMessagePack Item { get; set; }
        
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public OpenableBlockInventoryUpdateEventMessagePack() { }
        
        public OpenableBlockInventoryUpdateEventMessagePack(Vector3Int pos, int slot, IItemStack item)
        {
            Position = new Vector3IntMessagePack(pos);
            Slot = slot;
            Item = new ItemMessagePack(item.Id, item.Count);
        }
    }
}