using System;
using System.Collections.Generic;
using System.Threading;
using Core.Block.Config;
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

        public BlockGameObject CreateBlock(int blockId,Vector3 position,Quaternion rotation,Transform parent)
        {
            if (blockId < 0 || _blockObjectList.Count <= blockId)
            {
                //ブロックIDがないのでない時用のブロックを作る
                Debug.LogWarning("Not Id " + blockId);
                var nothing = Object.Instantiate(_nothingIndexBlockObject,position,rotation,parent);
                nothing.SetUp(blockId);
                return nothing.GetComponent<BlockGameObject>();
            }
            
            //ブロックIDは1から始まるので、オブジェクトのリストインデックスマイナス１する
            var blockObjectIndex = blockId - 1;

            //ブロックの作成とセットアップをして返す
            var block = Object.Instantiate(_blockObjectList[blockObjectIndex].BlockObject,position,rotation,parent);
            block.gameObject.SetActive(true);
            block.SetUp(blockId);
            //ブロックが開けるものの場合はそのコンポーネントを付与する
            if (IsOpenableInventory(_blockObjectList[blockObjectIndex].Type))
            {
                block.gameObject.AddComponent<OpenableInventoryBlock>();
            }
            return block.GetComponent<BlockGameObject>();
        }
        public string GetName(int index)
        {
            if (_blockObjectList.Count <= index)
            {
                return "Null";
            }

            return _blockObjectList[index].Name;
        }

        /// <summary>
        /// todo ブロックコンフィグのロードのdynamicを辞める時に一緒にこれに対応したシステムを構築する
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool IsOpenableInventory(string type)
        {
            return type is VanillaBlockType.Chest or VanillaBlockType.Generator or VanillaBlockType.Miner or VanillaBlockType.Machine;
        }
    }
    
}