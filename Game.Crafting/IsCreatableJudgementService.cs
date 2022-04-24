using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;

namespace Game.Crafting
{
    /// <summary>
    /// クラフト可能かどうかをチェックするためのサービスクラスです
    /// </summary>
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
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                nullItem.Add(itemStackFactory.CreatEmpty());
            }
            _nullCraftingConfigData = new CraftingConfigData(nullItem, itemStackFactory.CreatEmpty());
        }

        /// <summary>
        /// クラフトスロットの配置がクラフト可能かどうかをチェックする
        /// </summary>
        /// <param name="craftingItems">クラフトスロット</param>
        /// <returns>クラフト可能か</returns>
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

        /// <summary>
        /// クラフトスロットの配置のクラフト結果のアイテムを取得する
        /// ない時はからのアイテムがかえる
        /// </summary>
        /// <param name="craftingItems">クラフトスロット</param>
        /// <returns>結果のアイテム</returns>
        public IItemStack GetResult(List<IItemStack> craftingItems)
        {
            var key = GetCraftingConfigCacheKey(craftingItems);
            if (_craftingConfigDataCache.ContainsKey(key))
            {
                return _craftingConfigDataCache[key].Result;
            }

            throw new Exception("クラフト可能なアイテムがありません。この関数を使用する前にIsCreatableを使用してください。");
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


        
        /// <summary>
        /// 全てクラフトするときのクラフト可能な個数を返す
        /// </summary>
        /// <param name="craftingItems">クラフトスロットにおいてあるアイテム</param>
        /// <param name="mainInventoryItems">クラフト結果を入れるメインインベントリ</param>
        /// <returns>クラフト可能な個数</returns>
        public int CalcAllCraftItemNum(List<IItemStack> craftingItems,List<IItemStack> mainInventoryItems)
        {
            //クラフト不可能ならそのまま終了
            if (!IsCreatable(craftingItems))return 0;
            
            var resultItem = GetResult(craftingItems);
            //最大のクラフト回数を求める
            var maxCraftItemNum = CalcMaxCraftNum(craftingItems);
            
            //クラフト可能な個数を求めるために仮のインベントリを作成する
            var tempMainInventory = new OpenableInventoryItemDataStoreService((_,_) => {},_itemStackFactory,PlayerInventoryConst.MainInventorySize);
            //仮インベントリにアイテムをセットする
            for (int i = 0; i < mainInventoryItems.Count; i++) { tempMainInventory.SetItem(i, mainInventoryItems[i]); }


            var creatableCount = 0;
            //アイテムをinsertしてアイテムが入るかどうかをチェックする
            for (int i = 0; i < maxCraftItemNum; i++)
            {
                var reminderItem = tempMainInventory.InsertItem(resultItem);
                //個数が0ではない＝アイテムがいっぱいだったのでその時点での個数を出力する
                if (reminderItem.Count != 0)
                {
                    return creatableCount;
                }
                creatableCount++;
            }

            return creatableCount;
        }

        private int CalcMaxCraftNum(List<IItemStack> craftingItems)
        {
            var config = GetCraftingConfigData(craftingItems);
            var craftCount = 0;

            for (int i = 0; i < config.Items.Count; i++)
            {
                var count = craftingItems[i].Count / config.Items[i].Count;
                if (craftCount < count)
                {
                    craftCount = count;
                }
            }

            return craftCount;
        }

        private string GetCraftingConfigCacheKey(List<IItemStack> itemId)
        {
            var items = "";
            itemId.ForEach(i => { items = items + "_" + i.Id; });
            return items;
        }
    }
}