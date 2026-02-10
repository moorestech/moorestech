using System;
using Server.Util.MessagePack;
using static Server.Util.MessagePack.InventoryIdentifierMessagePack;

namespace Game.PlayerInventory.Interface.Subscription
{
    public static class ISubInventoryIdentifierExtension
    {
        public static InventoryIdentifierMessagePack ToMessagePack(this ISubInventoryIdentifier identifier)
        {
            return identifier switch
            {
                BlockInventorySubInventoryIdentifier blockId => CreateBlockMessage(blockId.Position),
                TrainInventorySubInventoryIdentifier trainId => CreateTrainMessage(trainId.TrainCarInstanceId),
                _ => throw new ArgumentException($"Unknown ISubInventoryIdentifier type: {identifier.GetType()}")
            };
        }
    }
}
