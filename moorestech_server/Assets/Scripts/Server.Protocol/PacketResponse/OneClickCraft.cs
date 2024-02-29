using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Game.Crafting.Interface;

namespace Server.Protocol.PacketResponse
{
    public class OneClickCraft: IPacketResponse
    {
        public const string Tag = "va:oneClickCraft";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore; 
        private readonly ICraftingConfig _craftingConfig;

        public OneClickCraft(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _craftingConfig = serviceProvider.GetService<ICraftingConfig>();
        }
        
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestOneClickCraftProtocolMessagePack>(payload.ToArray());
            
            var craftConfig = _craftingConfig.GetCraftingConfigData(data.CraftRecipeId);
            //プレイヤーインベントリを取得
            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            var mainInventory = playerInventory.MainOpenableInventory;
            var grabInventory = playerInventory.GrabInventory;

            //クラフト可能かどうかを確認
            if (!IsCraftable(mainInventory,grabInventory,craftConfig))
            {
                //クラフト不可能な場合は何もしない
                return null;
            }
            
            //クラフト可能な場合はクラフトを実行
            
            //クラフトに必要なアイテムを消費
            SubItem(mainInventory,craftConfig);
            //クラフト結果をプレイヤーインベントリに追加
            playerInventory.MainOpenableInventory.InsertItem(craftConfig.ResultItem);
            
            
            return null;
        }
        
        private static bool IsCraftable(IOpenableInventory mainInventory,IOpenableInventory grabInventory, CraftingConfigData craftingConfigData)
        {
            //クラフト結果のアイテムをインサートできるかどうかをチェックする
            if (!mainInventory.InsertionCheck(new List<IItemStack>() { craftingConfigData.ResultItem }))
            {
                return false;
            }
            
            
            //クラフトに必要なアイテムを収集する
            //key itemId value count
            var requiredItems = new Dictionary<int,int>();
            foreach (var itemData in craftingConfigData.CraftItemInfos)
            {
                if (requiredItems.ContainsKey(itemData.ItemStack.Id))
                {
                    requiredItems[itemData.ItemStack.Id] += itemData.ItemStack.Count;
                }
                else
                {
                    requiredItems.Add(itemData.ItemStack.Id, itemData.ItemStack.Count);
                }
            }
            
            //クラフトに必要なアイテムを持っているか確認する
            var checkResult = new Dictionary<int,int>();
            foreach (var itemStack in mainInventory.Items)
            {
                if (!requiredItems.ContainsKey(itemStack.Id)) continue;
                
                if (checkResult.ContainsKey(itemStack.Id))
                {
                    checkResult[itemStack.Id] += itemStack.Count;
                }
                else
                {
                    checkResult[itemStack.Id] = itemStack.Count;
                }
            }
            
            //必要なアイテムを持っていない場合はクラフトできない
            foreach (var requiredItem in requiredItems)
            {
                if (!checkResult.ContainsKey(requiredItem.Key))
                {
                    return false;
                }
                if (checkResult[requiredItem.Key] < requiredItem.Value)
                {
                    return false;
                }
            }


            return true;
        }


        /// <summary>
        /// クラフトしてアイテムを消費する
        /// </summary>
        private static void SubItem(IOpenableInventory mainInventory, CraftingConfigData craftingConfigData)
        {
            //クラフトに必要なアイテムを収集する
            //key itemId value count
            var requiredItems = new Dictionary<int,int>();
            foreach (var itemData in craftingConfigData.CraftItemInfos)
            {
                if (requiredItems.ContainsKey(itemData.ItemStack.Id))
                {
                    requiredItems[itemData.ItemStack.Id] += itemData.ItemStack.Count;
                }
                else
                {
                    requiredItems.Add(itemData.ItemStack.Id, itemData.ItemStack.Count);
                }
            }
            
            //クラフトのために消費する
            for (var i = 0; i < mainInventory.Items.Count; i++)
            {
                var inventoryItem = mainInventory.Items[i];
                if (!requiredItems.ContainsKey(inventoryItem.Id)) continue;
                
                var subCount = requiredItems[inventoryItem.Id];
                if (inventoryItem.Count <= subCount)
                {
                    mainInventory.SetItem(i, inventoryItem.SubItem(inventoryItem.Count));
                    requiredItems[inventoryItem.Id] -= inventoryItem.Count;
                }
                else
                {
                    mainInventory.SetItem(i, inventoryItem.SubItem(subCount));
                    requiredItems[inventoryItem.Id] -= subCount;
                }
            }
        }
    }
    
    [MessagePackObject]
    public class RequestOneClickCraftProtocolMessagePack : ProtocolMessagePackBase
    {
        [Key(2)]
        public int PlayerId { get; set; }
        [Key(3)]
        public int CraftRecipeId { get; set; }
        
        public RequestOneClickCraftProtocolMessagePack(int playerId, int craftRecipeId)
        {
            Tag = OneClickCraft.Tag;
            PlayerId = playerId;
            CraftRecipeId = craftRecipeId;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestOneClickCraftProtocolMessagePack() { }
    }
}