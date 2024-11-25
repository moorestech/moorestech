using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Item.Interface;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public interface IBlockInventory : ISubInventory
    {
        public void Initialize(BlockGameObject blockGameObject);
        public void UpdateItemList(List<IItemStack> response);
        public void UpdateInventorySlot(int slot, IItemStack item);
        public void DestroyUI();
    }
}