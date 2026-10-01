using Client.Game.InGame.Interact;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Network.API;
using Game.PlayerInventory.Interface.Subscription;
using Game.Train.Unit;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class TrainSubInventorySource : ISubInventorySource
    {
        public InventoryIdentifierMessagePack InventoryIdentifier { get; }
        public long TrainCarInstanceId => _trainCarInstanceId.AsPrimitive();

        // 直近の開閉で開けなかった理由。null なら正常に開けている
        // Why the latest open attempt failed; null means the inventory opened normally
        public TrainInventoryMessageType? LastOpenMessage { get; private set; }

        private readonly TrainCarInstanceId _trainCarInstanceId;
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;

        public TrainSubInventorySource(TrainCarInstanceId trainCarInstanceId, TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _trainCarInstanceId = trainCarInstanceId;
            _trainCarObjectDatastore = trainCarObjectDatastore;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateTrainMessage(trainCarInstanceId.AsPrimitive());
        }

        public bool TryGetReachTarget(out IInteractable reachTarget)
        {
            // 再同期や連結で車両の表示が作り直されるため、開いた瞬間の参照ではなくIDから今の表示を引く
            // Resync and coupling rebuild car views, so resolve the current view by ID instead of the reference held at open
            if (!_trainCarObjectDatastore.TryGetEntity(_trainCarInstanceId, out var trainCarEntityObject))
            {
                reachTarget = null;
                return false;
            }

            reachTarget = trainCarEntityObject.Interactable;
            return true;
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
