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
        // 成膜のグレード上限＝装着品質モジュールの最高tier+1（グレード数で頭打ち）
        // Deposition grade cap = highest equipped quality tier + 1, capped by the grade count
        public static int MaxWaferGrade(WaferGradeRecipeRole role, MachineModuleEffect moduleEffect)
        {
            return ChipYieldFormula.MaxWaferGrade(moduleEffect.HighestQualityModuleTier, role.GradeCount);
        }

        // 成膜の1段の昇格確率＝品質合計×クラス倍率（上限maxPromotionChance）
        // Deposition per-step promotion chance = quality sum x class multiplier, capped by maxPromotionChance
        public static double PromotionChance(WaferGradeRecipeRole role, CleanRoomClassYield classYield, MachineModuleEffect moduleEffect)
        {
            return ChipYieldFormula.PromotionChance(moduleEffect.QualityShift, classYield.QualityMultiplier, role.Draw.MaxPromotionChance);
        }

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
    }
}
