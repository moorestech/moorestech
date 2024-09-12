using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.CraftRecipesModule;

namespace Server.Protocol.PacketResponse
{
    public class OneClickCraft : IPacketResponse
    {
        public const string Tag = "va:oneClickCraft";
        private readonly CraftEvent _craftEvent;
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public OneClickCraft(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _craftEvent = serviceProvider.GetService<CraftEvent>();
        }
        
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestOneClickCraftProtocolMessagePack>(payload.ToArray());
            
            var craftConfig = MasterHolder.CraftRecipeMaster.GetCraftRecipe(data.CraftRecipeGuid);
            //プレイヤーインベントリを取得
            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            var mainInventory = playerInventory.MainOpenableInventory;
            
            //クラフト可能かどうかを確認
            if (!IsCraftable(mainInventory, craftConfig))
                //クラフト不可能な場合は何もしない
                return null;
            
            //クラフト可能な場合はクラフトを実行
            
            //クラフトに必要なアイテムを消費
            SubItem(mainInventory, craftConfig);
            //クラフト結果をプレイヤーインベントリに追加
            var resultItem = ServerContext.ItemStackFactory.Create(craftConfig.ResultItem.ItemGuid, craftConfig.ResultItem.Count);
            playerInventory.MainOpenableInventory.InsertItem(resultItem);
            
            _craftEvent.InvokeCraftItem(craftConfig);
            
            return null;
        }
        
        private static bool IsCraftable(IOpenableInventory mainInventory, CraftRecipeMasterElement recipe)
        {
            //クラフト結果のアイテムをインサートできるかどうかをチェックする
            var resultItem = ServerContext.ItemStackFactory.Create(recipe.ResultItem.ItemGuid, recipe.ResultItem.Count);
            var resultItemList = new List<IItemStack> { resultItem };
            if (!mainInventory.InsertionCheck(resultItemList))
                return false;
            
            //クラフトに必要なアイテムを収集する
            //key itemId value count
            var requiredItems = new Dictionary<ItemId, int>();
            foreach (var requiredItem in recipe.RequiredItems)
            {
                var requiredItemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                
                if (requiredItems.ContainsKey(requiredItemId))
                {
                    requiredItems[requiredItemId] += requiredItem.Count;
                }
                else
                {
                    requiredItems.Add(requiredItemId, requiredItem.Count);
                }
            }
            
            //クラフトに必要なアイテムを持っているか確認する
            var checkResult = new Dictionary<ItemId, int>();
            foreach (var itemStack in mainInventory.InventoryItems)
            {
                if (!requiredItems.ContainsKey(itemStack.Id)) continue;
                
                if (checkResult.ContainsKey(itemStack.Id))
                    checkResult[itemStack.Id] += itemStack.Count;
                else
                    checkResult[itemStack.Id] = itemStack.Count;
            }
            
            //必要なアイテムを持っていない場合はクラフトできない
            foreach (var requiredItem in requiredItems)
            {
                if (!checkResult.ContainsKey(requiredItem.Key)) return false;
                if (checkResult[requiredItem.Key] < requiredItem.Value) return false;
            }
            
            
            return true;
        }
        
        
        /// <summary>
        ///     クラフトしてアイテムを消費する
        /// </summary>
        private static void SubItem(IOpenableInventory mainInventory, CraftRecipeMasterElement recipe)
        {
            //クラフトに必要なアイテムを収集する
            //key itemId value count
            var requiredItems = new Dictionary<ItemId, int>();
            foreach (var requiredItem in recipe.RequiredItems)
            {
                var requiredItemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                
                if (requiredItems.ContainsKey(requiredItemId))
                    requiredItems[requiredItemId] += requiredItems.Count;
                else
                    requiredItems.Add(requiredItemId, requiredItems.Count);
            }
            
            //クラフトのために消費する
            for (var i = 0; i < mainInventory.InventoryItems.Count; i++)
            {
                var inventoryItem = mainInventory.InventoryItems[i];
                if (!requiredItems.TryGetValue(inventoryItem.Id, out var subCount)) continue;
                
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
        [Key(2)] public int PlayerId { get; set; }
        
        [Key(3)] public string CraftRecipeGuidStr { get; set; }
        [IgnoreMember] public Guid CraftRecipeGuid => Guid.Parse(CraftRecipeGuidStr);
        
        public RequestOneClickCraftProtocolMessagePack(int playerId, Guid craftRecipeGuid)
        {
            Tag = OneClickCraft.Tag;
            PlayerId = playerId;
            CraftRecipeGuidStr = craftRecipeGuid.ToString();
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestOneClickCraftProtocolMessagePack() { }
    }
}