using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item;
using Core.Item.Config;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;

namespace Game.Crafting
{
    /// <summary>
    ///     
    /// </summary>
    public class IsCreatableJudgementService : IIsCreatableJudgementService
    {
        private readonly Dictionary<string, CraftingConfigData> _craftingConfigDataCache = new();
        private readonly IItemConfig _itemConfig;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly CraftingConfigData _nullCraftingConfigData;

        public IsCreatableJudgementService(ICraftingConfig craftingConfig, ItemStackFactory itemStackFactory, IItemConfig itemConfig)
        {
            _itemStackFactory = itemStackFactory;
            _itemConfig = itemConfig;

            //_craftingConfigDataCache
            foreach (var c in craftingConfig.GetCraftingConfigList())
            {
                var cashKey = GetCraftingConfigCacheKey(c.CraftItems);
                if (_craftingConfigDataCache.ContainsKey(cashKey))
                {
                    var resultItemModId = _itemConfig.GetItemConfig(c.Result.ItemHash).ModId;
                    var resultItemName = _itemConfig.GetItemConfig(c.Result.ItemHash).Name;
                    var existItemModId = _itemConfig.GetItemConfig(_craftingConfigDataCache[cashKey].Result.Id).ModId;
                    var existItemName = _itemConfig.GetItemConfig(_craftingConfigDataCache[cashKey].Result.Id).Name;

                    //TODO Mod
                    Console.WriteLine("。。");
                    Console.WriteLine($" ModId:{resultItemModId} Name:{resultItemName}  ModId:{existItemModId} Name:{existItemName}");
                    throw new ArgumentException();
                }

                _craftingConfigDataCache.Add(cashKey, c);
            }

            
            var nullItem = new List<CraftingItemData>();
            for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++) nullItem.Add(new CraftingItemData(itemStackFactory.CreatEmpty(), false));
            _nullCraftingConfigData = new CraftingConfigData(nullItem, itemStackFactory.CreatEmpty());
        }


        ///     

        /// <param name="craftingItems"></param>
        /// <returns></returns>
        public bool IsCreatable(IReadOnlyList<IItemStack> craftingItems)
        {
            //ID
            var key = GetCraftingConfigCacheKey(craftingItems);
            if (!_craftingConfigDataCache.ContainsKey(key)) return false;

            
            var craftingConfigData = _craftingConfigDataCache[key];
            for (var i = 0; i < craftingItems.Count; i++)
                if (craftingItems[i].Count < craftingConfigData.CraftItemInfos[i].ItemStack.Count)
                    return false;

            return true;
        }


        ///     
        ///     

        /// <param name="craftingItems"></param>
        /// <returns></returns>
        public IItemStack GetResult(IReadOnlyList<IItemStack> craftingItems)
        {
            var key = GetCraftingConfigCacheKey(craftingItems);
            if (_craftingConfigDataCache.ContainsKey(key)) return _craftingConfigDataCache[key].Result;

            throw new Exception("。IsCreatable。");
        }

        public CraftingConfigData GetCraftingConfigData(IReadOnlyList<IItemStack> craftingItems)
        {
            var key = GetCraftingConfigCacheKey(craftingItems);
            if (_craftingConfigDataCache.ContainsKey(key)) return _craftingConfigDataCache[key];

            return _nullCraftingConfigData;
        }



        ///     

        /// <param name="craftingItems"></param>
        /// <param name="mainInventoryItems"></param>
        /// <returns></returns>
        public int CalcAllCraftItemNum(IReadOnlyList<IItemStack> craftingItems, IReadOnlyList<IItemStack> mainInventoryItems)
        {
            
            if (!IsCreatable(craftingItems)) return 0;
            var resultItem = GetResult(craftingItems);
            
            var maxCraftItemNum = CalcMaxCraftNum(craftingItems);

            return MainInventoryCanInsertNum(maxCraftItemNum, resultItem, mainInventoryItems);
        }


        ///     1

        /// <param name="craftingItems"></param>
        /// <param name="mainInventoryItems"></param>
        /// <returns></returns>
        public int CalcOneStackCraftItemNum(IReadOnlyList<IItemStack> craftingItems, IReadOnlyList<IItemStack> mainInventoryItems)
        {
            
            if (!IsCreatable(craftingItems)) return 0;

            var resultItem = GetResult(craftingItems);

            //1
            var oneStackMaxCraftNum = _itemConfig.GetItemConfig(resultItem.Id).MaxStack / resultItem.Count;

            
            var maxCraftItemNum = CalcMaxCraftNum(craftingItems);
            
            if (maxCraftItemNum < oneStackMaxCraftNum) oneStackMaxCraftNum = maxCraftItemNum;
            return MainInventoryCanInsertNum(oneStackMaxCraftNum, resultItem, mainInventoryItems);
            ;
        }

        private int MainInventoryCanInsertNum(int maxNum, IItemStack insertItem, IReadOnlyList<IItemStack> mainInventoryItems)
        {
            
            var tempMainInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, _itemStackFactory, PlayerInventoryConst.MainInventorySize);
            
            for (var i = 0; i < mainInventoryItems.Count; i++) tempMainInventory.SetItem(i, mainInventoryItems[i]);


            var creatableCount = 0;
            //insert
            for (var i = 0; i < maxNum; i++)
            {
                var reminderItem = tempMainInventory.InsertItem(insertItem);
                //0＝
                if (reminderItem.Count != 0) return creatableCount;
                creatableCount++;
            }

            return creatableCount;
        }

        private int CalcMaxCraftNum(IReadOnlyList<IItemStack> craftingItems)
        {
            var config = GetCraftingConfigData(craftingItems);
            var craftCount = 0;

            for (var i = 0; i < config.CraftItemInfos.Count; i++)
            {
                if (config.CraftItemInfos[i].ItemStack.Count == 0) continue;

                var count = craftingItems[i].Count / config.CraftItemInfos[i].ItemStack.Count;

                if (craftCount < count) craftCount = count;
            }

            return craftCount;
        }

        private string GetCraftingConfigCacheKey(IReadOnlyList<IItemStack> items)
        {
            return items.Aggregate("", (current, i) => current + "_" + i.Id);
        }
    }
}