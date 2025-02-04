using System;
using System.Collections.Generic;
using Core.Master;
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
            
            foreach (var blockId in MasterHolder.BlockMaster.GetBlockIds())
            {
                var blockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                
                var blockPrefab = GetBlockPrefab(blockMasterElement.BlockGuid);
                if (blockPrefab == null) continue;
                
                result.Add(blockId, new BlockObjectInfo(blockPrefab, blockMasterElement));
            }
            
            return result;
        }
        
        private GameObject GetBlockPrefab(Guid blockGuid)
        {
            foreach (var blockPrefab in blockPrefabs)
                if (blockPrefab.GetGuid() == blockGuid)
                    return blockPrefab.BlockPrefab;
            return null;
        }
    }
    
    [Serializable]
    public class BlockPrefabInfo
    {
        public GameObject BlockPrefab => blockPrefab;
        
        [SerializeField] private string blockGuid;
        [SerializeField] private GameObject blockPrefab;
        
        public Guid GetGuid()
        {
            if (Guid.TryParse(blockGuid, out var guid))
            {
                return guid;
            }
            
            Debug.LogError($"InvalidGuid {blockGuid} {blockPrefab.name}");
            return Guid.Empty;
        }
    }
    
    
    
    public class BlockObjectInfo
    {
        public readonly BlockMasterElement BlockMasterElement;
        public readonly GameObject BlockObject;
        
        public BlockObjectInfo(GameObject blockObject, BlockMasterElement blockMasterElement)
        {
            BlockObject = blockObject;
            BlockMasterElement = blockMasterElement;
        }
    }
}