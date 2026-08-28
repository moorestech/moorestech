using Core.Master;
using Mooresmaster.Model.MineSettingsModule;
using UnityEngine;

namespace Game.Block.Interface.Vein
{
    /// <summary>
    ///     採掘対象vein判定の唯一の実装
    ///     - 底面とAABBのXZ重なりのみ判定
    ///     - mineSettings一致のveinだけ掘れる
    ///     - ADR0039準拠
    ///     The sole implementation deciding which vein a miner can target
    ///     - XZ overlap of footprint and AABB only
    ///     - Only veins listed in mineSettings are minable
    ///     - Per ADR0039
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

        public static bool CanMine(MineSettings mineSettings, ItemId veinItemId)
        {
            // 未対応鉱脈を対象に入れると採掘時間が決まらず毎tick産出するため、mineSettingsに無い鉱脈は掘れない
            // An unlisted vein has no mining time and would yield every tick, so only veins in mineSettings are minable
            foreach (var miningSetting in mineSettings.items)
                if (MasterHolder.ItemMaster.GetItemId(miningSetting.ItemGuid) == veinItemId)
                    return true;

            return false;
        }
    }
}
