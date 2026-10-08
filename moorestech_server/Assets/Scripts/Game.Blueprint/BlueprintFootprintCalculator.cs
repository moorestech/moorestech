using UnityEngine;

namespace Game.Blueprint
{
    /// <summary>
    ///     回転後の全ブロックを包む直方体の寸法を求める
    ///     Measures the box enclosing all blocks after rotation
    /// </summary>
    public static class BlueprintFootprintCalculator
    {
        public static Vector3Int CalcSize(BlueprintJsonObject blueprint, int rotationStep)
        {
            var placements = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, rotationStep);

            // 空でも列の刻みは正
            // Keep the run stride positive when no blocks can be resolved
            if (placements.Count == 0)
            {
                Debug.Log($"[BlueprintFootprint] no resolvable blocks in blueprint {blueprint.BlueprintGuid}; stride folded to 1");
                return Vector3Int.one;
            }

            var first = BlueprintPlacementElementUtil.ToPositionInfo(placements[0]);
            var min = first.MinPos;
            var max = first.MaxPos;
            foreach (var placement in placements)
            {
                var info = BlueprintPlacementElementUtil.ToPositionInfo(placement);
                min = Vector3Int.Min(min, info.MinPos);
                max = Vector3Int.Max(max, info.MaxPos);
            }

            return max - min + Vector3Int.one;
        }
    }
}
