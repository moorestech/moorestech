using System;
using System.Collections.Generic;
using Client.Game.Block;
using Client.Game.Context;
using Cysharp.Threading.Tasks;
using Game.World.Interface.DataStore;
using MainGame.ModLoader.Glb;
using MainGame.UnityView.Block;
using UnityEngine;

namespace MainGame.UnityView.Chunk
{
    public class ChunkBlockGameObjectDataStore : MonoBehaviour
    {
        private readonly Dictionary<Vector3Int, BlockGameObject> _blockObjectsDictionary = new();

        public IReadOnlyDictionary<Vector3Int, BlockGameObject> BlockGameObjectDictionary => _blockObjectsDictionary;

        public event Action<BlockGameObject> OnPlaceBlock;
        

        public BlockGameObject GetBlockGameObject(Vector3Int position)
        {
            return _blockObjectsDictionary.TryGetValue(position, out var value) ? value : null;
        }

        public bool ContainsBlockGameObject(Vector3Int position)
        {
            return _blockObjectsDictionary.ContainsKey(position);
        }


        public void PlaceBlock(Vector3Int blockPosition, int blockId, BlockDirection blockDirection)
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
            var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(blockPosition, blockDirection, blockId);
            var rot = blockDirection.GetRotation();
            
            var block = MoorestechContext.BlockGameObjectContainer.CreateBlock(blockId, pos, rot,transform, blockPosition);
            //設置アニメーションを再生
            block.PlayPlaceAnimation().Forget();
            
            _blockObjectsDictionary.Add(blockPosition, block);
            OnPlaceBlock?.Invoke(block);
        }

        public void RemoveBlock(Vector3Int blockPosition)
        {
            //すでにブロックが置かれている時のみブロックを削除する
            if (!_blockObjectsDictionary.ContainsKey(blockPosition)) return;

            _blockObjectsDictionary[blockPosition].DestroyBlock().Forget();
            _blockObjectsDictionary.Remove(blockPosition);
        }
    }
}