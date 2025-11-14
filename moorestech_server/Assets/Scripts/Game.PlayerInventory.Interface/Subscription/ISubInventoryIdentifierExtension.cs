using System;
using Game.Common.MessagePack;
using static Game.Common.MessagePack.InventoryIdentifierMessagePack;

namespace Game.PlayerInventory.Interface.Subscription
{
    public static class ISubInventoryIdentifierExtension
    {
        public static InventoryIdentifierMessagePack ToMessagePack(this ISubInventoryIdentifier identifier)
        {
            return identifier switch
            {
                BlockInventorySubInventoryIdentifier blockId => CreateBlockMessage(blockId.Position),
                TrainInventorySubInventoryIdentifier trainId => CreateTrainMessage(trainId.TrainCarId),
                _ => throw new ArgumentException($"Unknown ISubInventoryIdentifier type: {identifier.GetType()}")
            };
        }
    }
}
