using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Define;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block;
using Game.Block.Interface;
using UnityEngine;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;


namespace Client.Game.InGame.Context
{
    /// <summary>
    ///     Unityに表示されるブロックの実際のGameObjectを管理するクラス
    ///     最初にブロックを生成しておき、必要なブロックを複製するためのクラス
    /// </summary>
    public class BlockGameObjectContainer
    {
        private readonly Dictionary<BlockId,BlockObjectInfo> _blockObjects;
        private readonly BlockGameObject _missingBlockIdObject;
        
        public BlockGameObjectContainer(BlockGameObject missingBlockIdObject, Dictionary<BlockId,BlockObjectInfo> blockObjects)
        {
            _missingBlockIdObject = missingBlockIdObject;
            _blockObjects = blockObjects;
        }
        
        public static async UniTask<BlockGameObjectContainer> CreateAndLoadBlockGameObjectContainer(BlockPrefabContainer blockPrefabContainer, BlockGameObject missingBlockIdObject)
        {
            // TODO アドレッサブルの対応
            var blockObjectList = blockPrefabContainer.GetBlockDataList();
            
            return new BlockGameObjectContainer(missingBlockIdObject, blockObjectList);
        }
        
        public BlockGameObject CreateBlock(BlockId blockId, Vector3 position, Quaternion rotation, Transform parent, Vector3Int blockPosition, BlockDirection direction)
        {
            if (!_blockObjects.TryGetValue(blockId, out var blockObjectInfo))
            {
                //ブロックIDがないのでない時用のブロックを作る
                return CreateMissingIdBlock();
            }
            
            //ブロックの作成とセットアップをして返す
            return CreateBlockObject();
            
            #region Internal
            
            BlockGameObject CreateMissingIdBlock()
            {
                var missingIdBlock = Object.Instantiate(_missingBlockIdObject, position, rotation, parent);
                var missingPosInfo = new BlockPositionInfo(blockPosition, direction, Vector3Int.one);
                
                //TODO nullのblock masterを入れる
                //missingIdBlock.Initialize(blockConfig, missingPosInfo, new NullBlockStateChangeProcessor());
                
                return missingIdBlock.GetComponent<BlockGameObject>();
            }
            
            BlockGameObject CreateBlockObject()
            {
                //ブロックIDは1から始まるので、オブジェクトのリストインデックスマイナス１する
                var blockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                
                //ブロックの作成とセットアップをして返す
                var block = Object.Instantiate(blockObjectInfo.BlockObject, position, rotation, parent);
                
                //コンポーネントの設定
                var blockObj = block.AddComponent<BlockGameObject>();
                //子要素のコンポーネントの設定
                foreach (var mesh in blockObj.GetComponentsInChildren<MeshRenderer>())
                {
                    mesh.gameObject.AddComponent<BlockGameObjectChild>();
                    mesh.gameObject.AddComponent<MeshCollider>();
                }
                
                blockObj.gameObject.SetActive(true);
                var posInfo = new BlockPositionInfo(blockPosition, direction, blockMasterElement.BlockSize);
                var blockType = blockMasterElement.BlockType;
                blockObj.Initialize(blockMasterElement, posInfo, GetBlockStateChangeProcessor(blockObj, blockType));
                
                //ブロックが開けるものの場合はそのコンポーネントを付与する
                if (IsOpenableInventory(blockType)) block.gameObject.AddComponent<OpenableInventoryBlock>();
                return block.GetComponent<BlockGameObject>();
            }
            
            #endregion
        }
        
        public BlockPreviewObject CreatePreviewBlock(BlockId blockId)
        {
            if (!_blockObjects.TryGetValue(blockId, out var blockObjectInfo))
            {
                var blockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                throw new System.Exception($"ブロックの登録がありません。Name:{blockMasterElement.Name} GUID:{blockMasterElement.BlockGuid}");
            }
            
            //ブロックの作成とセットアップをして返す
            var block = Object.Instantiate(blockObjectInfo.BlockObject, Vector3.zero, Quaternion.identity);
            block.SetActive(true);
            
            var previewGameObject = block.AddComponent<BlockPreviewObject>();
            previewGameObject.SetTriggerCollider(true);
            previewGameObject.Initialize(blockId);
            
            return previewGameObject;
        }
        
        /// <summary>
        ///     todo ブロックコンフィグのロードのdynamicを辞める時に一緒にこれに対応したシステムを構築する
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool IsOpenableInventory(string type)
        {
            return type is
                BlockTypeConst.Chest or
                BlockTypeConst.ElectricGenerator or
                BlockTypeConst.ElectricMiner or
                BlockTypeConst.ElectricMachine or
                BlockTypeConst.GearMachine or
                BlockTypeConst.GearMiner;
        }
        
        /// <summary>
        ///     どのブロックステートプロセッサーを使うかを決める
        /// </summary>
        private IBlockStateChangeProcessor GetBlockStateChangeProcessor(BlockGameObject block, string blockType)
        {
            if (block.TryGetComponent<IBlockStateChangeProcessor>(out var stateChangeProcessor)) return stateChangeProcessor;
            
            return blockType switch
            {
                BlockTypeConst.ElectricMiner => block.gameObject.AddComponent<MachineBlockStateChangeProcessor>(),
                _ => new NullBlockStateChangeProcessor(),
            };
        }
    }
}