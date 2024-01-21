using System;
using System.Collections.Generic;
using Game.Block.Config;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;
using Constant;
using MainGame.ModLoader.Glb;
using MainGame.UnityView.Block;
using SinglePlay;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.Chunk
{
    public class ChunkBlockGameObjectDataStore : MonoBehaviour
    {
        private readonly Dictionary<Vector2Int, BlockGameObject> _blockObjectsDictionary = new();
        private BlockGameObjectContainer _blockGameObjectContainer;
        private IBlockConfig _blockConfig;

        public IReadOnlyDictionary<Vector2Int, BlockGameObject> BlockGameObjectDictionary => _blockObjectsDictionary;

        public event Action<BlockGameObject> OnPlaceBlock;

        [Inject]
        public void Construct(BlockGameObjectContainer blockGameObjectContainer,SinglePlayInterface singlePlayInterface)
        {
            _blockGameObjectContainer = blockGameObjectContainer;
            _blockConfig = singlePlayInterface.BlockConfig;
        }


        public BlockGameObject GetBlockGameObject(Vector2Int position)
        {
            return _blockObjectsDictionary.ContainsKey(position) ? _blockObjectsDictionary[position] : null;
        }

        public bool ContainsBlockGameObject(Vector2Int position)
        {
            return _blockObjectsDictionary.ContainsKey(position);
        }


        public void GameObjectBlockPlace(Vector2Int blockPosition, int blockId, BlockDirection blockDirection)
        {
            //すでにブロックがあり、IDが違う場合は新しいブロックに置き換えるために削除する
            if (_blockObjectsDictionary.ContainsKey(blockPosition))
            {
                //IDが同じ時は再設置の必要がないため処理を終了
                if (_blockObjectsDictionary[blockPosition].BlockId == blockId) return;

                //IDが違うため削除
                Destroy(_blockObjectsDictionary[blockPosition].gameObject);
                _blockObjectsDictionary.Remove(blockPosition);
            }


            //新しいブロックを設置
            var blockConfig = _blockConfig.GetBlockConfig(blockId);
            var (pos,rot,scale) = SlopeBlockPlaceSystem.GetSlopeBeltConveyorTransform(blockPosition, blockDirection,blockConfig.BlockSize);

            var blockType = blockConfig.Type;
            if (!BlockSlopeDeformationType.IsDeformation(blockType))
            {
                rot = BlockDirectionAngle.GetRotation(blockDirection);
                scale = Vector3.one;
            }
            
            var block = _blockGameObjectContainer.CreateBlock(blockId, pos, rot,scale, transform, blockPosition);

            _blockObjectsDictionary.Add(blockPosition, block);
            OnPlaceBlock?.Invoke(block);
        }

        public void GameObjectBlockRemove(Vector2Int blockPosition)
        {
            //すでにブロックが置かれている時のみブロックを削除する
            if (!_blockObjectsDictionary.ContainsKey(blockPosition)) return;

            Destroy(_blockObjectsDictionary[blockPosition].gameObject);
            _blockObjectsDictionary.Remove(blockPosition);
        }
    }
}