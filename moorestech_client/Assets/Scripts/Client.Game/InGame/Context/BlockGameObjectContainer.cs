using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;
using Object = UnityEngine.Object;


namespace Client.Game.InGame.Context
{
    /// <summary>
    ///     Unityに表示されるブロックの実際のGameObjectを管理するクラス
    ///     最初にブロックを生成しておき、必要なブロックを複製するためのクラス
    /// </summary>
    public class BlockGameObjectContainer
    {
        public IReadOnlyDictionary<BlockId, BlockObjectInfo> BlockObjects => _blockObjects;
        private readonly Dictionary<BlockId, BlockObjectInfo> _blockObjects;
        private readonly BlockGameObject _missingBlockIdObject;
        
        public BlockGameObjectContainer(BlockGameObject missingBlockIdObject, Dictionary<BlockId, BlockObjectInfo> blockObjects)
        {
            _missingBlockIdObject = missingBlockIdObject;
            _blockObjects = blockObjects;
        }
        
        public static async UniTask<BlockGameObjectContainer> CreateAndLoadBlockGameObjectContainer(BlockGameObject missingBlockIdObject)
        {
            var blocks = new Dictionary<BlockId, BlockObjectInfo>();
            var tasks = new List<UniTask<BlockObjectInfo>>();
            foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
            {
                tasks.Add(LoadBlockGameObject(blockId));
            }
            
            var results = await UniTask.WhenAll(tasks);
            foreach (var result in results)
            {
                if (result == null) continue;
                blocks.Add(result.BlockId, result);
            }
            
            return new BlockGameObjectContainer(missingBlockIdObject, blocks);
        }
        
        private static async UniTask<BlockObjectInfo> LoadBlockGameObject(BlockId blockId)
        {
            var masterElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var path = masterElement.BlockPrefabAddressablesPath;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"ブロックのパスの設定がありません。 Name:{masterElement.Name} GUID:{masterElement.BlockGuid}");
                return null;
            }
            
            var blockAsset = await AddressableLoader.LoadAsync<GameObject>(path);
            if (blockAsset == null)
            {
                //TODO ログ基盤に入れる
                Debug.LogError($"ブロックのアセットが見つかりません。Name:{masterElement.Name} Path:{masterElement.BlockPrefabAddressablesPath} GUID:{masterElement.BlockGuid} ");
                return null;
            }
            
            return new BlockObjectInfo(blockId, blockAsset.Asset, masterElement);
        }
        
        /// <summary>
        /// 必要なセットアップを行なってブロックオブジェクトを生成する
        /// Create a block object with the necessary setup
        /// </summary>
        public BlockGameObject CreateBlock(BlockId blockId, Vector3 position, Quaternion rotation, Transform parent, Vector3Int blockPosition, BlockDirection direction, BlockInstanceId blockInstanceId)
        {
            if (!_blockObjects.TryGetValue(blockId, out var blockObjectInfo))
            {
                // ブロックIDがない時用のブロックを作る
                // Create a block for when the block ID does not exist
                return CreateMissingIdBlock();
            }

            // ブロックの作成とセットアップをして返す
            // Create and set up the block, then return
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
                // ブロックIDは1から始まるので、オブジェクトのリストインデックスマイナス１する
                // Block ID starts from 1, so subtract 1 from the object list index
                var blockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);

                // ブロックの作成とセットアップをして返す
                // Create and set up the block, then return
                var block = Object.Instantiate(blockObjectInfo.BlockObjectPrefab, position, rotation, parent);

                // コンポーネントの設定
                // Set up components
                if (!block.TryGetComponent(out BlockGameObject blockObj))
                {
                    blockObj = block.AddComponent<BlockGameObject>();
                }

                // 子要素のコンポーネントの設定
                // Set up child element components
                foreach (var mesh in blockObj.GetComponentsInChildren<MeshRenderer>())
                {
                    mesh.gameObject.AddComponent<BlockGameObjectChild>();
                    mesh.gameObject.AddComponent<MeshCollider>();
                }

                blockObj.gameObject.SetActive(true);
                var blockType = blockMasterElement.BlockType;
                // ブロックが開けるものの場合はそのコンポーネントを付与する
                // If the block is openable, add the corresponding component
                if (blockMasterElement.IsBlockOpenable()) block.gameObject.AddComponent<OpenableInventoryBlock>();
                // 機械の場合はそのプロセッサを付与する
                // If it's a machine, add the corresponding processor
                if (IsCommonMachine(blockType)) block.gameObject.AddComponent<CommonMachineBlockStateChangeProcessor>();

                // 初期化
                // Initialize
                var posInfo = new BlockPositionInfo(blockPosition, direction, blockMasterElement.BlockSize);
                blockObj.Initialize(blockMasterElement, posInfo, blockInstanceId);

                return blockObj;
            }

            bool IsCommonMachine(string type)
            {
                return type is
                    BlockTypeConst.ElectricGenerator or
                    BlockTypeConst.ElectricMiner or
                    BlockTypeConst.ElectricMachine or
                    BlockTypeConst.GearMachine or
                    BlockTypeConst.GearMiner;
            }

            #endregion
        }
        
        
        /// <summary>
        /// GameObjectの生成だけを行う
        /// Create only the GameObject
        /// </summary>
        public GameObject CreateBlockGameObject(BlockId blockId, Vector3 position, Quaternion rotation)
        {
            if (!_blockObjects.TryGetValue(blockId, out var blockObjectInfo))
            {
                var blockMasterElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                throw new System.Exception($"ブロックの登録がありません。Name:{blockMasterElement.Name} GUID:{blockMasterElement.BlockGuid}");
            }
            
            //ブロックの作成とセットアップをして返す
            return Object.Instantiate(blockObjectInfo.BlockObjectPrefab, position, rotation);
        }
    }
    
    public class BlockObjectInfo
    {
        public readonly BlockId BlockId;
        public readonly BlockMasterElement BlockMasterElement;
        public readonly GameObject BlockObjectPrefab;
        
        public BlockObjectInfo(BlockId blockId, GameObject blockObjectPrefab, BlockMasterElement blockMasterElement)
        {
            BlockObjectPrefab = blockObjectPrefab;
            BlockMasterElement = blockMasterElement;
            BlockId = blockId;
        }
    }
}