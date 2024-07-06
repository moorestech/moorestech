using System;
using System.Collections.Generic;
using Client.Common;
using Client.Mod.Glb;
using Core.Const;
using Game.Context;
using UnityEngine;

namespace Client.Game.InGame.Define
{
    [CreateAssetMenu(fileName = "BlockPrefabContainer", menuName = "moorestech/BlockPrefabContainer", order = 0)]
    public class BlockPrefabContainer : ScriptableObject
    {
        [SerializeField] private List<BlockPrefabInfo> blockPrefabs;
        
        public GameObject GetBlockPrefab(string modId, string blockName)
        {
            foreach (var blockPrefab in blockPrefabs)
                if (blockPrefab.ModId == modId && blockPrefab.BlockName == blockName)
                    return blockPrefab.BlockPrefab;
            return null;
        }
        
        public List<BlockData> GetBlockDataList()
        {
            var result = new List<BlockData>();
            
            var blockConfigs = ServerContext.BlockConfig.BlockConfigList;
            foreach (var blockConfig in blockConfigs)
            {
                var blockPrefab = GetBlockPrefab(blockConfig.ModId, blockConfig.Name);
                if (blockPrefab == null) continue;
                
                var blockName = blockConfig.Name;
                var type = blockConfig.Type;
                result.Add(new BlockData(blockPrefab, blockName, type, blockConfig));
            }
            
            return result;
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
}