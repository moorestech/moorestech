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
            
            
            //そのスロットに入るアイテム数を計算する
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