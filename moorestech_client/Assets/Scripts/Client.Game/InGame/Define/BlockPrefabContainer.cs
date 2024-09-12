using System;
using System.Collections.Generic;
using Client.Common;
using Core.Const;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.Define
{
    [CreateAssetMenu(fileName = "BlockPrefabContainer", menuName = "moorestech/BlockPrefabContainer", order = 0)]
    public class BlockPrefabContainer : ScriptableObject
    {
        [SerializeField] private List<BlockPrefabInfo> blockPrefabs;
        
        public Dictionary<BlockId,BlockObjectInfo> GetBlockDataList()
        {
            var result = new Dictionary<BlockId,BlockObjectInfo>();
            
            var blockConfigs = ServerContext.BlockConfig.BlockConfigList;
            foreach (var blockConfig in blockConfigs)
            {
                var blockPrefab = GetBlockPrefab(blockConfig.ModId, blockConfig.Name);
                if (blockPrefab == null) continue;
                
                var blockName = blockConfig.Name;
                var type = blockConfig.Type;
                result.Add(new BlockObjectInfo(blockPrefab, blockName, type));
            }
            
            return result;
        }
        
        private GameObject GetBlockPrefab(string blockName)
        {
            foreach (var blockPrefab in blockPrefabs)
                if (blockPrefab.ModId == modId && blockPrefab.BlockName == blockName)
                    return blockPrefab.BlockPrefab;
            return null;
        }
    }
    
    [Serializable]
    public class BlockPrefabInfo
    {
        [SerializeField] private string blockName;
        [SerializeField] private GameObject blockPrefab;
        
        public string ModId => AlphaMod.ModId;
        //TODO 将来的に設定できるようにする [SerializeField] private string modId = AlphaMod.ModId;
        
        public string BlockName => blockName;
        
        public GameObject BlockPrefab => blockPrefab;
    }
    
    
    
    public class BlockObjectInfo
    {
        public readonly BlockMasterElement BlockMasterElement;
        public readonly GameObject BlockObject;
        public readonly string Name;
        public readonly string Type;
        
        public BlockObjectInfo(GameObject blockObject, string name, string type, BlockMasterElement blockMasterElement)
        {
            BlockObject = blockObject;
            Name = name;
            Type = type;
            BlockMasterElement = blockMasterElement;
        }
    }
}