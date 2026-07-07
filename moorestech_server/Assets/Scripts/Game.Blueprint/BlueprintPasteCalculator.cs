using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Blueprint
{
    public static class BlueprintPasteCalculator
    {
        public static List<BlueprintPlacementElement> CalculatePlacements(BlueprintJsonObject blueprint, Vector3Int pasteAnchor, int rotationStep)
        {
            var result = new List<BlueprintPlacementElement>();
            foreach (var block in blueprint.Blocks)
            {
                // マスタに無いGuid（mod構成変更）はスキップして継続する
                // Skip blocks whose GUID no longer exists in the master data
                var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(block.BlockGuid);
                if (blockId == null) continue;

                var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId.Value).BlockSize;
                var element = CalcElement(block, blockId.Value, blockSize);
                result.Add(element);
            }

            return result;

            #region Internal

            BlueprintPlacementElement CalcElement(BlueprintBlockJsonObject block, BlockId blockId, Vector3Int blockSize)
            {
                var direction = (BlockDirection)block.Direction;
                for (var i = 0; i < rotationStep; i++) direction = direction.HorizonRotation();

                // 原点と最大セルを回転し、成分ごとのminを新原点にする（マルチセル対応）
                // Rotate origin and max cell; take component-wise min as new origin
                var originalDirection = (BlockDirection)block.Direction;
                var maxOffset = BlockPositionInfo.CalcBlockMaxPos(block.Offset, originalDirection, blockSize);
                var rotatedOrigin = RotateOffset(block.Offset, rotationStep);
                var rotatedMax = RotateOffset(maxOffset, rotationStep);
                var newOrigin = Vector3Int.Min(rotatedOrigin, rotatedMax);

                return new BlueprintPlacementElement(pasteAnchor + newOrigin, direction, blockId, block.Settings);
            }

            // 時計回り90度: (x, z) -> (z, -x)。HorizonRotation(North->East)と同回転
            // 90-degree clockwise: (x, z) -> (z, -x), matching HorizonRotation
            Vector3Int RotateOffset(Vector3Int offset, int steps)
            {
                var current = offset;
                for (var i = 0; i < steps; i++) current = new Vector3Int(current.z, current.y, -current.x);
                return current;
            }

            #endregion
        }
    }
}
