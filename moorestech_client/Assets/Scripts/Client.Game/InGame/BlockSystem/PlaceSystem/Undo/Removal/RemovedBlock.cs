using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Removal;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去したブロック1つ。占有範囲が空いていれば記録した生成パラメータで再設置する
    ///     One removed block; re-placed with captured creation parameters when its footprint is free
    /// </summary>
    public class RemovedBlock : IRemovedObject
    {
        private readonly Vector3Int _position;
        private readonly BlockId _blockId;
        private readonly BlockDirection _direction;
        private readonly BlockCreateParam[] _createParams;

        public RemovedBlock(Vector3Int position, BlockId blockId, BlockDirection direction, BlockCreateParam[] createParams)
        {
            _position = position;
            _blockId = blockId;
            _direction = direction;
            _createParams = createParams;
        }

        public static void Capture(BlockGameObject block, RemovedObjectCollector collector)
        {
            // 部品が必要な生成値をまだ持たない場合は、不完全な再設置を記録しない
            // Do not record an incomplete replacement when a component lacks required creation data
            var createParams = new List<BlockCreateParam>();
            foreach (var source in block.GetComponentsInChildren<IBlockRecreateParamSource>(true))
            {
                if (!source.TryGetBlockRecreateParams(out var sourceParams))
                {
                    collector.AddUnrecordable($"block at {block.BlockPosInfo.OriginalPos}: {source.GetType().Name} has no recreate params");
                    return;
                }
                createParams.AddRange(sourceParams);
            }

            collector.Add(new RemovedBlock(block.BlockPosInfo.OriginalPos, block.BlockId, block.BlockPosInfo.BlockDirection, createParams.ToArray()));
        }

        public object RestoreKey => _position;

        public BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
            // 占有中のセルは再設置しない（撤去失敗・他者設置セルを除外）。理由はログへ残し、件数はプレイヤー通知へ回る
            // Skip occupied cells (failed removals or rebuilt cells); log why, and the count goes to the player notification
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(_blockId).BlockSize;
            if (occupancy.IsOverlapPositionInfo(new BlockPositionInfo(_position, _direction, blockSize)))
            {
                Debug.LogWarning($"[RemovalRestore] skip re-place: footprint occupied at {_position}");
                return BlockRestoreOutcome.SkippedOccupied;
            }

            placeInfos.Add(new PlaceInfo
            {
                Position = _position,
                Direction = _direction,
                VerticalDirection = ToVerticalDirection(_direction),
                BlockId = _blockId,
                Placeable = true,
                CreateParams = _createParams,
            });
            return BlockRestoreOutcome.Appended;

            #region Internal

            BlockVerticalDirection ToVerticalDirection(BlockDirection direction)
            {
                return direction switch
                {
                    BlockDirection.UpNorth or BlockDirection.UpEast or BlockDirection.UpSouth or BlockDirection.UpWest => BlockVerticalDirection.Up,
                    BlockDirection.DownNorth or BlockDirection.DownEast or BlockDirection.DownSouth or BlockDirection.DownWest => BlockVerticalDirection.Down,
                    _ => BlockVerticalDirection.Horizontal,
                };
            }

            #endregion
        }

        public void SendConnectionRestore(IRemovalRestoreSender sender)
        {
        }
    }
}
