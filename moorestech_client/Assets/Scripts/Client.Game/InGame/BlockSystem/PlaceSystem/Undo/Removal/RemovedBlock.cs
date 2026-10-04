using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去したブロック1つ。復元は占有範囲が空いているときだけ再設置する（CreateParamsは復元不可のため空）
    ///     One removed block; restored only when its footprint is free (CreateParams cannot be restored, so empty)
    /// </summary>
    public class RemovedBlock : IRemovedObject
    {
        private readonly Vector3Int _position;
        private readonly BlockId _blockId;
        private readonly BlockDirection _direction;

        public RemovedBlock(Vector3Int position, BlockId blockId, BlockDirection direction)
        {
            _position = position;
            _blockId = blockId;
            _direction = direction;
        }

        public static RemovedBlock From(BlockGameObject block)
        {
            return new RemovedBlock(block.BlockPosInfo.OriginalPos, block.BlockId, block.BlockPosInfo.BlockDirection);
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
            });
            return BlockRestoreOutcome.Appended;
        }

        public void SendConnectionRestore(IRemovalRestoreSender sender)
        {
        }

        private static BlockVerticalDirection ToVerticalDirection(BlockDirection direction)
        {
            return direction switch
            {
                BlockDirection.UpNorth or BlockDirection.UpEast or BlockDirection.UpSouth or BlockDirection.UpWest => BlockVerticalDirection.Up,
                BlockDirection.DownNorth or BlockDirection.DownEast or BlockDirection.DownSouth or BlockDirection.DownWest => BlockVerticalDirection.Down,
                _ => BlockVerticalDirection.Horizontal,
            };
        }
    }
}
