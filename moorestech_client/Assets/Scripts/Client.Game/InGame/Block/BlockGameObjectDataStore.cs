using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockGameObjectDataStore : MonoBehaviour
    {
        public IReadOnlyDictionary<Vector3Int, BlockGameObject> BlockGameObjectDictionary => _blockObjectsDictionary;
        private readonly Dictionary<Vector3Int, BlockGameObject> _blockObjectsDictionary = new();
        
        public IObservable<BlockGameObject> OnBlockPlaced => _onBlockPlaced;
        private readonly Subject<BlockGameObject> _onBlockPlaced = new();
        
        
        public BlockGameObject GetBlockGameObject(Vector3Int position)
        {
            return _blockObjectsDictionary.GetValueOrDefault(position);
        }
        
        public bool ContainsBlockGameObject(Vector3Int position)
        {
            return _blockObjectsDictionary.ContainsKey(position);
        }
        
        public bool TryGetBlockGameObject(Vector3Int position, out BlockGameObject blockGameObject)
        {
            return _blockObjectsDictionary.TryGetValue(position, out blockGameObject);
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void PlaceBlock(Vector3Int blockPosition, BlockId blockId, BlockDirection blockDirection)
        {
            //すでにブロックがあり、IDが違う場合は新しいブロックに置き換えるために削除する
            if (_blockObjectsDictionary.ContainsKey(blockPosition))
            {
                //IDが同じ時は再設置の必要がないため処理を終了
                if (_blockObjectsDictionary[blockPosition].BlockId == blockId)
                {
                    return;
                }
                
                //IDが違うため削除
                Destroy(_blockObjectsDictionary[blockPosition].gameObject);
                _blockObjectsDictionary.Remove(blockPosition);
            }
            
            //新しいブロックを設置
            var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(blockPosition, blockDirection, blockId);
            var rot = blockDirection.GetRotation();
            
            var block = ClientContext.BlockGameObjectContainer.CreateBlock(blockId, pos, rot, transform, blockPosition, blockDirection);
            //設置アニメーションを再生
            block.PlayPlaceAnimation().Forget();
            
            _blockObjectsDictionary.Add(blockPosition, block);
            _onBlockPlaced.OnNext(block);
        }
        
        public void RemoveBlock(Vector3Int blockPosition)
        {
            //すでにブロックが置かれている時のみブロックを削除する
            if (!_blockObjectsDictionary.ContainsKey(blockPosition)) return;
            
            _blockObjectsDictionary[blockPosition].DestroyBlock().Forget();
            _blockObjectsDictionary.Remove(blockPosition);
        }
        
        public bool IsOverlapPositionInfo(BlockPositionInfo target)
        {
            foreach (var block in _blockObjectsDictionary.Values)
                if (block.BlockPosInfo.IsOverlap(target))
                    return true;
            return false;
        }
    }
}