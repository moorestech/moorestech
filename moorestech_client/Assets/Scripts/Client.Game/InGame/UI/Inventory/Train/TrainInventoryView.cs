using System.Collections.Generic;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Train
{
    public class TrainInventoryView : MonoBehaviour, ITrainInventoryView
    {
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; }
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; }
        public void UpdateItemList(List<IItemStack> items)
        {
            throw new System.NotImplementedException();
        }
        public void UpdateInventorySlot(int slot, IItemStack item)
        {
            throw new System.NotImplementedException();
        }
        public void DestroyUI()
        {
            throw new System.NotImplementedException();
        }
        public void Initialize(TrainCarEntityObject trainCarEntity)
        {
            throw new System.NotImplementedException();
        }
    }
}