using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Block;
using Game.Block.Interface.BlockConfig;
using MainGame.ModLoader;
using MainGame.ModLoader.Glb;
using MainGame.UnityView.Block.StateChange;
using SinglePlay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MainGame.UnityView.Block
{
    /// <summary>
    ///     Unityに表示されるブロックの実際のGameObjectを管理するクラス
    ///     最初にブロックを生成しておき、必要なブロックを複製するためのクラス
    /// </summary>
    public class BlockGameObjectFactory
    {
        private readonly BlockGameObject _nothingIndexBlockObject;
        private readonly IBlockConfig _blockConfig;
        private List<BlockData> _blockObjectList;

        public BlockGameObjectFactory(ModDirectory modDirectory, BlockGameObject nothingIndexBlockObject, SinglePlayInterface singlePlayInterface)
        {
            Init(modDirectory, singlePlayInterface).Forget();
            _nothingIndexBlockObject = nothingIndexBlockObject;
            _blockConfig = singlePlayInterface.BlockConfig;
        }

        public event Action OnLoadFinished;

        private async UniTask Init(ModDirectory modDirectory, SinglePlayInterface singlePlayInterface)
        {
            //await BlockGlbLoader.GetBlockLoaderは同期処理になっているため、ここで1フレーム待って他のイベントが追加されるのを待つ
            await UniTask.WaitForFixedUpdate();

            _blockObjectList = await BlockGlbLoader.GetBlockLoader(modDirectory.Directory, singlePlayInterface);
            OnLoadFinished?.Invoke();
        }

        public BlockGameObject CreateBlock(int blockId, Vector3 position, Quaternion rotation,Vector3 scale ,Transform parent, Vector2Int blockPosition)
        {
            //ブロックIDは1から始まるので、オブジェクトのリストインデックスマイナス１する
            var blockConfigIndex = blockId - 1;
            var blockConfig = _blockConfig.GetBlockConfig(blockId);

            if (blockConfigIndex < 0 || _blockObjectList.Count <= blockConfigIndex)
            {
                //ブロックIDがないのでない時用のブロックを作る
                Debug.LogWarning("Not Id " + blockConfigIndex);
                var nothing = Object.Instantiate(_nothingIndexBlockObject, position, rotation, parent);
                nothing.Initialize(blockConfig, blockPosition, new NullBlockStateChangeProcessor());
                return nothing.GetComponent<BlockGameObject>();
            }


            //ブロックの作成とセットアップをして返す
            var blockType = _blockObjectList[blockConfigIndex].Type;

            var block = Object.Instantiate(_blockObjectList[blockConfigIndex].BlockObject, position, rotation, parent);
            block.transform.localScale = scale;
            
            block.gameObject.SetActive(true);
            block.Initialize(blockConfig, blockPosition, GetBlockStateChangeProcessor(block, blockType));

            //ブロックが開けるものの場合はそのコンポーネントを付与する
            if (IsOpenableInventory(blockType)) block.gameObject.AddComponent<OpenableInventoryBlock>();
            return block.GetComponent<BlockGameObject>();
        }

        public string GetName(int index)
        {
            if (_blockObjectList.Count <= index) return "Null";

            return _blockObjectList[index].Name;
        }

        /// <summary>
        ///     todo ブロックコンフィグのロードのdynamicを辞める時に一緒にこれに対応したシステムを構築する
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool IsOpenableInventory(string type)
        {
            return type is VanillaBlockType.Chest or VanillaBlockType.Generator or VanillaBlockType.Miner or VanillaBlockType.Machine;
        }


        /// <summary>
        ///     どのブロックステートプロセッサーを使うかを決める
        /// </summary>
        private IBlockStateChangeProcessor GetBlockStateChangeProcessor(BlockGameObject block, string blockType)
        {
            return blockType switch
            {
                VanillaBlockType.Machine => block.gameObject.AddComponent<MachineBlockStateChangeProcessor>(),
                _ => new NullBlockStateChangeProcessor()
            };
        }
    }
}