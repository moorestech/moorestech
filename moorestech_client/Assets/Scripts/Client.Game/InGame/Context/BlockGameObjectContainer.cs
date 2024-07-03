using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Define;
using Client.Mod.Glb;
using Cysharp.Threading.Tasks;
using Game.Block;
using Game.Block.Interface;
using Game.Context;
using UnityEngine;

namespace Client.Game.InGame.Context
{
    /// <summary>
    ///     Unityに表示されるブロックの実際のGameObjectを管理するクラス
    ///     最初にブロックを生成しておき、必要なブロックを複製するためのクラス
    /// </summary>
    public class BlockGameObjectContainer
    {
        private readonly List<BlockData> _blockObjectList;
        private readonly BlockGameObject _nothingIndexBlockObject;
        
        public BlockGameObjectContainer(BlockGameObject nothingIndexBlockObject, List<BlockData> blockObjectList)
        {
            _nothingIndexBlockObject = nothingIndexBlockObject;
            _blockObjectList = blockObjectList;
        }
        
        public static async UniTask<BlockGameObjectContainer> CreateAndLoadBlockGameObjectContainer(string modDirectory, BlockPrefabContainer blockPrefabContainer, BlockGameObject nothingIndexBlockObject)
        {
            var blockObjectList = await BlockGlbLoader.GetBlockLoader(modDirectory);
            
            var prefabBlockData = blockPrefabContainer.GetBlockDataList();
            blockObjectList.AddRange(prefabBlockData);
            
            //IDでソート
            blockObjectList.Sort((a, b) => a.BlockConfig.BlockId - b.BlockConfig.BlockId);
            
            return new BlockGameObjectContainer(nothingIndexBlockObject, blockObjectList);
        }
        
        
        public BlockGameObject CreateBlock(int blockId, Vector3 position, Quaternion rotation, Transform parent, Vector3Int blockPosition, BlockDirection direction)
        {
            //ブロックIDは1から始まるので、オブジェクトのリストインデックスマイナス１する
            var blockConfigIndex = blockId - 1;
            var blockConfig = ServerContext.BlockConfig.GetBlockConfig(blockId);
            
            if (blockConfigIndex < 0 || _blockObjectList.Count <= blockConfigIndex)
            {
                //ブロックIDがないのでない時用のブロックを作る
                Debug.LogError("Not Id " + blockConfigIndex);
                var nothing = Object.Instantiate(_nothingIndexBlockObject, position, rotation, parent);
                var nothingBlockPosInfo = new BlockPositionInfo(blockPosition, direction, Vector3Int.one);
                nothing.Initialize(blockConfig, nothingBlockPosInfo, new NullBlockStateChangeProcessor());
                return nothing.GetComponent<BlockGameObject>();
            }
            
            //ブロックの作成とセットアップをして返す
            var block = Object.Instantiate(_blockObjectList[blockConfigIndex].BlockObject, position, rotation, parent);
            
            //コンポーネントの設定
            var blockObj = block.AddComponent<BlockGameObject>();
            //子要素のコンポーネントの設定
            foreach (var mesh in blockObj.GetComponentsInChildren<MeshRenderer>())
            {
                mesh.gameObject.AddComponent<BlockGameObjectChild>();
                mesh.gameObject.AddComponent<MeshCollider>();
            }
            
            var blockType = _blockObjectList[blockConfigIndex].Type;
            blockObj.gameObject.SetActive(true);
            var posInfo = new BlockPositionInfo(blockPosition, direction, blockConfig.BlockSize);
            blockObj.Initialize(blockConfig, posInfo, GetBlockStateChangeProcessor(blockObj, blockType));
            
            //ブロックが開けるものの場合はそのコンポーネントを付与する
            if (IsOpenableInventory(blockType)) block.gameObject.AddComponent<OpenableInventoryBlock>();
            return block.GetComponent<BlockGameObject>();
        }
        
        public BlockPreviewObject CreatePreviewBlock(int blockId)
        {
            var blockConfigIndex = blockId - 1;
            if (blockConfigIndex < 0 || _blockObjectList.Count <= blockConfigIndex) return null;
            
            //ブロックの作成とセットアップをして返す
            var block = Object.Instantiate(_blockObjectList[blockConfigIndex].BlockObject, Vector3.zero, Quaternion.identity);
            block.SetActive(true);
            
            var previewGameObject = block.AddComponent<BlockPreviewObject>();
            previewGameObject.Initialize(
                ServerContext.BlockConfig.GetBlockConfig(blockId),
                Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial)
            );
            return previewGameObject;
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
            return type is VanillaBlockType.Chest or VanillaBlockType.ElectricGenerator or VanillaBlockType.ElectricMiner or VanillaBlockType.ElectricMachine;
        }
        
        
        /// <summary>
        ///     どのブロックステートプロセッサーを使うかを決める
        /// </summary>
        private IBlockStateChangeProcessor GetBlockStateChangeProcessor(BlockGameObject block, string blockType)
        {
            if (block.TryGetComponent<IBlockStateChangeProcessor>(out var stateChangeProcessor)) return stateChangeProcessor;
            
            return blockType switch
            {
                VanillaBlockType.ElectricMiner => block.gameObject.AddComponent<MachineBlockStateChangeProcessor>(),
                _ => new NullBlockStateChangeProcessor(),
            };
        }
    }
}