using System;
using System.Collections.Generic;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface.Component;
using Mooresmaster.Model.ItemsModule;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     抽選と同じ式に仮の部屋クラス・仮の装着を入れ、今との差分を並べる。数字が変わらない行は並べない
    ///     Feeds a hypothetical room class or loadout into the draw's own formula and lists the differences from now, dropping rows that change nothing
    /// </summary>
    internal static class CleanRoomYieldImprovementCalculator
    {
        // 表示の最小単位（0.1%）の半分。全項目の差分がこれ未満なら「数字が変わらない」
        // Half of the display step (0.1%); a row whose every delta is below this "changes nothing"
        public const double VisibleDeltaThreshold = 0.0005;

        // 並びは 部屋クラスを1つ上げる → 候補の品質モジュールを1個ずつ足す（候補の順）。current は部品が同じ装着から1回だけ出した今の最終の内訳
        // Order: one class up, then one more of each candidate quality module in candidate order; current is the final odds the component derived once from the same loadout
        public static List<CleanRoomYieldImprovement> Compute(CleanRoomRecipeRole role, CleanRoomClassYield classYield, IReadOnlyList<MachineModuleEffect.EquippedModule> equipped, CleanRoomFinalOdds current, IReadOnlyList<ModuleMasterElement> addableQualityModules)
        {
            var improvements = new List<CleanRoomYieldImprovement>();
            AddRoomClassUp();
            foreach (var module in addableQualityModules) AddQualityModule(module);
            return improvements;

            #region Internal

            void AddRoomClassUp()
            {
                // いちばん良いクラス（添字0）より上は無い。判定は C1 の1か所に任せる
                // Nothing is above the best class (index 0); the check is left to the single one from C1
                if (!CleanRoomClassYield.TryGetPromotionTargetIndex(classYield.ThresholdIndex, out var targetIndex)) return;
                var odds = OddsOf(CleanRoomClassYield.FromThresholdIndex(targetIndex), equipped);
                if (!TryGetVisibleDeltas(odds, out var levelDeltas, out var failedDelta)) return;
                var className = MasterHolder.CleanRoomMaster.Thresholds[targetIndex].ClassName;
                improvements.Add(new CleanRoomRoomClassUpImprovement(className, levelDeltas, failedDelta));
            }

            void AddQualityModule(ModuleMasterElement module)
            {
                // 今の装着にちょうど1個足した仮の装着で同じ式を回す
                // Run the same formula on a hypothetical loadout with exactly one more of the module
                var withOneMore = new List<MachineModuleEffect.EquippedModule>(equipped) { new(module, 1) };
                var odds = OddsOf(classYield, withOneMore);
                if (!TryGetVisibleDeltas(odds, out var levelDeltas, out var failedDelta)) return;
                improvements.Add(new CleanRoomAddQualityModuleImprovement(module.Tier, levelDeltas, failedDelta));
            }

            // 仮の条件で、抽選と同じ役割分岐（Evaluate）を抽選なしで回す
            // Run the draw's own role dispatch (Evaluate) without rolling, under the hypothetical condition
            CleanRoomFinalOdds OddsOf(CleanRoomClassYield yieldOfClass, IReadOnlyList<MachineModuleEffect.EquippedModule> modules)
            {
                var rates = CleanRoomYieldPreview.Evaluate(role, yieldOfClass, MachineModuleEffect.Aggregate(modules), null, out _);
                return CleanRoomFinalOdds.Of(role, rates);
            }

            // 今との差（Lv別と失敗）をここで1回だけ出し、見せるかの判定と行の組み立ての両方に同じ値を使う。失敗かLvのどれか1つでも表示で見える差があれば true
            // Derive the deltas from now (per level and failure) once here and use the same values for both the visibility check and the row; true when the failure or any level differs by a visible amount
            bool TryGetVisibleDeltas(CleanRoomFinalOdds odds, out double[] levelDeltas, out double failedDelta)
            {
                failedDelta = odds.FailedRate - current.FailedRate;
                levelDeltas = new double[odds.LevelRates.Count];
                var isVisible = VisibleDeltaThreshold <= Math.Abs(failedDelta);
                for (var i = 0; i < levelDeltas.Length; i++)
                {
                    levelDeltas[i] = odds.LevelRates[i] - current.LevelRates[i];
                    if (VisibleDeltaThreshold <= Math.Abs(levelDeltas[i])) isVisible = true;
                }
                return isVisible;
            }

            #endregion
        }
    }
}
