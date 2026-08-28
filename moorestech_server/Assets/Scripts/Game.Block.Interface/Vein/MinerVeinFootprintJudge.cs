using UnityEngine;

namespace Game.Block.Interface.Vein
{
    /// <summary>
    ///     採掘機がどの鉱脈を掘れるかの唯一の判定。底面フットプリントと鉱脈AABBのXZ重なりだけを見る（ADR 0039）
    ///     The single judge of which vein a miner can mine: only the XZ overlap of its footprint and the vein AABB (ADR 0039)
    /// </summary>
    public static class MinerVeinFootprintJudge
    {
        public static bool OverlapsXz(BlockPositionInfo footprint, Vector3Int veinMinCell, Vector3Int veinMaxCell)
        {
            // 採掘機は地表に置く前提なので、斜面で鉱脈AABBのYから外れても掘れるようYは見ない
            // Miners sit on the surface, so Y is ignored to keep slopes from pushing them outside the vein AABB
            var min = footprint.MinPos;
            var max = footprint.MaxPos;
            return min.x <= veinMaxCell.x && veinMinCell.x <= max.x &&
                   min.z <= veinMaxCell.z && veinMinCell.z <= max.z;
        }
    }
}
