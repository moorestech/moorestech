using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Network.API;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class TrainSubInventorySource : ISubInventorySource
    {
        public InventoryIdentifierMessagePack InventoryIdentifier { get; }
        public string UIPrefabAddressablePath => "Vanilla/UI/Train/CommonTrainInventoryView";
        
        private readonly TrainCarEntityObject _trainCarEntityObject;
        public long TrainCarInstanceId => _trainCarEntityObject.TrainCarInstanceId.AsPrimitive();
        
        public TrainSubInventorySource(TrainCarEntityObject trainCarEntityObject)
        {
            _trainCarEntityObject = trainCarEntityObject;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateTrainMessage(trainCarEntityObject.TrainCarInstanceId.AsPrimitive());
        }
        
        public void ExecuteInitialize(ISubInventoryView subInventoryView, InventoryResponse inventoryResponse)
        {
            var trainInventoryView = (ITrainInventoryView)subInventoryView;
            switch (inventoryResponse.Result)
            {
                case InventoryRequestResult.Success:
                    trainInventoryView.Initialize(_trainCarEntityObject);
                    trainInventoryView.UpdateItemList(inventoryResponse.Items);
                    return;
                case InventoryRequestResult.ContainerNotFound:
                    trainInventoryView.HideSlotObjects();
                    trainInventoryView.ShowMessage(TrainInventoryMessageType.ContainerMissing);
                    return;
                case InventoryRequestResult.TrainCarNotFound:
                    trainInventoryView.HideSlotObjects();
                    trainInventoryView.ShowMessage(TrainInventoryMessageType.TrainCarMissing);
                    return;
                default:
                    trainInventoryView.HideSlotObjects();
                    trainInventoryView.ShowMessage(TrainInventoryMessageType.OpenFailed);
                    return;
            }
        }
    }
}
