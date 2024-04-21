using System;
using System.Collections.Generic;
using Client.Common;
using Game.Context;
using MainGame.ModLoader.Glb;
using UnityEngine;

namespace Client.Game.Block
{
    [CreateAssetMenu(fileName = "BlockPrefabContainer", menuName = "moorestech/BlockPrefabContainer", order = 0)]
    public class BlockPrefabContainer : ScriptableObject
    {
        public IReadOnlyList<BlockPrefabInfo> BlockPrefabs => blockPrefabs;
        [SerializeField] private List<BlockPrefabInfo> blockPrefabs;

        public GameObject GetBlockPrefab(string modId, string blockName)
        {
            foreach (var blockPrefab in blockPrefabs)
            {
                if (blockPrefab.ModId == modId && blockPrefab.BlockName == blockName)
                {
                    return blockPrefab.BlockPrefab;
                }
            }
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
                result.Add(new BlockData(blockPrefab, blockName, type));
            }
            
            return result;
        }
    }

    [Serializable]
    public class BlockPrefabInfo
    {
        public string ModId => AlphaMod.ModId;
        //TODO 将来的に設定できるようにする [SerializeField] private string modId = AlphaMod.ModId;
        
        public string BlockName => blockName;
        [SerializeField] private string blockName;
        
        public GameObject BlockPrefab => blockPrefab;
        [SerializeField] private GameObject blockPrefab;
    }
}