using Server.Util.MessagePack;

namespace Game.PlayerInventory.Interface.Subscription
{
    // 列車インベントリを識別する実装
    // Identifier implementation for train inventories
    public class TrainInventorySubInventoryIdentifier : ISubInventoryIdentifier
    {
        public InventoryType Type => InventoryType.Train;
        public long TrainCarInstanceId { get; }

        public TrainInventorySubInventoryIdentifier(long trainCarInstanceId)
        {
            TrainCarInstanceId = trainCarInstanceId;
        }

        public override bool Equals(object obj)
        {
            if (obj is TrainInventorySubInventoryIdentifier other)
            {
                return TrainCarInstanceId.Equals(other.TrainCarInstanceId);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return TrainCarInstanceId.GetHashCode();
        }
    }
}
