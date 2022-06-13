using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.ModLoader;
using MainGame.ModLoader.Glb;
using MainGame.UnityView.Chunk;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class BlockObjects
    {
        private List<Block> _blockObjectList;
        private BlockGameObject _nothingIndexBlockObject;

        public BlockObjects(ModDirectory modDirectory,BlockGameObject nothingIndexBlockObject,SinglePlayInterface singlePlayInterface)
        {
            UniTask.Create(() =>
            {
                var a = await BlockGlbLoader.GetBlockLoader(modDirectory.Directory, singlePlayInterface);
            }).Forget();
            _nothingIndexBlockObject = nothingIndexBlockObject;
        }

        public BlockGameObject GetBlock(int index)
        {
            if (_blockObjectList.Count <= index)
            {
                return _nothingIndexBlockObject;
            }

            return _blockObjectList[index].BlockObject;
        }
        public string GetName(int index)
        {
            if (_blockObjectList.Count <= index)
            {
                return "Null";
            }

            return _blockObjectList[index].name;
        }
    }
    class Block{
        public BlockGameObject BlockObject;
        public string name;
    }
    
}