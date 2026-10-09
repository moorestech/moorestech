using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Blueprint
{
    public static class BlueprintPasteCalculator
    {
        public static List<BlueprintPlacementElement> CalculatePlacements(BlueprintJsonObject blueprint, Vector3Int origin, int rotationStep)
        {
            var result = new List<BlueprintPlacementElement>();
            for (var blockIndex = 0; blockIndex < blueprint.Blocks.Count; blockIndex++)
            {
                var block = blueprint.Blocks[blockIndex];
                // マスタ欠損の警告は呼び出し側のBP解決時に一度出す。プレビュー毎フレームの計算では繰り返さない
                // The caller logs missing master entries once when resolving a blueprint, avoiding per-frame preview spam
                var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(block.BlockGuid);
                if (blockId == null) continue;

                var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId.Value).BlockSize;
                var element = CalcElement(blockIndex, block, blockId.Value, blockSize);
                result.Add(element);
            }

            // 回転後の外接箱最小角を指定原点へ平行移動する
            // Translate the rotated extent's minimum corner to the requested origin
            if (result.Count == 0) return result;
            var rotatedMin = result[0].Position;
            foreach (var element in result)
            {
                rotatedMin = Vector3Int.Min(rotatedMin, element.Position);
            }

            var shift = origin - rotatedMin;
            for (var i = 0; i < result.Count; i++)
            {
                var element = result[i];
                result[i] = new BlueprintPlacementElement(element.BlockIndex, element.Position + shift, element.Direction, element.BlockId, element.Settings);
            }
            return result;

            #region Internal

            BlueprintPlacementElement CalcElement(int blockIndex, BlueprintBlockJsonObject block, BlockId blockId, Vector3Int blockSize)
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

                return new BlueprintPlacementElement(blockIndex, newOrigin, direction, blockId, block.Settings);
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
