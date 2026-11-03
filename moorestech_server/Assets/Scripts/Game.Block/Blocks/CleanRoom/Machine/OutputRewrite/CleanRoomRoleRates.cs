using System;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.Yield;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
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

        // ダイシングの率を1回だけ計算する。損失率は品質合計で割り引き、Tierはダイシング装置の品質最高Tier（ダイの最大レベルで頭打ち）
        // Compute dicing rates once; loss is discounted by the quality sum and the tier is the machine's highest quality tier capped at the die's top level
        public static DicingPreview Dicing(DicingRecipeRole role, CleanRoomClassYield classYield, MachineModuleEffect moduleEffect)
        {
            var lossRate = ChipYieldFormula.ReducedRate(classYield.DieLossRate, moduleEffect.QualityShift, role.Dicing.MaxLossReduction);
            var qualityTier = Math.Min(moduleEffect.HighestQualityModuleTier, role.DieLevelCount);
            var table = role.Dicing.QualityTierDieTables[qualityTier];
            var levelRates = ChipYieldFormula.ShiftedDieRates(table.LevelRates, table.FailedRate, qualityTier, moduleEffect.QualityShift, classYield.QualityMultiplier, moduleEffect.HighestQualityModuleEffectValue, role.Dicing.MaxRareShift, out var failedRate);
            return new DicingPreview(lossRate, qualityTier, levelRates, failedRate);
        }
    }
}
