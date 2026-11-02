using System;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.Yield;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom.Machine.OutputRewrite
{
    /// <summary>
    ///     役割ごとに部屋クラスの値とモジュール効果を ChipYieldFormula へ渡す唯一の口。抽選（書き換え器）と機械UIの表示が共有する
    ///     The single place that feeds per-role class values and module effects into ChipYieldFormula; shared by the draws (rewriters) and the machine UI
    /// </summary>
    internal static class CleanRoomRoleRates
    {
        // EUVの層失敗率＝クラス別の層失敗率×(1−min(品質合計, maxFailReduction))
        // EUV layer fail rate = class layer fail rate x (1 - min(quality sum, maxFailReduction))
        public static double LayerFailRate(LayerExposureRecipeRole role, CleanRoomClassYield classYield, MachineModuleEffect moduleEffect)
        {
            return ChipYieldFormula.ReducedRate(classYield.LayerFailRate, moduleEffect.QualityShift, role.Exposure.MaxFailReduction);
        }

        // ダイシングのダイ損失率＝クラス別のダイ損失率×(1−min(品質合計, maxLossReduction))
        // Dicing die loss rate = class die loss rate x (1 - min(quality sum, maxLossReduction))
        public static double DieLossRate(DicingRecipeRole role, CleanRoomClassYield classYield, MachineModuleEffect moduleEffect)
        {
            return ChipYieldFormula.ReducedRate(classYield.DieLossRate, moduleEffect.QualityShift, role.Dicing.MaxLossReduction);
        }

        // ダイシングの良品1個あたりの割合。Tierはダイシング装置の品質最高Tier（ダイの最大レベルで頭打ち）
        // Per-survivor dicing rates; the tier is the dicing machine's highest quality tier, capped at the die's top level
        internal static double[] DicingRates(DicingRecipeRole role, CleanRoomClassYield classYield, MachineModuleEffect moduleEffect, out int qualityTier)
        {
            qualityTier = Math.Min(moduleEffect.HighestQualityModuleTier, role.DieLevelCount);
            var table = role.Dicing.QualityTierDieTables[qualityTier];
            return ChipYieldFormula.ShiftedDieRates(table.LevelRates, table.FailedRate, qualityTier, moduleEffect.QualityShift, classYield.QualityMultiplier, moduleEffect.HighestQualityModuleEffectValue, role.Dicing.MaxRareShift);
        }
    }
}
