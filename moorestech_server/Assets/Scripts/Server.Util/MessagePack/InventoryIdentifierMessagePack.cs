using System;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    /// <summary>
    /// インベントリ識別子を保持するMessagePackクラス
    /// MessagePack class that holds inventory identifier
    /// </summary>
    [MessagePackObject]
    public class InventoryIdentifierMessagePack
    {
        /// <summary>
        /// ブロックインベントリの場合の座標（InventoryType.Blockの場合に使用）
        /// Block position for block inventory (used when InventoryType.Block)
        /// </summary>
        [Key(0)] public Vector3IntMessagePack BlockPosition { get; set; }
        
        /// <summary>
        /// 列車インベントリの場合のTrainId（InventoryType.Trainの場合に使用）
        /// TrainId for train inventory (used when InventoryType.Train)
        /// </summary>
        [Key(1)] public string TrainId { get; set; }
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public InventoryIdentifierMessagePack()
        {
        }
        
        /// <summary>
        /// ブロックインベントリ用のコンストラクタ
        /// Constructor for block inventory
        /// </summary>
        public InventoryIdentifierMessagePack(Vector3Int blockPosition)
        {
            BlockPosition = new Vector3IntMessagePack(blockPosition);
            TrainId = null;
        }
        
        /// <summary>
        /// 列車インベントリ用のコンストラクタ
        /// Constructor for train inventory
        /// </summary>
        public InventoryIdentifierMessagePack(Guid trainId)
        {
            BlockPosition = null;
            TrainId = trainId.ToString();
        }
    }
}

