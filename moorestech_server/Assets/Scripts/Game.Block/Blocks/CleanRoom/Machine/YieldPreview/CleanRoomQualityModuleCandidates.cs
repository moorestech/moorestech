using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Machine.Module;
using Mooresmaster.Model.ItemsModule;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     改善の差分に並べる品質モジュールを選ぶ。今刺さっている最高 tier のものと、その1つ上の tier のもの。あと1個刺せないものは外す
    ///     Picks the quality modules for the improvement list: the highest equipped tier and the tier above it, dropping any that cannot take one more
    /// </summary>
    internal static class CleanRoomQualityModuleCandidates
    {
        // highestTier は部品が抽選と同じ集計（MachineModuleEffect.Aggregate）で1回だけ出した HighestQualityModuleTier。品質モジュールが無ければ0
        // highestTier is the HighestQualityModuleTier the component took once from the draw's own aggregate (MachineModuleEffect.Aggregate); zero without quality modules
        public static List<ModuleMasterElement> Select(MachineModuleEffectComponent effectComponent, int highestTier)
        {
            var candidates = new List<ModuleMasterElement>();

            // 刺さっている最高 tier のものを1個増やす。上限か空きで入らなければ並べない
            // One more of the highest equipped tier, unless the limit or free space keeps it out
            if (0 < highestTier && TryGetQualityModuleOfTier(highestTier, out var highest) && effectComponent.CanEquipOneMore(highest)) candidates.Add(highest);

            // その1つ上の tier を1個刺す。品質モジュールが無ければ tier1。マスタに無い tier（最上位の次）は並べない
            // One of the next tier; tier 1 when no quality module is equipped; a tier missing from the master (past the top) is not listed
            if (TryGetQualityModuleOfTier(highestTier + 1, out var nextTier) && effectComponent.CanEquipOneMore(nextTier)) candidates.Add(nextTier);
            return candidates;

            #region Internal

            // 指定 tier の品質モジュール定義。同じ tier が複数あればマスタの並びで先のもの。モジュールを定義しないマスタ（items.yml で optional）では false
            // The quality module of the tier; with several of one tier the first in master order; false for a master defining no modules (optional in items.yml)
            static bool TryGetQualityModuleOfTier(int tier, out ModuleMasterElement module)
            {
                module = null;
                if (MasterHolder.ItemMaster.Items.Modules == null) return false;
                foreach (var candidate in MasterHolder.ItemMaster.Items.Modules)
                {
                    if (candidate.EffectAxis != ModuleMasterElement.EffectAxisConst.Quality || candidate.Tier != tier) continue;
                    module = candidate;
                    return true;
                }
                return false;
            }

            #endregion
        }
    }
}
