using System;
using Server.Util.MessagePack;
using UnityEngine;

namespace Game.PlayerInventory.Interface
{
    // サブスクリプション識別子の共通インターフェース
    // Common interface for subscription identifiers
    public interface ISubscriptionIdentifier
    {
        InventoryType Type { get; }
    }

    // ブロックインベントリを識別する実装
    // Identifier implementation for block inventories
    public class BlockInventorySubscriptionIdentifier : ISubscriptionIdentifier
    {
        public InventoryType Type => InventoryType.Block;
        public Vector3Int Position { get; }

        public BlockInventorySubscriptionIdentifier(Vector3Int position)
        {
            Position = position;
        }

        public override bool Equals(object obj)
        {
            if (obj is BlockInventorySubscriptionIdentifier other)
            {
                return Position.Equals(other.Position);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Position.GetHashCode();
        }
    }

    // 列車インベントリを識別する実装
    // Identifier implementation for train inventories
    public class TrainInventorySubscriptionIdentifier : ISubscriptionIdentifier
    {
        public InventoryType Type => InventoryType.Train;
        public Guid TrainCarId { get; }

        public TrainInventorySubscriptionIdentifier(Guid trainCarId)
        {
            TrainCarId = trainCarId;
        }

        public override bool Equals(object obj)
        {
            if (obj is TrainInventorySubscriptionIdentifier other)
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

