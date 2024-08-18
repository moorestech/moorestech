using System.Collections.Generic;
using UnityEngine;

namespace Game.Block.Interface.BlockConfig
{
    public class BlockConfigData
    {
        public readonly long BlockHash;
        public readonly int BlockId;
        
        public readonly Vector3Int BlockSize;
        
        public readonly List<ConnectSettings> InputConnectSettings;
        
        public readonly int ItemId;
        public readonly ModelTransform ModelTransform;
        public readonly string ModId;
        public readonly string Name;
        public readonly List<ConnectSettings> OutputConnectSettings;
        
        public readonly IBlockConfigParam Param;
        
        public readonly string Type;
        
        public BlockConfigData(string modId, int blockId, string name, long blockHash, string type,
            IBlockConfigParam param, int itemId, ModelTransform modelTransform, Vector3Int blockSize, List<ConnectSettings> inputConnectSettings, List<ConnectSettings> outputConnectSettings)
        {
            BlockId = blockId;
            Name = name;
            Type = type;
            Param = param;
            ItemId = itemId;
            ModelTransform = modelTransform;
            BlockSize = blockSize;
            ModId = modId;
            BlockHash = blockHash;
            InputConnectSettings = inputConnectSettings;
            OutputConnectSettings = outputConnectSettings;
        }
    }
    
    public class ModelTransform
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
        
        public ModelTransform(Vector3 position, Vector3 rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
        
        public ModelTransform()
        {
        }
    }
    
    public class ConnectSettings
    {
        public List<Vector3Int> ConnectorDirections; // インプットされる方向、もしくはアウトプットする方向
        public Vector3Int ConnectorPosOffset; // 原点からみたコネクターの場所のオフセット
        public IConnectOption Option; // オプション
    }
}