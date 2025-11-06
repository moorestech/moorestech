using System;
using Server.Util.MessagePack;
using UnityEngine;

namespace Game.PlayerInventory.Interface.Subscription
{
    // サブスクリプション識別子の共通インターフェース
    // Common interface for subscription identifiers
    public interface ISubInventoryIdentifier
    {
        InventoryType Type { get; }
        
        // HashSetで使用するので、EqualsとGetHashCodeをオーバーライドする必要がある
        // Since it is used in HashSet, Equals and GetHashCode need to be overridden
        bool Equals(object obj);
        int GetHashCode();
    }

    // ブロックインベントリを識別する実装
    // Identifier implementation for block inventories
    public class BlockInventorySubInventoryIdentifier : ISubInventoryIdentifier
    {
        public InventoryType Type => InventoryType.Block;
        public Vector3Int Position { get; }

        public BlockInventorySubInventoryIdentifier(Vector3Int position)
        {
            Position = position;
        }

        public override bool Equals(object obj)
        {
            if (obj is BlockInventorySubInventoryIdentifier other)
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

