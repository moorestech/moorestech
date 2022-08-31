using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util;
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
            
            
            //クラフトインベントリ、グラブインベントリのアイテムを全てメインインベントリに移動
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var itemCount = craftingInventory.GetItem(i).Count;
                InventoryItemInsertService.Insert(craftingInventory,i,mainInventory,itemCount);
            }
            var grabItemCount = grabInventory.GetItem(0).Count;
            InventoryItemInsertService.Insert(grabInventory,0,mainInventory,grabItemCount);
            
            
            //必要なアイテムがMainインベントリにあるかチェック
            var requiredItemCount = new Dictionary<int, int>();
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var itemId = craftingInventory.GetItem(i).Id;
                if (requiredItemCount.ContainsKey(itemId))
                {
                    requiredItemCount[itemId] += craftingInventory.GetItem(i).Count;
                }
                else
                {
                    requiredItemCount.Add(itemId, craftingInventory.GetItem(i).Count);
                }
            }
            var mainInventoryItemCount = new Dictionary<int, int>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var itemId = mainInventory.GetItem(i).Id;
                if (mainInventoryItemCount.ContainsKey(itemId))
                {
                    mainInventoryItemCount[itemId] += mainInventory.GetItem(i).Count;
                }
                else
                {
                    mainInventoryItemCount.Add(itemId, mainInventory.GetItem(i).Count);
                }
            }
            
            
            



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