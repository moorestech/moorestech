using System.Collections.Generic;
using Core.Item;
using Game.Craft.Interface;
using Game.PlayerInventory.Interface;

namespace Game.Craft
{
    public class IsCreatableJudgementService : IIsCreatableJudgementService
    {
        private readonly ItemStackFactory _itemStackFactory;
        private readonly Dictionary<string, CraftingConfigData> _craftingConfigDataCache = new();
        private readonly CraftingConfigData _nullCraftingConfigData;

        public IsCreatableJudgementService(ICraftingConfig craftingConfig, ItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
            
            //_craftingConfigDataCacheの作成
            foreach (var c in craftingConfig.GetCraftingConfigList())
            {
                _craftingConfigDataCache.Add(GetCraftingConfigCacheKey(c.Items),c);
            }
            
            //レシピがない時のデータの作成
            var nullItem = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.CraftSlotSize; i++)
            {
                nullItem.Add(itemStackFactory.CreatEmpty());
            }
            _nullCraftingConfigData = new CraftingConfigData(nullItem, itemStackFactory.CreatEmpty());
        }

        public bool IsCreatable(List<IItemStack> craftingItems)
        {
            //アイテムIDが足りているかをチェックする
            var key = GetCraftingConfigCacheKey(craftingItems);
            if (!_craftingConfigDataCache.ContainsKey(key))
            {
                return false;
            }
            
            //アイテム数が足りているかチェック
            var craftingConfigData = _craftingConfigDataCache[key];
            for (int i = 0; i < craftingItems.Count; i++)
            {
                if (craftingItems[i].Count < craftingConfigData.Items[i].Count)
                {
                    return false;
                }
            }

            return true;
        }

        public IItemStack GetResult(List<IItemStack> craftingItems)
        {
            var key = GetCraftingConfigCacheKey(craftingItems);
            if (_craftingConfigDataCache.ContainsKey(key))
            {
                return _craftingConfigDataCache[key].Result;
            }

            return _itemStackFactory.CreatEmpty();
        }

        public CraftingConfigData GetCraftingConfigData(List<IItemStack> craftingItems)
        {
            var key = GetCraftingConfigCacheKey(craftingItems);
            if (_craftingConfigDataCache.ContainsKey(key))
            {
                return _craftingConfigDataCache[key];
            }

            return _nullCraftingConfigData;
        }
        
        private string GetCraftingConfigCacheKey(List<IItemStack> itemId)
        {
            var items = "";
            itemId.ForEach(i => { items = items + "_" + i.Id; });
            return items;
        }
    }
}