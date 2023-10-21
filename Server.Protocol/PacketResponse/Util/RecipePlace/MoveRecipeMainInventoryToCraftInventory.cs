using Core.Const;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;

namespace Server.Protocol.PacketResponse.Util.RecipePlace
{
    public class MoveRecipeMainInventoryToCraftInventory
    {

        ///     
        ///     
        ///     

        /// <param name="itemStackFactory"></param>
        /// <param name="main"></param>
        /// <param name="craft"></param>
        /// <param name="moveItem"></param>
        public static void Move(ItemStackFactory itemStackFactory, IOpenableInventory main, IOpenableInventory craft, IItemStack[] moveItem)
        {
            for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                
                if (moveItem[i].Id == ItemConst.EmptyItemId) continue;


                
                CollectAndMoveItem(itemStackFactory, moveItem[i], main);

                
                craft.SetItem(i, moveItem[i]);
            }
        }


        ///     0

        /// <param name="itemStackFactory"></param>
        /// <param name="moveItem"></param>
        /// <param name="main"></param>
        private static void CollectAndMoveItem(ItemStackFactory itemStackFactory, IItemStack moveItem, IOpenableInventory main)
        {
            var collectedItem = itemStackFactory.CreatEmpty();
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                
                if (main.GetItem(i).Id != moveItem.Id) continue;


                
                var addedItemResult = collectedItem.AddItem(main.GetItem(i));


                
                if (addedItemResult.ProcessResultItemStack.Count < moveItem.Count)
                {
                    collectedItem = addedItemResult.ProcessResultItemStack;
                    main.SetItem(i, addedItemResult.RemainderItemStack);
                    continue;
                }


                
                if (addedItemResult.ProcessResultItemStack.Count == moveItem.Count)
                {
                    main.SetItem(i, addedItemResult.RemainderItemStack);
                    break;
                }


                
                
                var reminderCount = addedItemResult.ProcessResultItemStack.Count - moveItem.Count;
                //　
                var mainItemCount = reminderCount + addedItemResult.RemainderItemStack.Count;

                var item = itemStackFactory.Create(moveItem.Id, mainItemCount);
                main.SetItem(i, item);
            }
        }
    }
}