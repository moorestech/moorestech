using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.NearestSearch;
using CommandForgeGenerator.Command;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockGameObjectDataStore : MonoBehaviour, ISkitBlockObjectControl
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

        // ブロックGUID別の最近傍索引。全ブロック走査での最寄り探索を毎フレーム回さないため（前例: OutcropGameObjectDatastore）
        // Per-block-GUID nearest index, so no per-frame full scan is needed for nearest lookups (precedent: OutcropGameObjectDatastore)
        private readonly NearestTargetIndex<BlockGameObject> _nearestIndex = new();
        
        
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

        /// <summary>
        ///     指定GUIDのブロックのうち、その座標から最も近いものを返す。1つも無ければnull
        ///     Returns the block of that GUID nearest to the position, or null when there is none
        /// </summary>
        public BlockGameObject SearchNearestBlock(Guid blockGuid, Vector3 position)
        {
            return _nearestIndex.TrySearchNearest(blockGuid, position, out var block, out _) ? block : null;
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void PlaceBlock(Vector3Int blockPosition, BlockId blockId, BlockDirection blockDirection, BlockInstanceId blockInstanceId, bool playPlaceAnimation)
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
                DropFromNearestIndex(oldBlock);
                Destroy(oldBlock.gameObject);
                _blockObjectsDictionary.Remove(blockPosition);
            }

            // 新しいブロックを設置
            // Place a new block
            var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(blockPosition, blockDirection, blockId);
            var rot = blockDirection.GetRotation();

            var block = ClientContext.BlockGameObjectPrefabContainer.CreateBlock(blockId, pos, rot, transform, blockPosition, blockDirection, blockInstanceId);
            if (playPlaceAnimation)
            {
                // 単発の配置イベントだけ設置アニメーションを再生する
                // Play place animation only for single placement events
                block.PlayPlaceAnimation().Forget();
            }
            
            _blockObjectsDictionary.Add(blockPosition, block);
            _blockObjectsByInstanceIdDictionary.Add(blockInstanceId, block);
            _nearestIndex.Register(block.BlockMasterElement.BlockGuid, block);
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
            DropFromNearestIndex(block);
            _blockObjectsDictionary.Remove(blockPosition);
            
            // ブロック削除イベントを発行
            // Fire block removal event
            _onBlockRemoved.OnNext(blockPosition);
        }
        
        // 索引は墓標で外す。破棄アニメ中も探索対象へ戻らないよう、辞書から消すのと同じ瞬間に立てる
        // The index drops it as a tombstone, raised the moment it leaves the dictionary so a destroy animation never keeps it searchable
        private void DropFromNearestIndex(BlockGameObject block)
        {
            block.MarkUnsearchable();
            _nearestIndex.NotifyTargetUnsearchable(block.BlockMasterElement.BlockGuid);
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
