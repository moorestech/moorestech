using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using UnityEngine;

namespace Game.Block.Blocks.Machine.State
{
    /// <summary>
    ///     セーブから復元する加工中ジョブ（レシピ・残りtick・保留出力・継承レベル）。ジョブが無いことはこの型が無いこと（null）で表すので、継承レベルの null は「継承しないレシピのジョブ」だけを意味する
    ///     A running job restored from a save (recipe, remaining ticks, pending outputs, inherited level); no job is the absence of this type (null), so a null inherited level only means a job of a non-inheritance recipe
    /// </summary>
    public sealed class MachineProcessingJob
    {
        public readonly MachineRecipeMasterElement Recipe;
        public readonly uint RemainingTicks;

        // 保留出力。保留出力を持たない旧セーブは null で、完了時に一度だけ確定する
        // Pending outputs; null for old saves without them, realized once on completion
        public readonly List<IItemStack> PendingOutputs;

        // 継承レシピは 1..最大レベル、継承しないレシピは null
        // 1..max for an inheritance recipe, null for a non-inheritance recipe
        public readonly int? InheritedLevel;

        public MachineProcessingJob(MachineRecipeMasterElement recipe, uint remainingTicks, List<IItemStack> pendingOutputs, int? inheritedLevel)
        {
            // レシピと合わない継承レベルはセーブ破損かマイグレーション漏れなので、Lv1 などで黙って補わずに止める
            // An inherited level that does not fit the recipe means a corrupt save or a missed migration, so stop instead of silently filling in Lv1
            if (!MachineRecipeLevelInheritanceUtil.IsValidInheritedLevel(recipe, inheritedLevel)) throw new InvalidOperationException($"加工中ジョブの継承レベルが不正です recipe={recipe.MachineRecipeGuid} inheritedLevel={inheritedLevel}");

            Recipe = recipe;
            RemainingTicks = remainingTicks;
            PendingOutputs = pendingOutputs;
            InheritedLevel = inheritedLevel;
        }

        // セーブの平らなキーからジョブを組む。ジョブが無いか、レシピがマスタから消えていれば null
        // Builds the job from a save's flat keys; null when there is no job or its recipe has left the master
        public static MachineProcessingJob RestoreFromFlatSave(string recipeGuidStr, double remainingSeconds, List<ItemStackSaveJsonObject> pendingOutputs, int? inheritedLevel)
        {
            // ジョブ無しは Guid.Empty で保存される（清浄室機械の旧セーブはキー自体が無く null）
            // No job is saved as Guid.Empty (old clean-room saves lack the key, so it is null)
            if (string.IsNullOrEmpty(recipeGuidStr)) return null;
            var recipeGuid = Guid.Parse(recipeGuidStr);
            if (recipeGuid == Guid.Empty) return null;

            // レシピがマスタから消えていれば、継承レベルごとジョブを捨てる（加工中の状態は呼び出し側が Idle へ戻す。既存の縮退と同じ）
            // When the recipe has left the master, drop the job together with its inherited level (the caller returns a mid-job state to Idle, as the existing fallback does)
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid);
            if (recipe == null)
            {
                Debug.LogWarning($"[MachineProcessingJob] 加工中レシピがマスタにないため、ジョブを捨てて待機へ戻します recipe={recipeGuid} inheritedLevel={inheritedLevel}");
                return null;
            }

            var restoredOutputs = pendingOutputs?.Select(item => item.ToItemStack()).ToList();
            return new MachineProcessingJob(recipe, GameUpdater.SecondsToTicks(remainingSeconds), restoredOutputs, inheritedLevel);
        }
    }
}
