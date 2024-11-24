using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Item.Interface;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class ChestBlockInventory : BlockInventoryBase
    {
        public override void OpenBlockInventoryType(BlockGameObject blockGameObject)
        {
        }
        
        public override void CloseBlockInventory()
        {
            throw new System.NotImplementedException();
        }
        
        public override void UpdateInventorySlot(int packetSlot, IItemStack item)
        {
            throw new System.NotImplementedException();
        }
        
        public override void SetItemList(List<IItemStack> response)
        {
            throw new System.NotImplementedException();
        }
    }
}