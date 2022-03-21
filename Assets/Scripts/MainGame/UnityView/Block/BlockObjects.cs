using System.Collections.Generic;
using MainGame.UnityView.Chunk;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    [CreateAssetMenu(fileName = "BlockObjects", menuName = "BlockObjects", order = 0)]
    public class BlockObjects : ScriptableObject
    {
        [SerializeField] private List<Block> BlockObjectList;
        [SerializeField] private BlockGameObject NothingIndexBlockObject;

        public BlockGameObject GetBlock(int index)
        {
            if (BlockObjectList.Count <= index)
            {
                return NothingIndexBlockObject;
            }

            return BlockObjectList[index].BlockObject;
        }
    }
    [System.Serializable]
    public class Block{
        public BlockGameObject BlockObject;
        public string name;
    }
    
}