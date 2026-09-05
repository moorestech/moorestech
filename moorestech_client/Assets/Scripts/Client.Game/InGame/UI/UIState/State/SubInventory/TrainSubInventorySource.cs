using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Train;
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

        // 直近の開閉で開けなかった理由。null なら正常に開けている
        // Why the latest open attempt failed; null means the inventory opened normally
        public TrainInventoryMessageType? LastOpenMessage { get; private set; }

        public TrainSubInventorySource(long trainCarInstanceId)
        {
            TrainCarInstanceId = trainCarInstanceId;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateTrainMessage(trainCarInstanceId);
        }

        public SubInventoryModel CreateModel(InventoryResponse inventoryResponse)
        {
            var model = new SubInventoryModel(new TrainInventorySubInventoryIdentifier(TrainCarInstanceId));
            switch (inventoryResponse.Result)
            {
                // 成功応答は前回の失敗理由を持ち越さない（エラー種別付きでアイテムが並ぶ矛盾を防ぐ）
                // A successful response drops the previous failure so items never arrive alongside an error kind
                case InventoryRequestResult.Success:
                    LastOpenMessage = null;
                    model.SetItems(inventoryResponse.Items);
                    return model;
                case InventoryRequestResult.ContainerNotFound:
                    LastOpenMessage = TrainInventoryMessageType.ContainerMissing;
                    return model;
                case InventoryRequestResult.TrainCarNotFound:
                    LastOpenMessage = TrainInventoryMessageType.TrainCarMissing;
                    return model;
                default:
                    LastOpenMessage = TrainInventoryMessageType.OpenFailed;
                    return model;
            }
        }
    }
}
