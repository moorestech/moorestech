using Client.Game.InGame.Train.View.Object;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Train;
using Server.Util.MessagePack;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class TrainSubInventorySource : ISubInventorySource
    {
        public InventoryIdentifierMessagePack InventoryIdentifier { get; }
        public string UIPrefabAddressablePath => "Vanilla/UI/Train/CommonTrainInventoryView";
        
        private readonly TrainCarEntityObject _trainCarEntityObject;
        
        public TrainSubInventorySource(TrainCarEntityObject trainCarEntityObject)
        {
            _trainCarEntityObject = trainCarEntityObject;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateTrainMessage(trainCarEntityObject.TrainCarInstanceId.AsPrimitive());
        }
        
        public void ExecuteInitialize(ISubInventoryView subInventoryView)
        {
            ((ITrainInventoryView)subInventoryView).Initialize(_trainCarEntityObject);
        }
    }
}
