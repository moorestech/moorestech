using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Element;
using Client.Game.InGame.UI.Inventory.Sub;
using Core.Item.Interface;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public abstract class BlockInventoryBase : MonoBehaviour, ISubInventory
    {
        public abstract void OpenBlockInventoryType(BlockGameObject blockGameObject);
        public abstract void CloseBlockInventory();
        public abstract void UpdateInventorySlot(int packetSlot, IItemStack item);
        public abstract void SetItemList(List<IItemStack> response);
        public IReadOnlyList<ItemSlotObject> SubInventorySlotObjects { get; }
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; }
    }
}