using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    // 清浄室機械の加工完了時チップ抽選を適用する
    // Applies clean-room chip draw when a machine cycle completes
    internal static class CleanRoomChipDrawApplyUtil
    {
        public static void ApplyChipDrawOnCompletion(ProcessingMachineProcessState processingState, CleanRoomEffect cleanRoomEffect, BlockInstanceId blockInstanceId, ref uint cycleCount)
        {
            // チップはレシピ出力の置き換えで個数は増えないため、開始時の容量判定は素の出力のままで有効
            // Chips only swap recipe outputs in place without increasing counts, so start-time capacity checks stay valid
            var recipeGuid = processingState.RecipeGuid;
            if (recipeGuid == Guid.Empty) return;
            if (!MasterHolder.CleanRoomMaster.TryGetChipDraw(recipeGuid, out var chipDraw)) return;
            // サイクル完了ごとにカウンタを進め、ブロック固有で再現可能なシードにする
            // Advance the counter each completed cycle and create a per-block reproducible seed
            cycleCount++;
            var seed = ((long)blockInstanceId.AsPrimitive() << 20) ^ cycleCount;
            var pendingOutputs = processingState.PendingOutputs;
            var replaced = new List<IItemStack>(pendingOutputs.Count);
            for (var i = 0; i < pendingOutputs.Count; i++)
            {
                replaced.Add(DrawSlot(pendingOutputs[i], i));
            }
            processingState.ReplacePendingOutputs(replaced);

            #region Internal

            IItemStack DrawSlot(IItemStack output, int outputIndex)
            {
                foreach (var distribution in chipDraw.OutputDistributions)
                {
                    if (MasterHolder.ItemMaster.GetItemId(distribution.OutputItemGuid) != output.Id) continue;
                    var levels = new List<(int level, double weight, ItemId chipItemId)>();
                    foreach (var level in distribution.Levels)
                    {
                        levels.Add((level.Level, level.Weight, MasterHolder.ItemMaster.GetItemId(level.ChipItemGuid)));
                    }
                    levels.Sort((a, b) => a.level.CompareTo(b.level));
                    var result = CleanRoomChipDraw.TryDraw(levels, cleanRoomEffect.MaxChipLevel, cleanRoomEffect.DownBinRate, chipDraw.EuvSuccessRate, seed, outputIndex, out var itemId);
                    return result == CleanRoomChipDraw.Result.Drawn
                        ? ServerContext.ItemStackFactory.Create(itemId, output.Count)
                        : ServerContext.ItemStackFactory.CreatEmpty();
                }
                // このレシピ出力に対応する抽選テーブルが無ければ素の出力のまま
                // If no distribution matches this recipe output, leave it unchanged
                return output;
            }

            #endregion
        }
    }
}
