using System.Collections.Generic;
using Client.Game.InGame.Entity.Object;
using Core.Item.Interface;
using Mooresmaster.Model.TrainModule;

namespace Client.Game.InGame.UI.Inventory.Train
{
    public interface ITrainInventoryView : ISubInventory
    {
        public void Initialize(TrainEntityObject trainEntity);
        public void UpdateItemList(List<IItemStack> response);
        public void UpdateInventorySlot(int slot, IItemStack item);
        public void DestroyUI();
    }
}