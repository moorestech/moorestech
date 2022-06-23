using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MainGame.ModLoader;
using MainGame.ModLoader.Glb;
using SinglePlay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MainGame.UnityView.Block
{
    public class BlockObjects
    {
        public event Action OnLoadFinished;
        private List<BlockData> _blockObjectList;
        private readonly BlockGameObject _nothingIndexBlockObject;

        public BlockObjects(ModDirectory modDirectory,BlockGameObject nothingIndexBlockObject,SinglePlayInterface singlePlayInterface)
        {
            Init(modDirectory,singlePlayInterface).Forget();
            _nothingIndexBlockObject = nothingIndexBlockObject;
        }
        
        private async UniTask Init(ModDirectory modDirectory,SinglePlayInterface singlePlayInterface)
        {
            //await BlockGlbLoader.GetBlockLoaderは同期処理になっているため、ここで1フレーム待って他のイベントが追加されるのを待つ
            await UniTask.WaitForFixedUpdate();
            
            _blockObjectList = await BlockGlbLoader.GetBlockLoader(modDirectory.Directory, singlePlayInterface);
            OnLoadFinished?.Invoke();
        }

        public BlockGameObject CreateBlock(int blockId)
        {
            //block idは1から始まるのでマイナス１する
            blockId--;
            if (blockId < 0 || _blockObjectList.Count <= blockId)
            {
                Debug.LogWarning("Not Id " + blockId);
                return Object.Instantiate(_nothingIndexBlockObject);
            }

            var block = Object.Instantiate(_blockObjectList[blockId].BlockObject);
            block.gameObject.SetActive(true);
            return block;
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