using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;

namespace Client.Game.InGame.UI.Inventory
{
    public interface ISubInventory
    {
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; }
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; }
    }
    
    public class EmptySubInventory : ISubInventory
    {
        public EmptySubInventory()
        {
            Count = 0;
            SubInventorySlotObjects = new List<ItemSlotView>();
            SubInventory = new List<IItemStack>();
            ItemMoveInventoryInfo = null;
        }
        
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; }
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; }
    }
    
    public static class ISubInventoryExtension
    {
        public static bool IsEnableSubInventory(this ISubInventory subInventory) => subInventory.Count > 0;
    } 
}