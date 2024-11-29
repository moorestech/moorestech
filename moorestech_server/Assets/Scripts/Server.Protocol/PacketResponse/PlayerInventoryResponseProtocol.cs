using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class PlayerInventoryResponseProtocol : IPacketResponse
    {
        public const string Tag = "va:playerInvRequest";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public PlayerInventoryResponseProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(payload.ToArray());
            
            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            
            //メインインベントリのアイテムを設定
            var mainItems = new List<ItemMessagePack>();
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var id = playerInventory.MainOpenableInventory.GetItem(i).Id;
                var count = playerInventory.MainOpenableInventory.GetItem(i).Count;
                mainItems.Add(new ItemMessagePack(id, count));
            }
            
            //グラブインベントリのアイテムを設定
            var grabItem = new ItemMessagePack(
                playerInventory.GrabInventory.GetItem(0).Id,
                playerInventory.GrabInventory.GetItem(0).Count);
            
            
            return new PlayerInventoryResponseProtocolMessagePack(data.PlayerId, mainItems.ToArray(), grabItem);
        }
    }
    
    
    [MessagePackObject]
    public class RequestPlayerInventoryProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestPlayerInventoryProtocolMessagePack()
        {
        }
        
        public RequestPlayerInventoryProtocolMessagePack(int playerId)
        {
            Tag = PlayerInventoryResponseProtocol.Tag;
            PlayerId = playerId;
        }
        
        [Key(2)] public int PlayerId { get; set; }
    }
    
    
    [MessagePackObject]
    public class PlayerInventoryResponseProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PlayerInventoryResponseProtocolMessagePack()
        {
        }
        
        
        public PlayerInventoryResponseProtocolMessagePack(int playerId, ItemMessagePack[] main, ItemMessagePack grab)
        {
            Tag = PlayerInventoryResponseProtocol.Tag;
            PlayerId = playerId;
            Main = main;
            Grab = grab;
        }
        
        [Key(2)] public int PlayerId { get; set; }
        
        [Key(3)] public ItemMessagePack[] Main { get; set; }
        
        [Key(4)] public ItemMessagePack Grab { get; set; }
    }
}