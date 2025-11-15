using System;
using Game.Common.MessagePack;

namespace Game.PlayerInventory.Interface.Subscription
{
    // 列車インベントリを識別する実装
    // Identifier implementation for train inventories
    public class TrainInventorySubInventoryIdentifier : ISubInventoryIdentifier
    {
        public InventoryType Type => InventoryType.Train;
        public Guid TrainCarId { get; }

        public TrainInventorySubInventoryIdentifier(Guid trainCarId)
        {
            TrainCarId = trainCarId;
        }

        public override bool Equals(object obj)
        {
            if (obj is TrainInventorySubInventoryIdentifier other)
            {
                return TrainCarId.Equals(other.TrainCarId);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return TrainCarId.GetHashCode();
        }
    }
}
