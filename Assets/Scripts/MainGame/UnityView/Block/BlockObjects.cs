using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.ModLoader;
using MainGame.ModLoader.Glb;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.Util;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class BlockObjects
    {
        private List<BlockData> _blockObjectList;
        private readonly BlockGameObject _nothingIndexBlockObject;

        public BlockObjects(ModDirectory modDirectory,BlockGameObject nothingIndexBlockObject,SinglePlayInterface singlePlayInterface,IInitialViewLoadingDetector initialViewLoadingDetector)
        {
            Init(modDirectory,singlePlayInterface,initialViewLoadingDetector).Forget();
            _nothingIndexBlockObject = nothingIndexBlockObject;
        }
        
        private async UniTask Init(ModDirectory modDirectory,SinglePlayInterface singlePlayInterface,IInitialViewLoadingDetector initialViewLoadingDetector)
        {
            _blockObjectList = await BlockGlbLoader.GetBlockLoader(modDirectory.Directory, singlePlayInterface);
            initialViewLoadingDetector.FinishBlockModelLoading();
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

            return _blockObjectList[index].Name;
        }
    }
    
}