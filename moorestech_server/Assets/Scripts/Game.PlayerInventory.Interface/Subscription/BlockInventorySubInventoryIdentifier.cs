using Game.Common.MessagePack;
using UnityEngine;

namespace Game.PlayerInventory.Interface.Subscription
{
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
}
