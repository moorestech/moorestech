using Core.Inventory;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.InventoryService.Resolver
{
    public class TrainInventoryIdentifierResolver : IInventoryIdentifierResolver
    {
        public InventoryType InventoryType => InventoryType.Train;

        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;

        public TrainInventoryIdentifierResolver(ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
        }

        public IOpenableInventory Resolve(InventoryIdentifierMessagePack identifier)
        {
            // 列車カーIDからアイテムコンテナを取得する
            // Get the item container from the train car id.
            var trainCarInstanceId = new TrainCarInstanceId(long.Parse(identifier.TrainCarInstanceId));
            if (!_trainUnitLookupDatastore.TryGetTrainCar(trainCarInstanceId, out var trainCar)) return null;
            if (trainCar.Container is not ItemTrainCarContainer itemContainer) return null;

            return itemContainer;
        }
    }
}
