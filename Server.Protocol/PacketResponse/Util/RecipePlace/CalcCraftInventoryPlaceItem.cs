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

        ///     
        ///     

        /// <param name="itemStackFactory"></param>
        /// <param name="itemConfig"></param>
        /// <param name="recipe"></param>
        /// <param name="mainInventoryRequiredItemCount"></param>
        /// <returns></returns>
        public static IItemStack[] Calc(ItemStackFactory itemStackFactory, IItemConfig itemConfig, ItemMessagePack[] recipe, Dictionary<int, int> mainInventoryRequiredItemCount)
        {
            //ID
            var requiredItemSlotCount = CalcRequiredItemSlotCount(recipe);


            
            var craftInventoryPlaceItem = CraftInventoryPlaceItemWithoutReminder(recipe, itemStackFactory, itemConfig, mainInventoryRequiredItemCount, requiredItemSlotCount);


            
            //IDID
            
            return CalcPlaceItemReminder(requiredItemSlotCount, craftInventoryPlaceItem, mainInventoryRequiredItemCount, itemConfig, itemStackFactory);
        }



        ///     ID
        ///     

        /// <param name="recipe"></param>
        /// <returns>key ID value ID</returns>
        private static Dictionary<int, int> CalcRequiredItemSlotCount(ItemMessagePack[] recipe)
        {
            var requiredItemSlotCount = new Dictionary<int, int>();
            foreach (var item in recipe)
            {
                if (item.Id == ItemConst.EmptyItemId) continue;

                if (requiredItemSlotCount.ContainsKey(item.Id))
                    requiredItemSlotCount[item.Id] += item.Count;
                else
                    requiredItemSlotCount.Add(item.Id, item.Count);
            }

            return requiredItemSlotCount;
        }



        ///     

        /// <param name="recipe"></param>
        /// <param name="itemStackFactory"></param>
        /// <param name="itemConfig"></param>
        /// <param name="mainInventoryRequiredItemCount"></param>
        /// <param name="requiredItemSlotCount"></param>
        /// <returns></returns>
        private static IItemStack[] CraftInventoryPlaceItemWithoutReminder(ItemMessagePack[] recipe, ItemStackFactory itemStackFactory, IItemConfig itemConfig, Dictionary<int, int> mainInventoryRequiredItemCount, Dictionary<int, int> requiredItemSlotCount)
        {
            var craftInventoryPlaceItem = new IItemStack[PlayerInventoryConst.CraftingSlotSize];
            for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var id = recipe[i].Id;
                
                if (id == ItemConst.EmptyItemId || !mainInventoryRequiredItemCount.ContainsKey(id))
                {
                    craftInventoryPlaceItem[i] = itemStackFactory.CreatEmpty();
                    continue;
                }


                
                // /  = 　
                var count = mainInventoryRequiredItemCount[id] / requiredItemSlotCount[id];
                count = Math.Clamp(count, 0, itemConfig.GetItemConfig(id).MaxStack);

                craftInventoryPlaceItem[i] = itemStackFactory.Create(id, count);
            }

            return craftInventoryPlaceItem;
        }



        ///     

        /// <param name="requiredItemSlotCount"></param>
        /// <param name="craftInventoryPlaceItem"></param>
        /// <param name="mainInventoryRequiredItemCount"></param>
        /// <param name="itemConfig"></param>
        /// <param name="itemStackFactory"></param>
        /// <returns></returns>
        private static IItemStack[] CalcPlaceItemReminder(Dictionary<int, int> requiredItemSlotCount, IItemStack[] craftInventoryPlaceItem, Dictionary<int, int> mainInventoryRequiredItemCount, IItemConfig itemConfig, ItemStackFactory itemStackFactory)
        {
            
            //IDID
            
            foreach (var id in requiredItemSlotCount.Keys)
                for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
                {
                    if (id != craftInventoryPlaceItem[i].Id) continue;

                    var remainder = mainInventoryRequiredItemCount[id] % requiredItemSlotCount[id];
                    var count = craftInventoryPlaceItem[i].Count + remainder;
                    count = Math.Clamp(count, 0, itemConfig.GetItemConfig(id).MaxStack);

                    craftInventoryPlaceItem[i] = itemStackFactory.Create(id, count);
                    break;
                }

            return craftInventoryPlaceItem;
        }
    }
}