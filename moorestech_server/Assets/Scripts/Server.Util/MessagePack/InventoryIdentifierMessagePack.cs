using System;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    public enum InventoryType : byte
    {
        Block,
        Train,
    }
    
    /// <summary>
    /// インベントリ識別子を保持するMessagePackクラス
    /// MessagePack class that holds inventory identifier
    /// </summary>
    [MessagePackObject]
    public class InventoryIdentifierMessagePack
    {
        [Key(0)] public InventoryType InventoryType { get; set; }
        
        /// <summary>
        /// ブロックインベントリの場合の座標
        /// Block position for block inventor
        /// </summary>
        [Key(1)] public Vector3IntMessagePack BlockPosition { get; set; }
        
        /// <summary>
        /// 列車インベントリの場合のTrainId
        /// TrainId for train inventory
        /// </summary>
        [Key(2)] public string TrainId { get; set; }
        
        public InventoryIdentifierMessagePack() { }
        
        public InventoryIdentifierMessagePack(Vector3Int position)
        {
            // ブロックインベントリ識別子を初期化
            // Initialize block inventory identifier
            InventoryType = InventoryType.Block;
            BlockPosition = new Vector3IntMessagePack(position);
        }
        
        public InventoryIdentifierMessagePack(Guid trainId)
        {
            // 列車インベントリ識別子を初期化
            // Initialize train inventory identifier
            InventoryType = InventoryType.Train;
            TrainId = trainId.ToString();
        }
        
        public static InventoryIdentifierMessagePack CreateBlockMessage(Vector3Int position)
        {
            return new InventoryIdentifierMessagePack
            {
                InventoryType = InventoryType.Block,
                BlockPosition = new Vector3IntMessagePack(position),
            };
        }
        
        public static InventoryIdentifierMessagePack CreateTrainMessage(Guid trainId)
        {
            return new InventoryIdentifierMessagePack
            {
                InventoryType = InventoryType.Train,
                TrainId = trainId.ToString(),
            };
        }
        
    }
}
