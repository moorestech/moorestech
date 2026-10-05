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
    ///     撤去ブロック1つ。空いていれば記録した生成値で再設置
    ///     One removed block; re-placed with captured params if free
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
            // 生成値が未取得なら再設置を記録しない
            // Do not record a re-place while creation data is missing
            var createParams = new List<BlockCreateParam>();
            foreach (var source in block.GetComponentsInChildren<IBlockRecreateParamSource>(true))
            {
                if (!source.TryGetBlockRecreateParams(out var sourceParams))
                {
                    collector.AddUnrecordableBlock(block.BlockPosInfo.OriginalPos, block.BlockPosInfo.BlockDirection, block.BlockId, $"block at {block.BlockPosInfo.OriginalPos}: {source.GetType().Name} has no recreate params");
                    return;
                }
                createParams.AddRange(sourceParams);
            }

            collector.Add(new RemovedBlock(block.BlockPosInfo.OriginalPos, block.BlockId, block.BlockPosInfo.BlockDirection, createParams.ToArray()));
        }

        public object RestoreKey => _position;

        public BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
            // 占有判定はブロックIDから寸法を解決する
            // Occupancy resolves the footprint size from the block id
            var state = occupancy.GetOccupancy(_position, _direction, _blockId);
            if (state == BlockFootprintOccupancy.SameBlockPresent)
            {
                Debug.Log($"[RemovalRestore] block already present at {_position}");
                return BlockRestoreOutcome.AlreadyPresent;
            }
            if (state == BlockFootprintOccupancy.OtherBlock)
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
