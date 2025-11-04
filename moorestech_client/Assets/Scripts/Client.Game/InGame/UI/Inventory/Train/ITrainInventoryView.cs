using Client.Game.InGame.Entity.Object;
using Mooresmaster.Model.TrainModule;

namespace Client.Game.InGame.UI.Inventory.Train
{
    public interface ITrainInventoryView : ISubInventory
    {
        public void Initialize(TrainEntityObject trainEntity);
    }
}