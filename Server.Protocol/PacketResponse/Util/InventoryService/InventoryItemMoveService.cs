using System;
using Core.Inventory;
using Core.Item;

namespace Server.Protocol.PacketResponse.Util.InventoryService
{
    public static class InventoryItemMoveService
    {
        public static void Move(ItemStackFactory itemStackFactory, IOpenableInventory fromInventory, int fromSlot, IOpenableInventory toInventory, int toSlot, int itemCount)
        {
            try
            {
                ExecuteMove(itemStackFactory, fromInventory, fromSlot, toInventory, toSlot, itemCount);
            }
            catch (ArgumentOutOfRangeException e)
            {
                //TODO 
                var fromInventoryName = fromInventory.GetType().Name;
                var toInventoryName = toInventory.GetType().Name;
                Console.WriteLine($"InventoryItemMoveService.Move: \n {e.Message} \n fromInventory={fromInventoryName} fromSlot={fromSlot} toInventory={toInventoryName} toSlot={toSlot} itemCount={itemCount}  \n {e.StackTrace}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void ExecuteMove(ItemStackFactory itemStackFactory, IOpenableInventory fromInventory, int fromSlot, IOpenableInventory toInventory, int toSlot, int itemCount)
        {
            
            if (fromInventory.GetHashCode() == toInventory.GetHashCode() && fromSlot == toSlot) return;


            
            var originItem = fromInventory.GetItem(fromSlot);
            
            if (originItem.Count < itemCount) itemCount = originItem.Count;

            
            var moveItem = itemStackFactory.Create(originItem.Id, itemCount);

            var destinationInventoryItem = toInventory.GetItem(toSlot);

            
            //ID
            if (destinationInventoryItem.Count == 0 || originItem.Id == destinationInventoryItem.Id)
            {
                
                var replaceItem = toInventory.ReplaceItem(toSlot, moveItem);

                
                //NullItem
                var playerItemCount = originItem.Count - itemCount;
                var remainItem = replaceItem.AddItem(itemStackFactory.Create(originItem.Id, playerItemCount))
                    .ProcessResultItemStack;

                
                fromInventory.SetItem(fromSlot, remainItem);
            }
            //ID
            
            else if (itemCount == originItem.Count)
            {
                toInventory.SetItem(toSlot, originItem);
                fromInventory.SetItem(fromSlot, destinationInventoryItem);
            }
        }
    }
}