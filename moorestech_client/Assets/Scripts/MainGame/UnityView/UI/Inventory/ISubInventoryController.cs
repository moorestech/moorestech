using System.Collections.Generic;
using Core.Item;
using MainGame.UnityView.UI.UIObjects;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;

namespace MainGame.UnityView.UI.Inventory
{
    public interface ISubInventoryController
    {
        public IReadOnlyList<UIBuilderItemSlotObject> SubInventorySlotObjects { get; }
        
        public int SubInventorySlotCount { get; }
        public IItemStack GetItemStack(int slot);
        
        public void OnMoveItem(ItemMoveInventoryType from,int fromSlot, ItemMoveInventoryType to, int toSlot, int count);
    }
}