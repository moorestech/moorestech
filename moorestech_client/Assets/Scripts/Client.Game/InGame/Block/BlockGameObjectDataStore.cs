using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using CommandForgeGenerator.Command;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockGameObjectDataStore : MonoBehaviour, IBlockObjectControl
    {
        public IReadOnlyDictionary<Vector3Int, BlockGameObject> BlockGameObjectDictionary => _blockObjectsDictionary;
        private readonly Dictionary<Vector3Int, BlockGameObject> _blockObjectsDictionary = new();

        // BlockInstanceIdで検索用の辞書
        // Dictionary for searching by BlockInstanceId
        public IReadOnlyDictionary<BlockInstanceId, BlockGameObject> BlockGameObjectByInstanceIdDictionary => _blockObjectsByInstanceIdDictionary;
        private readonly Dictionary<BlockInstanceId, BlockGameObject> _blockObjectsByInstanceIdDictionary = new();

        public IObservable<BlockGameObject> OnBlockPlaced => _onBlockPlaced;
        private readonly Subject<BlockGameObject> _onBlockPlaced = new();
        
        public IObservable<Vector3Int> OnBlockRemoved => _onBlockRemoved;
        private readonly Subject<Vector3Int> _onBlockRemoved = new();
        
        
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

        public bool TryGetBlockGameObject(BlockInstanceId blockInstanceId, out BlockGameObject blockGameObject)
        {
            return _blockObjectsByInstanceIdDictionary.TryGetValue(blockInstanceId, out blockGameObject);
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void PlaceBlock(Vector3Int blockPosition, BlockId blockId, BlockDirection blockDirection, BlockInstanceId blockInstanceId)
        {
            // すでにブロックがあり、IDが違う場合は新しいブロックに置き換えるために削除する
            // If a block already exists and the ID is different, delete it to replace with a new block
            if (_blockObjectsDictionary.ContainsKey(blockPosition))
            {
                // IDが同じ時は再設置の必要がないため処理を終了
                // If the ID is the same, no need to re-place, so exit
                if (_blockObjectsDictionary[blockPosition].BlockId == blockId)
                {
                    return;
                }

                // IDが違うため削除（BlockInstanceId辞書からも削除）
                // Delete because the ID is different (also remove from BlockInstanceId dictionary)
                var oldBlock = _blockObjectsDictionary[blockPosition];
                _blockObjectsByInstanceIdDictionary.Remove(oldBlock.BlockInstanceId);
                Destroy(oldBlock.gameObject);
                _blockObjectsDictionary.Remove(blockPosition);
            }

            // 新しいブロックを設置
            // Place a new block
            var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(blockPosition, blockDirection, blockId);
            var rot = blockDirection.GetRotation();

            var block = ClientContext.BlockGameObjectContainer.CreateBlock(blockId, pos, rot, transform, blockPosition, blockDirection, blockInstanceId);
            // 設置アニメーションを再生
            // Play place animation
            block.PlayPlaceAnimation().Forget();
            
            _blockObjectsDictionary.Add(blockPosition, block);
            _blockObjectsByInstanceIdDictionary.Add(blockInstanceId, block);
            _onBlockPlaced.OnNext(block);
        }
        
        public void RemoveBlock(Vector3Int blockPosition)
        {
            // すでにブロックが置かれている時のみブロックを削除する
            // Only delete the block if it already exists
            if (!_blockObjectsDictionary.ContainsKey(blockPosition)) return;

            var block = _blockObjectsDictionary[blockPosition];
            block.DestroyBlock().Forget();
            _blockObjectsByInstanceIdDictionary.Remove(block.BlockInstanceId);
            _blockObjectsDictionary.Remove(blockPosition);
            
            // ブロック削除イベントを発行
            // Fire block removal event
            _onBlockRemoved.OnNext(blockPosition);
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