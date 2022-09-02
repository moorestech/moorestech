using System;
using System.Collections.Generic;
using Core.Const;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.RecipePlace
{
    public class CalcCraftInventoryPlaceItem
    {
        /// <summary>
        /// クラフトインベントリに実際に「どのスロットに何個アイテムを置くか」を計算するためのクラス
        /// メインインベントリにあるアイテムをできる限り集め、均等に分配する
        /// </summary>
        /// <param name="itemStackFactory">アイテム作成用</param>
        /// <param name="itemConfig">アイテム最大スタック数チェック用</param>
        /// <param name="recipe">実際におくレシピ</param>
        /// <param name="mainInventoryRequiredItemCount">メインインベントリに何個アイテムがあるかチェックする</param>
        /// <returns>実際にクラフトインベントリにおくアイテム</returns>
        public static IItemStack[] Calc(ItemStackFactory itemStackFactory,IItemConfig itemConfig,ItemMessagePack[] recipe,Dictionary<int,int> mainInventoryRequiredItemCount)
        {
            //そのアイテムIDが必要なスロットがいくつあるか求める
            var requiredItemSlotCount = CalcRequiredItemSlotCount(recipe);
            
            
            //そのスロットに入るアイテム数を計算する
            var craftInventoryPlaceItem = CraftInventoryPlaceItemWithoutReminder(recipe, itemStackFactory, itemConfig, mainInventoryRequiredItemCount, requiredItemSlotCount);
            
            
            //あまり分を足す
            //アイテムIDのループを回し、一番最初にそのアイテムIDが入っているスロットを探す
            //そのスロットにあまりを入れる
            return CalcPlaceItemReminder(requiredItemSlotCount,craftInventoryPlaceItem,mainInventoryRequiredItemCount,itemConfig,itemStackFactory);
        }


        /// <summary>
        /// そのアイテムIDあるスロットがいくつかを計算します
        /// アイテム数を均等に分配してアイテムを配置するために計算しています
        /// </summary>
        /// <param name="recipe">レシピ</param>
        /// <returns>key アイテムID value そのアイテムIDがあるスロット数</returns>
        private static Dictionary<int, int> CalcRequiredItemSlotCount(ItemMessagePack[] recipe)
        {
            var requiredItemSlotCount = new Dictionary<int, int>();
            foreach (var item in recipe)
            {
                if (item.Id == ItemConst.EmptyItemId) continue;
                
                if (requiredItemSlotCount.ContainsKey(item.Id))
                {
                    requiredItemSlotCount[item.Id] += item.Count;
                }
                else
                {
                    requiredItemSlotCount.Add(item.Id, item.Count);
                }
            }

            return requiredItemSlotCount;
        }


        /// <summary>
        /// あまりを考慮しないで、クラフトインベントリのどのスロットにどのアイテムが何個入るかを計算します 
        /// </summary>
        /// <param name="recipe">入れるレシピ</param>
        /// <param name="itemStackFactory">アイテム作成用</param>
        /// <param name="itemConfig">最大スタック確認用</param>
        /// <param name="mainInventoryRequiredItemCount">アイテムを均等に分配するためのメインインベントリにあるアイテム数のデータ</param>
        /// <param name="requiredItemSlotCount">均等に分配するために何個スロットがあるかのデータ</param>
        /// <returns>あまりを考慮しない場合のアイテム配置</returns>
        private static IItemStack[] CraftInventoryPlaceItemWithoutReminder(ItemMessagePack[] recipe,ItemStackFactory itemStackFactory,IItemConfig itemConfig,Dictionary<int,int> mainInventoryRequiredItemCount,Dictionary<int,int> requiredItemSlotCount)
        {
            var craftInventoryPlaceItem = new IItemStack[PlayerInventoryConst.CraftingSlotSize];
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var id = recipe[i].Id;
                if (id == ItemConst.EmptyItemId)
                {
                    craftInventoryPlaceItem[i] = itemStackFactory.CreatEmpty();
                    continue;
                }
                
                //一旦あまりを考慮しないアイテム数を計算する
                //メインインベントリに入っているアイテム数 / 必要とするスロット数 = スロットに入るアイテム数　となる
                var count = mainInventoryRequiredItemCount[id] / requiredItemSlotCount[id];
                count = Math.Clamp(count,0, itemConfig.GetItemConfig(id).MaxStack);
                
                craftInventoryPlaceItem[i] = itemStackFactory.Create(id,count);
            }

            return craftInventoryPlaceItem;
        }


        
        /// <summary>
        /// 入り切らなかったあまり分を加算する
        /// </summary>
        /// <param name="requiredItemSlotCount"></param>
        /// <param name="craftInventoryPlaceItem"></param>
        /// <param name="mainInventoryRequiredItemCount"></param>
        /// <param name="itemConfig"></param>
        /// <param name="itemStackFactory"></param>
        /// <returns></returns>
        private static IItemStack[] CalcPlaceItemReminder(Dictionary<int,int> requiredItemSlotCount,IItemStack[] craftInventoryPlaceItem,Dictionary<int,int> mainInventoryRequiredItemCount,IItemConfig itemConfig,ItemStackFactory itemStackFactory)
        {
            //あまり分を足す
            //アイテムIDのループを回し、一番最初にそのアイテムIDが入っているスロットを探す
            //そのスロットにあまりを入れる
            foreach (var id in requiredItemSlotCount.Keys)
            {
                for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
                {
                    if (id != craftInventoryPlaceItem[i].Id) continue;
                    
                    var remainder = mainInventoryRequiredItemCount[id] % requiredItemSlotCount[id];
                    var count = craftInventoryPlaceItem[i].Count + remainder;
                    count = Math.Clamp(count,0, itemConfig.GetItemConfig(id).MaxStack);
                    
                    craftInventoryPlaceItem[i] = itemStackFactory.Create(id,count);
                    break;
                }
            }

            return craftInventoryPlaceItem;
        }
    }
}