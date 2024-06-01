using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;

namespace Client.Game.InGame.UI.Inventory
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
        public EmptySubInventory()
        {
            Count = 0;
            SubInventorySlotObjects = new List<ItemSlotObject>();
            SubInventory = new List<IItemStack>();
            ItemMoveInventoryInfo = null;
        }
        public IReadOnlyList<ItemSlotObject> SubInventorySlotObjects { get; }
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; }
    }
}