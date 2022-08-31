using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class SetRecipeCraftingInventoryProtocol: IPacketResponse
    {
        public const string Tag = "va:setRecipeCraftingInventory";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public SetRecipeCraftingInventoryProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SetRecipeCraftingInventoryProtocolMessagePack>(payload.ToArray());

            var mainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
            var craftingInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).CraftingOpenableInventory;
            var grabInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).GrabInventory;



            return new List<List<byte>>();
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class SetRecipeCraftingInventoryProtocolMessagePack : ProtocolMessagePackBase
    {
        public SetRecipeCraftingInventoryProtocolMessagePack(int playerId,ItemMessagePack[] recipe)
        {
            Tag = SetRecipeCraftingInventoryProtocol.Tag;
            Recipe = recipe;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SetRecipeCraftingInventoryProtocolMessagePack() { }

        public ItemMessagePack[] Recipe { get; set; }
        public int PlayerId { get; set; }

    }
}