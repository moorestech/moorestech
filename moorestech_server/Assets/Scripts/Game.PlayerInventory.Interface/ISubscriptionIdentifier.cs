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
    }

    // 列車インベントリを識別する実装
    // Identifier implementation for train inventories
    public class TrainInventorySubscriptionIdentifier : ISubscriptionIdentifier
    {
        public InventoryType Type => InventoryType.Train;
        public Guid TrainId { get; }
        
        public TrainInventorySubscriptionIdentifier(Guid trainId)
        {
            TrainId = trainId;
        }
    }
}

