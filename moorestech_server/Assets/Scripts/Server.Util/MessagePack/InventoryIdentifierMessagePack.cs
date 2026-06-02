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
        [Key(0)] public InventoryType InventoryType { get; set; }
        
        /// <summary>
        /// ブロックインベントリの場合の座標
        /// Block position for block inventor
        /// </summary>
        [Key(1)] public Vector3IntMessagePack BlockPosition { get; set; }
        
        /// <summary>
        /// 列車インベントリの場合のTrainCarInstanceId
        /// Train car instance id for train inventory
        /// </summary>
        [Key(2)] public string TrainCarInstanceId { get; set; }

        /// <summary>
        /// プレイヤーインベントリの場合のPlayerId
        /// Player id for player inventory
        /// </summary>
        [Key(3)] public int PlayerId { get; set; }
        
        
        public InventoryIdentifierMessagePack() { }

        public static InventoryIdentifierMessagePack CreateMainMessage(int playerId)
        {
            return new InventoryIdentifierMessagePack
            {
                InventoryType = InventoryType.Main,
                PlayerId = playerId,
            };
        }

        public static InventoryIdentifierMessagePack CreateGrabMessage(int playerId)
        {
            return new InventoryIdentifierMessagePack
            {
                InventoryType = InventoryType.Grab,
                PlayerId = playerId,
            };
        }
        
        public static InventoryIdentifierMessagePack CreateBlockMessage(Vector3Int position)
        {
            return new InventoryIdentifierMessagePack
            {
                InventoryType = InventoryType.Block,
                BlockPosition = new Vector3IntMessagePack(position),
            };
        }
        
        public static InventoryIdentifierMessagePack CreateTrainMessage(long trainCarInstanceId)
        {
            return new InventoryIdentifierMessagePack
            {
                InventoryType = InventoryType.Train,
                TrainCarInstanceId = trainCarInstanceId.ToString(),
            };
        }
        
    }
}
