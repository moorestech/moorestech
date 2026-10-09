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
            if (!TryCalculatePlacements(blueprint, origin, rotationStep, out var result))
                Debug.LogWarning($"[BlueprintPaste] invalid coordinates origin={origin} rotation={rotationStep}");
            return result;
        }

        public static bool TryCalculatePlacements(BlueprintJsonObject blueprint, Vector3Int origin, int rotationStep,
            out List<BlueprintPlacementElement> result)
        {
            result = new List<BlueprintPlacementElement>();
            var offsets = new List<(long x, long y, long z)>();
            for (var blockIndex = 0; blockIndex < blueprint.Blocks.Count; blockIndex++)
            {
                var block = blueprint.Blocks[blockIndex];
                // マスタ欠損の警告は呼び出し側のBP解決時に一度出す。プレビュー毎フレームの計算では繰り返さない
                // The caller logs missing master entries once when resolving a blueprint, avoiding per-frame preview spam
                var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(block.BlockGuid);
                if (blockId == null) continue;

                var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId.Value).BlockSize;
                var direction = (BlockDirection)block.Direction;
                for (var step = 0; step < rotationStep; step++) direction = direction.HorizonRotation();
                var sizeOffset = BlockPositionInfo.CalcBlockMaxPos(Vector3Int.zero, (BlockDirection)block.Direction, blockSize);
                var first = Rotate(block.Offset.x, block.Offset.y, block.Offset.z);
                var last = Rotate((long)block.Offset.x + sizeOffset.x, (long)block.Offset.y + sizeOffset.y, (long)block.Offset.z + sizeOffset.z);
                offsets.Add((System.Math.Min(first.x, last.x), System.Math.Min(first.y, last.y), System.Math.Min(first.z, last.z)));
                result.Add(new BlueprintPlacementElement(blockIndex, Vector3Int.zero, direction, blockId.Value, block.Settings));
            }

            // 回転と平行移動は整数範囲を広げて計算する
            // Compute rotation and translation with wider integer arithmetic
            if (result.Count == 0) return true;
            var min = offsets[0];
            foreach (var offset in offsets)
                min = (System.Math.Min(min.x, offset.x), System.Math.Min(min.y, offset.y), System.Math.Min(min.z, offset.z));
            for (var i = 0; i < result.Count; i++)
            {
                var element = result[i];
                var offset = offsets[i];
                var x = origin.x + offset.x - min.x;
                var y = origin.y + offset.y - min.y;
                var z = origin.z + offset.z - min.z;
                var size = MasterHolder.BlockMaster.GetBlockMaster(element.BlockId).BlockSize;
                var max = BlockPositionInfo.CalcBlockMaxPos(Vector3Int.zero, element.Direction, size);
                if (!Fits(x, max.x) || !Fits(y, max.y) || !Fits(z, max.z))
                {
                    result.Clear();
                    return false;
                }
                result[i] = new BlueprintPlacementElement(element.BlockIndex, new Vector3Int((int)x, (int)y, (int)z),
                    element.Direction, element.BlockId, element.Settings);
            }
            return true;

            #region Internal

            bool Fits(long coordinate, int maxOffset)
            {
                return int.MinValue <= coordinate && coordinate + maxOffset <= int.MaxValue;
            }

            (long x, long y, long z) Rotate(long x, long y, long z)
            {
                for (var step = 0; step < rotationStep; step++) (x, z) = (z, -x);
                return (x, y, z);
            }

            #endregion
        }
    }
}
