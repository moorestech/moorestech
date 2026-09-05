using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.Inventory;
using Client.Network.API;
using Game.PlayerInventory.Interface.Subscription;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class TrainSubInventorySource : ISubInventorySource
    {
        public InventoryIdentifierMessagePack InventoryIdentifier { get; }
        public long TrainCarInstanceId { get; }

        public TrainSubInventorySource(TrainCarEntityObject trainCarEntityObject) : this(trainCarEntityObject.TrainCarInstanceId.AsPrimitive())
        {
        }

        // 識別子だけで組める経路。テストと本番の両方が同じ変換を通る
        // Identifier-only construction path shared by tests and production
        protected TrainSubInventorySource(long trainCarInstanceId)
        {
            TrainCarInstanceId = trainCarInstanceId;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateTrainMessage(trainCarInstanceId);
        }

        public SubInventoryModel CreateModel(InventoryResponse inventoryResponse)
        {
            var model = new SubInventoryModel(new TrainInventorySubInventoryIdentifier(TrainCarInstanceId));
            switch (inventoryResponse.Result)
            {
                case InventoryRequestResult.Success:
                    model.SetItems(inventoryResponse.Items);
                    return model;
                case InventoryRequestResult.ContainerNotFound:
                    model.SetTrainMessage(TrainInventoryMessageType.ContainerMissing);
                    return model;
                case InventoryRequestResult.TrainCarNotFound:
                    model.SetTrainMessage(TrainInventoryMessageType.TrainCarMissing);
                    return model;
                default:
                    model.SetTrainMessage(TrainInventoryMessageType.OpenFailed);
                    return model;
            }
        }
    }
}
