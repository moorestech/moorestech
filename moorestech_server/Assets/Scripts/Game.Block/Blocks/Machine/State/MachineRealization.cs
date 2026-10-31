using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Machine.Module;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.State
{
    /// <summary>
    ///     確定した1回分の加工結果。実現出力と加工時間を同じモジュール効果の集計から決め、開始時に取り直さない
    ///     One realized job: the outputs and the processing time are fixed from the same module-effect aggregate and never recomputed at start
    /// </summary>
    internal sealed class MachineRealization
    {
        public readonly MachineRecipeMasterElement Recipe;
        public readonly int? InheritedLevel;
        public readonly List<IItemStack> Outputs;
        public readonly uint TotalTicks;
        public readonly float PowerMultiplier;

        // 差し替え口が確定時に使った値の記録（清浄室は抽選に使った率）。差し替えの無い出力は null
        // Record of the values the rewrite hook used when fixing (the clean room's draw rates); null when nothing was rewritten
        public readonly IRealizedOutputDrawRecord DrawRecord;

        public MachineRealization(MachineRecipeMasterElement recipe, int? inheritedLevel, List<IItemStack> outputs, uint totalTicks, float powerMultiplier, IRealizedOutputDrawRecord drawRecord)
        {
            Recipe = recipe;
            InheritedLevel = inheritedLevel;
            Outputs = outputs;
            TotalTicks = totalTicks;
            PowerMultiplier = powerMultiplier;
            DrawRecord = drawRecord;
        }

        // この確定結果が、いま揃っている入力（レシピと継承レベルの組）のものか。組が違えば確定し直す対象になる
        // Whether this result belongs to the currently ready input (recipe and inherited level); a different pair must be realized anew
        public bool IsRealizedFor(MachineRecipeMasterElement recipe, int? inheritedLevel)
        {
            return Recipe == recipe && InheritedLevel == inheritedLevel;
        }

        // レシピ時間にモジュールの時間倍率を掛けた加工tick数（最低1tick）
        // Processing ticks as the recipe time times the module time multiplier (at least one tick)
        public static uint ComputeTotalTicks(MachineRecipeMasterElement recipe, MachineModuleEffect effect)
        {
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            return (uint)Math.Max(1, (long)Math.Round(baseTicks * effect.ProcessingTimeMultiplier));
        }
    }
}
