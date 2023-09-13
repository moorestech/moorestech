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
    /// クラフト可能かどうかをチェックするためのサービスクラスです
    /// </summary>
    public class IsCreatableJudgementService : IIsCreatableJudgementService
    {
        private readonly ItemStackFactory _itemStackFactory;
        private readonly Dictionary<string, CraftingConfigData> _craftingConfigDataCache = new();
        private readonly CraftingConfigData _nullCraftingConfigData;
        private readonly IItemConfig _itemConfig;

        public IsCreatableJudgementService(ICraftingConfig craftingConfig, ItemStackFactory itemStackFactory, IItemConfig itemConfig)
        {
            _itemStackFactory = itemStackFactory;
            _itemConfig = itemConfig;

            //_craftingConfigDataCacheの作成
            foreach (var c in craftingConfig.GetCraftingConfigList())
            {
                var cashKey = GetCraftingConfigCacheKey(c.CraftItems);
                if (_craftingConfigDataCache.ContainsKey(cashKey))
                {
                    var resultItemModId = _itemConfig.GetItemConfig(c.Result.ItemHash).ModId;
                    var resultItemName = _itemConfig.GetItemConfig(c.Result.ItemHash).Name;
                    var existItemModId = _itemConfig.GetItemConfig(_craftingConfigDataCache[cashKey].Result.Id).ModId;
                    var existItemName = _itemConfig.GetItemConfig(_craftingConfigDataCache[cashKey].Result.Id).Name;
                    
                    //TODO Modパースエラーのログを出す
                    Console.WriteLine("クラフトレシピのキャッシュの構築中に失敗しました。クラフトレシピに重複があります。");
                    Console.WriteLine($"ロードしようとしたレシピの結果 ModId:{resultItemModId} Name:{resultItemName} 重複したレシピの結果 ModId:{existItemModId} Name:{existItemName}");
                    throw new ArgumentException();
                }
                else
                {
                    _craftingConfigDataCache.Add(cashKey, c);
                }
            }
            
            //レシピがない時のデータの作成
            var nullItem = new List<CraftingItemData>();
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                nullItem.Add(new CraftingItemData(itemStackFactory.CreatEmpty(),false));
            }
            _nullCraftingConfigData = new CraftingConfigData(nullItem, itemStackFactory.CreatEmpty());
        }

        /// <summary>
        /// クラフトスロットの配置がクラフト可能かどうかをチェックする
        /// </summary>
        /// <param name="craftingItems">クラフトスロット</param>
        /// <returns>クラフト可能か</returns>
        public bool IsCreatable(IReadOnlyList<IItemStack> craftingItems)
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
                if (craftingItems[i].Count < craftingConfigData.CraftItemInfos[i].ItemStack.Count)
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
        public IItemStack GetResult(IReadOnlyList<IItemStack> craftingItems)
        {
            var key = GetCraftingConfigCacheKey(craftingItems);
            if (_craftingConfigDataCache.ContainsKey(key))
            {
                return _craftingConfigDataCache[key].Result;
            }

            throw new Exception("クラフト可能なアイテムがありません。この関数を使用する前にIsCreatableを使用してください。");
        }

        public CraftingConfigData GetCraftingConfigData(IReadOnlyList<IItemStack> craftingItems)
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
        public int CalcAllCraftItemNum(IReadOnlyList<IItemStack> craftingItems,IReadOnlyList<IItemStack> mainInventoryItems)
        {
            //クラフト不可能ならそのまま終了
            if (!IsCreatable(craftingItems)) return 0;
            var resultItem = GetResult(craftingItems);
            //最大のクラフト回数を求める
            var maxCraftItemNum = CalcMaxCraftNum(craftingItems);

            return MainInventoryCanInsertNum(maxCraftItemNum,resultItem,mainInventoryItems);
        }

        /// <summary>
        /// 1スタッククラフトするときのクラフト可能な個数を返す
        /// </summary>
        /// <param name="craftingItems">クラフトスロットにおいてあるアイテム</param>
        /// <param name="mainInventoryItems">クラフト結果を入れるメインインベントリ</param>
        /// <returns>クラフト可能な個数</returns>
        public int CalcOneStackCraftItemNum(IReadOnlyList<IItemStack> craftingItems, IReadOnlyList<IItemStack> mainInventoryItems)
        {
            //クラフト不可能ならそのまま終了
            if (!IsCreatable(craftingItems))return 0;
            
            var resultItem = GetResult(craftingItems);
            
            //1スタックの最大クラフト数の取得
            var oneStackMaxCraftNum = _itemConfig.GetItemConfig(resultItem.Id).MaxStack / resultItem.Count;
            
            //最大のクラフト回数を求める
            var maxCraftItemNum = CalcMaxCraftNum(craftingItems);
            //アイテムが足りないなどの理由で最大個数に到達しない場合は最大クラフト回数を使用する
            if (maxCraftItemNum < oneStackMaxCraftNum)
            {
                oneStackMaxCraftNum = maxCraftItemNum;
            }
            return MainInventoryCanInsertNum(oneStackMaxCraftNum,resultItem,mainInventoryItems);;
        }

        private int MainInventoryCanInsertNum(int maxNum,IItemStack insertItem,IReadOnlyList<IItemStack> mainInventoryItems)
        {
            //クラフト可能な個数を求めるために仮のインベントリを作成する
            var tempMainInventory = new OpenableInventoryItemDataStoreService((_,_) => {},_itemStackFactory,PlayerInventoryConst.MainInventorySize);
            //仮インベントリにアイテムをセットする
            for (int i = 0; i < mainInventoryItems.Count; i++) { tempMainInventory.SetItem(i, mainInventoryItems[i]); }


            var creatableCount = 0;
            //アイテムをinsertしてアイテムが入るかどうかをチェックする
            for (int i = 0; i < maxNum; i++)
            {
                var reminderItem = tempMainInventory.InsertItem(insertItem);
                //個数が0ではない＝アイテムがいっぱいだったのでその時点での個数を出力する
                if (reminderItem.Count != 0)
                {
                    return creatableCount;
                }
                creatableCount++;
            }

            return creatableCount;
        }

        private int CalcMaxCraftNum(IReadOnlyList<IItemStack> craftingItems)
        {
            var config = GetCraftingConfigData(craftingItems);
            var craftCount = 0;

            for (int i = 0; i < config.CraftItemInfos.Count; i++)
            {
                if (config.CraftItemInfos[i].ItemStack.Count == 0) continue;
                
                var count = craftingItems[i].Count / config.CraftItemInfos[i].ItemStack.Count;

                if (craftCount < count)
                {
                    craftCount = count;
                }
            }

            return craftCount;
        }

        private string GetCraftingConfigCacheKey(IReadOnlyList<IItemStack> items)
        {
            return items.Aggregate("", (current, i) => current + "_" + i.Id);
        }
    }
}