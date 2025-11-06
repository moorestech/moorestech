using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;

namespace Client.Game.InGame.UI.Inventory
{
    public interface ISubInventory
    {
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; }
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; }
    }
    
    public static class ISubInventoryExtension
    {
        public static bool IsEnableSubInventory(this ISubInventory subInventory) => subInventory.Count > 0;
    }
    
    public class EmptySubInventory : ISubInventory
    {
        public EmptySubInventory()
        {
            Count = 0;
            SubInventorySlotObjects = new List<ItemSlotView>();
            SubInventory = new List<IItemStack>();
            ISubInventoryIdentifier = null;
        }

        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; }
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; }
    }
}