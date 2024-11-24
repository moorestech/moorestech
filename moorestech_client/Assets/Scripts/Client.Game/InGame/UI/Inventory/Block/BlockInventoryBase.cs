using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public abstract class BlockInventoryBase : MonoBehaviour, ISubInventory
    {
        public IReadOnlyList<ItemSlotObject> SubInventorySlotObjects => _blockItemSlotObjects;
        public int Count => _blockItemSlotObjects.Count;
        protected readonly List<ItemSlotObject> _blockItemSlotObjects = new();
        public List<IItemStack> SubInventory { get; } = new();
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; protected set; }
        
        public abstract void Initialize(BlockGameObject blockGameObject);
        
        
        public void UpdateItemList(List<IItemStack> response)
        {
            SubInventory.Clear();
            SubInventory.AddRange(response);
        }
        public void UpdateInventorySlot(int slot, IItemStack item)
        {
            if (SubInventory.Count <= slot)
            {
                Debug.LogError($"インベントリのサイズを超えています。item:{item} slot:{slot}");
                return;
            }
            
            SubInventory[slot] = item;
        }
    }
}