using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using UnityEngine;

namespace Client.Game.InGame.Chunk
{
    public class BlockGameObjectDataStore : MonoBehaviour
    {
        private readonly Dictionary<Vector3Int, BlockGameObject> _blockObjectsDictionary = new();

        public IReadOnlyDictionary<Vector3Int, BlockGameObject> BlockGameObjectDictionary => _blockObjectsDictionary;

        public event Action<BlockGameObject> OnPlaceBlock;


        public BlockGameObject GetBlockGameObject(Vector3Int position)
        {
            return _blockObjectsDictionary.GetValueOrDefault(position);
        }

        public bool ContainsBlockGameObject(Vector3Int position)
        {
            return _blockObjectsDictionary.ContainsKey(position);
        }


        public void PlaceBlock(Vector3Int blockPosition, int blockId, BlockDirection blockDirection)
        {
            //すでにブロックがあり、IDが違う場合は新しいブロックに置き換えるために削除する
            var blockConfig = ServerContext.BlockConfig;
            var boundingBox = BlockPositionInfo.GetBlockBoundingBox(blockPosition, blockDirection, blockConfig.GetBlockConfig(blockId).BlockSize);
            
            foreach (var position in boundingBox)
            {
                if (_blockObjectsDictionary.ContainsKey(position))
                {
                    // //IDが同じかつ全く同じ位置にあるオブジェクトの場合は再設置の必要がないため処理を終了
                    if (_blockObjectsDictionary[position].BlockId == blockId && _blockObjectsDictionary[position].BlockPosition == blockPosition)
                    {
                        return;
                    }
                    
                    //IDが違うため削除
                    RemoveBlock(position);
                }
            }

            //新しいブロックを設置
            var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(blockPosition, blockDirection, blockId);
            var rot = blockDirection.GetRotation();

            var block = MoorestechContext.BlockGameObjectContainer.CreateBlock(
                blockId, 
                pos, 
                rot,
                transform,
                blockPosition, 
                blockDirection
            );
            
            //設置アニメーションを再生
            block.PlayPlaceAnimation().Forget();
            
            foreach (var position in boundingBox)
            {
                _blockObjectsDictionary[position] = block;   
            }
            
            OnPlaceBlock?.Invoke(block);
        }

        public void RemoveBlock(Vector3Int blockPosition)
        {
            //すでにブロックが置かれている時のみブロックを削除する
            if (!_blockObjectsDictionary.ContainsKey(blockPosition)) return;
            
            var block = _blockObjectsDictionary[blockPosition];
            var boundingBox = BlockPositionInfo.GetBlockBoundingBox(block.BlockPosition, block.BlockDirection, block.BlockConfig.BlockSize);
            block.DestroyBlock().Forget();
            foreach (var position in boundingBox)
            {
                _blockObjectsDictionary.Remove(position);
            }
        }
    }
}
