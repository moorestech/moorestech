using System.Collections.Generic;
using Core.Item;
using MainGame.UnityView.UI.Inventory.Element;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;

namespace MainGame.UnityView.UI.Inventory
{
    public interface ISubInventory
    {
        public IReadOnlyList<ItemSlotObject> SubInventorySlotObjects { get; }
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; }
    }
    
    public class EmptySubInventory : ISubInventory
    {
        public IReadOnlyList<ItemSlotObject> SubInventorySlotObjects { get; }
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; }
        
        public EmptySubInventory()
        {
            Count = 0;
            SubInventorySlotObjects = new List<ItemSlotObject>();
            SubInventory = new List<IItemStack>();
            ItemMoveInventoryInfo = null;
        }
    }
}