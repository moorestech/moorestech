using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.CraftRecipesModule;
using Mooresmaster.Model.MachineRecipesModule;
using UnityEngine;

namespace Game.CraftChainer
{
    public struct CraftChainItem : IEquatable<CraftChainItem>
    {
        public ItemId ItemId { get; }
        public int Count { get; }
        
        public CraftChainItem(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
        
        public bool Equals(CraftChainItem other)
        {
            return ItemId.Equals(other.ItemId) && Count == other.Count;
        }
        public override bool Equals(object obj)
        {
            return obj is CraftChainItem other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(ItemId, Count);
        }
    }
    
    public static class CraftChainItemUtil
    {
        public static List<CraftChainItem> ToResultCraftChainItems(this CraftRecipeMasterElement craftRecipeMaster, CraftChainItem targetCraftChainItem)
        {
            var requireCraftCount = GetRequireCraftCount(craftRecipeMaster, targetCraftChainItem);
            
            var resultItemId = MasterHolder.ItemMaster.GetItemId(craftRecipeMaster.CraftResultItemGuid);
            var resultItemCount = craftRecipeMaster.CraftResultCount * requireCraftCount;
            return new List<CraftChainItem> { new(resultItemId, resultItemCount) };
        }
        
        public static List<CraftChainItem> ToRequireCraftChainItems(this CraftRecipeMasterElement craftRecipeMaster, CraftChainItem targetCraftChainItem)
        {
            var requireCraftCount = GetRequireCraftCount(craftRecipeMaster, targetCraftChainItem);
            
            var requireCraftItems = new List<CraftChainItem>();
            foreach (var requiredItem in craftRecipeMaster.RequiredItems)
            {
                var requiredItemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var requiredItemCount = requiredItem.Count * requireCraftCount;
                
                requireCraftItems.Add(new CraftChainItem(requiredItemId, requiredItemCount));
            }
            
            return requireCraftItems;
        }
        
        private static int GetRequireCraftCount(CraftRecipeMasterElement craftRecipeMaster,CraftChainItem targetCraftChainItem)
        {
            return Mathf.CeilToInt(craftRecipeMaster.CraftResultCount / (float)targetCraftChainItem.Count);
        }
        
        public static List<CraftChainItem> ToResultMachineChainItems(this MachineRecipeMasterElement recipeMasterElement, CraftChainItem targetCraftChainItem)
        {
            var requireCount = GetRequireMachineCraftCount(recipeMasterElement, targetCraftChainItem);
            
            var result = new List<CraftChainItem>();
            foreach (var outputItem in recipeMasterElement.OutputItems)
            {
                var outputItemId = MasterHolder.ItemMaster.GetItemId(outputItem.ItemGuid);
                var outputItemCount = outputItem.Count * requireCount;
                
                result.Add(new CraftChainItem(outputItemId, outputItemCount));
            }
            
            return result;
        }
        
        public static List<CraftChainItem> ToRequireMachineChainItems(this MachineRecipeMasterElement recipeMasterElement, CraftChainItem targetCraftChainItem)
        {
            var requireCount = GetRequireMachineCraftCount(recipeMasterElement, targetCraftChainItem);
            
            var result = new List<CraftChainItem>();
            foreach (var requiredItem in recipeMasterElement.InputItems)
            {
                var requiredItemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var requiredItemCount = requiredItem.Count * requireCount;
                
                result.Add(new CraftChainItem(requiredItemId, requiredItemCount));
            }
            
            return result;
        }
        
        private static int GetRequireMachineCraftCount(MachineRecipeMasterElement recipeMasterElement, CraftChainItem targetCraftChainItem)
        {
            var outputCount = -1;
            
            // アウトプットアイテムの出力個数を取得
            foreach (var outputItem in recipeMasterElement.OutputItems)
            {
                var outputItemId = MasterHolder.ItemMaster.GetItemId(outputItem.ItemGuid);
                if (targetCraftChainItem.ItemId == outputItemId)
                {
                    outputCount = outputItem.Count;
                    break;
                }
            }
            
            if (outputCount == -1)
            {
                var itemGuid = MasterHolder.ItemMaster.GetItemMaster(targetCraftChainItem.ItemId);
                throw new Exception($"指定しているアイテムをアウトプットしない機械レシピでした。レシピGUID：{recipeMasterElement.MachineRecipeGuid} アイテムGUID：{itemGuid}");
            }
            
            return outputCount / targetCraftChainItem.Count;
        }
    }
}